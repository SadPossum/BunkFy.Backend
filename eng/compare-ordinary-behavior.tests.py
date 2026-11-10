import copy
import importlib.util
import json
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('comparison', Path(__file__).with_name('compare-ordinary-behavior.py'))
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


def document(variant):
    return {'evidenceId': 'BFX-SS-K0-CMP-0001', 'variant': variant, 'buildFingerprint': variant + '-source',
            'denominator': {'operations': 26, 'candidateControlObservations': 52, 'comparisons': 26},
            'normalizationRules': ['Only producer-declared field paths are normalized.'],
            'normalizationPathAllowlist': {'tokenPaths': ['$.accessToken']},
            'observations': [{'id': name, 'ordinal': index, 'expectedStatus': 200, 'actualStatus': 200,
                              'result': 'pass', 'headers': {'cache-control': ['no-store']},
                              'runtime': {'effectiveAuthorizationResultHandlerCount': 1}}
                             for index, name in enumerate(comparison.OPERATION_IDS, 1)]}


def session_document(variant, metadata=False):
    value = document(variant)
    value['normalizationPathAllowlist']['timestampPaths'] = ['$.sessions[].loginDateTimeUtc']
    sessions = [dict(sessionId='<generated-id>', authenticationMethod='password',
                     isCurrent=current, loginDateTimeUtc='<timestamp>') for current in (True, False)]
    if metadata:
        value['normalizationPathAllowlist']['timestampPaths'].insert(0, comparison.SESSION_ACTIVITY_PATH)
        for session in sessions:
            session.update(lastActivityAtUtc='<timestamp>', userAgent=None)
    observation = value['observations'][12]
    observation.update(method='GET', path='/api/auth/sessions', fixture='primary-account', state='two-active-sessions')
    set_sessions(observation, sessions)
    return value


def set_sessions(observation, sessions):
    # Match the producer's ordered canonical JSON and HTML-safe placeholders.
    observation['canonicalBody'] = json.dumps({'sessions': sessions}, sort_keys=True, separators=(',', ':')).replace('<', '\\u003C').replace('>', '\\u003E')
    kind = lambda value: 'null' if value is None else 'boolean' if type(value) is bool else 'string'
    observation['bodySchema'] = sorted(['$.sessions:array', '$.sessions[]:array'] +
                                      [f'$.sessions[].{key}:{kind(value)}' for key, value in sessions[0].items()])


class OrdinaryComparisonSafetyTests(unittest.TestCase):
    def compare(self, control, candidate):
        return comparison.compare(control, candidate, 'control-source', 'candidate-source')

    def test_compares_all_52_observations_while_preserving_distinct_source_provenance(self):
        result = self.compare(document('control'), document('candidate'))
        self.assertTrue(result['passed'])
        self.assertEqual(52, result['observations'])
        self.assertEqual(26, result['comparisons'])

    def test_a_single_header_change_is_a_failure_with_its_exact_path(self):
        candidate = document('candidate')
        candidate['observations'][19]['headers']['cache-control'] = ['public']
        result = self.compare(document('control'), candidate)
        self.assertFalse(result['passed'])
        self.assertEqual(['$.observations[19].headers.cache-control[0]'], result['differentPaths'])

    def test_missing_duplicate_empty_and_unsuccessful_operations_fail_closed(self):
        for mode in ('missing', 'duplicate', 'empty', 'failed', 'multiple-handlers'):
            control, candidate = document('control'), document('candidate')
            for item in (control, candidate):
                if mode == 'missing': item['observations'].pop()
                elif mode == 'duplicate': item['observations'][1] = copy.deepcopy(item['observations'][0])
                elif mode == 'empty': item['observations'] = []
                elif mode == 'failed': item['observations'][0]['result'] = 'fail'
                else: item['observations'][0]['runtime']['effectiveAuthorizationResultHandlerCount'] = 2
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                self.compare(control, candidate)

    def test_changed_redaction_rules_are_compared_instead_of_silently_adopted(self):
        candidate = document('candidate')
        candidate['normalizationPathAllowlist']['tokenPaths'].append('$.*')
        self.assertFalse(self.compare(document('control'), candidate)['passed'])

    def test_wrong_source_and_mislabeled_control_are_rejected(self):
        for field, value in [('buildFingerprint', 'other-source'), ('variant', 'candidate')]:
            control = document('control')
            control[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.compare(control, document('candidate'))

    def test_duplicate_json_fields_are_rejected(self):
        with self.assertRaises(ValueError):
            json.loads('{"variant":"control","variant":"candidate"}', object_pairs_hook=comparison.unique_object)

    def test_records_exact_session_metadata_extension_without_mutating_raw_evidence(self):
        control, candidate = session_document('control'), session_document('candidate', metadata=True)
        original = copy.deepcopy(candidate)
        result = self.compare(control, candidate)
        self.assertTrue(result['passed'])
        self.assertEqual(comparison.SESSION_METADATA_CHANGE, result['approvedAdditions'][0]['id'])
        self.assertEqual(['lastActivityAtUtc', 'userAgent'], result['approvedAdditions'][0]['fields'])
        self.assertEqual(original, candidate)

    def test_existing_session_fields_and_all_other_observations_stay_exact(self):
        for mode in ('method', 'current', 'order', 'extra', 'header', 'runtime', 'another-operation'):
            candidate = session_document('candidate', metadata=True)
            observation = candidate['observations'][12]
            sessions = json.loads(observation['canonicalBody'])['sessions']
            if mode == 'method': sessions[0]['authenticationMethod'] = 'changed'
            elif mode == 'current': sessions[0]['isCurrent'] = False
            elif mode == 'order': sessions.reverse()
            elif mode == 'extra': sessions[0]['unreviewedField'] = 'unexpected'
            elif mode == 'header': observation['headers']['cache-control'] = ['public']
            elif mode == 'runtime': observation['runtime']['handlerKind'] = 'changed'
            else: candidate['observations'][0]['canonicalBody'] = observation['canonicalBody']
            set_sessions(observation, sessions)
            with self.subTest(mode=mode):
                self.assertFalse(self.compare(session_document('control'), candidate)['passed'])

    def test_missing_malformed_or_unexpected_new_metadata_is_not_accepted(self):
        for mode in ('missing-activity', 'missing-agent', 'unknown-activity', 'bad-activity', 'agent-present', 'schema', 'allowlist'):
            candidate = session_document('candidate', metadata=True)
            observation = candidate['observations'][12]
            sessions = json.loads(observation['canonicalBody'])['sessions']
            if mode == 'missing-activity': sessions[0].pop('lastActivityAtUtc')
            elif mode == 'missing-agent': sessions[0].pop('userAgent')
            elif mode == 'unknown-activity': sessions[0]['lastActivityAtUtc'] = None
            elif mode == 'bad-activity': sessions[0]['lastActivityAtUtc'] = 'invalid'
            elif mode == 'agent-present': sessions[0]['userAgent'] = 'unexpected browser'
            set_sessions(observation, sessions)
            if mode == 'schema': observation['bodySchema'].remove('$.sessions[].userAgent:null')
            if mode == 'allowlist': candidate['normalizationPathAllowlist']['timestampPaths'].append('$.*')
            with self.subTest(mode=mode):
                self.assertFalse(self.compare(session_document('control'), candidate)['passed'])

    def test_metadata_is_compared_exactly_once_the_control_already_has_it(self):
        control = session_document('control', metadata=True)
        candidate = session_document('candidate', metadata=True)
        result = self.compare(control, candidate)
        self.assertTrue(result['passed'])
        self.assertEqual([], result['approvedAdditions'])
        sessions = json.loads(candidate['observations'][12]['canonicalBody'])['sessions']
        sessions[0]['userAgent'] = 'different'
        set_sessions(candidate['observations'][12], sessions)
        self.assertFalse(self.compare(control, candidate)['passed'])


if __name__ == '__main__':
    unittest.main()
