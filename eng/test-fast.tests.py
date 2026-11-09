"""Exercise test-fast.ps1's real CLI forwarding without compiling projects."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

ENG = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('worker_results', ENG / 'check-worker-timing-results.py')
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)


class FastTestInvocationTests(unittest.TestCase):
    def invoke(self, arguments, no_build=True):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            eng = root / 'eng'
            eng.mkdir()
            shutil.copyfile(ENG / 'test-fast.ps1', eng / 'test-fast.ps1')
            shutil.copyfile(ENG / 'check-worker-timing-results.py', eng / 'check-worker-timing-results.py')
            (root / 'BunkFy.slnx').write_text('<Solution><Project Path="Gma.Framework.Tests.csproj" /></Solution>')
            (root / 'Gma.Framework.Tests.csproj').write_text(
                '<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>')
            report = ET.Element('TestRun')
            definitions = ET.SubElement(report, 'TestDefinitions')
            results = ET.SubElement(report, 'Results')
            for index, name in enumerate(sorted(evidence.REQUIRED_CASES)):
                ET.SubElement(definitions, 'UnitTest', id=str(index), name=name,
                              storage='Gma.Framework.Tests.dll')
                ET.SubElement(results, 'UnitTestResult', testId=str(index), testName=name,
                              executionId=str(index), outcome='Passed')
            ET.SubElement(ET.SubElement(report, 'ResultSummary'), 'Counters',
                          total='11', executed='11', passed='11', failed='0', notExecuted='0')
            ET.ElementTree(report).write(root / 'fixture.trx')
            (eng / 'common.ps1').write_text(r'''
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:FixtureRoot = Split-Path $PSScriptRoot -Parent
function Join-GmaPath { param([string] $Path) Join-Path $script:FixtureRoot $Path }
function Invoke-GmaDotNet {
    param([string[]] $Arguments)
    Add-Content -LiteralPath (Join-GmaPath 'calls.jsonl') -Value (ConvertTo-Json -InputObject $Arguments -Compress)
    $resultIndex = [Array]::IndexOf($Arguments, '--results-directory')
    if ($resultIndex -ge 0) {
        $destination = $Arguments[$resultIndex + 1]
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item -LiteralPath (Join-GmaPath 'fixture.trx') -Destination (Join-Path $destination 'worker.trx')
    }
}
''')
            command = ['pwsh', '-NoProfile', '-File', str(eng / 'test-fast.ps1'), '-IsolateWorkerTimingContracts']
            if no_build:
                command.append('-NoBuild')
            command.extend(arguments)
            environment = {**os.environ, 'PYTHONDONTWRITEBYTECODE': '1'}
            if '--' in arguments:
                # PowerShell's native -File binder treats a bare -- as a parameter.
                # Pass runsettings through the script's explicit string[] parameter.
                environment.update(FAST_TEST_SCRIPT=str(eng / 'test-fast.ps1'),
                                   FAST_TEST_ARGUMENTS=json.dumps(arguments))
                command = ['pwsh', '-NoProfile', '-Command',
                           '& $env:FAST_TEST_SCRIPT -IsolateWorkerTimingContracts '
                           + ('-NoBuild ' if no_build else '')
                           + '-DotNetArguments @(ConvertFrom-Json -InputObject $env:FAST_TEST_ARGUMENTS)']
            result = subprocess.run(command, cwd=root, capture_output=True, text=True,
                                    env=environment)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            calls = [json.loads(line) for line in (root / 'calls.jsonl').read_text().splitlines()]
        self.assertEqual(len(calls), 2)
        self.assertIn('FullyQualifiedName!~Gma.Framework.Tests.Tasks.TaskWorkerServiceTests.', calls[0][calls[0].index('--filter') + 1])
        self.assertEqual(calls[1][calls[1].index('--filter') + 1],
                         'FullyQualifiedName~Gma.Framework.Tests.Tasks.TaskWorkerServiceTests.')
        self.assertIn('trx', calls[1])
        self.assertIn('--results-directory', calls[1])
        return calls

    def test_preserves_release_framework_runtime_and_output_in_both_invocations(self):
        for configuration in ('--configuration', '-c'):
            with self.subTest(configuration=configuration):
                options = [configuration, 'Release', '--framework', 'net10.0',
                           '--runtime', 'win-x64', '--output', 'build output',
                           '--artifacts-path', 'artifacts path']
                for call in self.invoke(options):
                    for option, value in zip(options[::2], options[1::2]):
                        self.assertEqual(call[call.index(option) + 1], value)
                        self.assertLess(call.index(option), call.index('--'))
                    self.assertIn('--no-build', call)

    def test_preserves_runsettings_after_one_separator_and_enforces_serial_collections(self):
        options = ['--configuration', 'Release', '--', 'RunConfiguration.TestSessionTimeout=60000']
        for call in self.invoke(options):
            self.assertEqual(call.count('--'), 1)
            self.assertIn('RunConfiguration.TestSessionTimeout=60000', call[call.index('--') + 1:])
            self.assertEqual(call[-1], 'xUnit.ParallelizeTestCollections=false')

    def test_allows_building_when_no_build_was_not_requested(self):
        for call in self.invoke(['--configuration', 'Release'], no_build=False):
            self.assertNotIn('--no-build', call)


if __name__ == '__main__':
    unittest.main()
