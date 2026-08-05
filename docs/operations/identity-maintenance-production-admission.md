# Identity Maintenance Production Admission

BunkFy Production hosts fail closed until Auth retention and Organizations
maintenance each have an approved policy and one declared Worker owner. GMA
owns the generic bounded jobs; these declarations select BunkFy's retention
windows and deployment topology.

## Independent Policies

Every public API, Admin API, and Worker receives the same Auth declaration:

```json
{
  "BunkFy": {
    "AuthRetention": {
      "ProductionAdmission": {
        "ApprovalState": "Approved",
        "ApprovalReference": "ops/auth-retention-2026-08",
        "MaintenanceOwner": "Worker",
        "MaintenanceOwnerInstanceCount": 1,
        "CurrentProcessOwnsMaintenance": false,
        "ExistingHistoryDisposition": "ApplyApprovedWindows",
        "ExpiredExchangeHistoryHours": 24,
        "PasswordRecoveryHistoryHours": 24,
        "SessionHistoryDays": 365,
        "AuthenticationChallengeHistoryHours": 24,
        "ExpiredTotpEnrollmentHistoryHours": 24,
        "DisabledTotpAuthenticatorHistoryDays": 365,
        "MultiFactorFailureHistoryHours": 24,
        "AuthenticationFailureHistoryHours": 24
      }
    }
  }
}
```

Organizations has a separate declaration so its owner can be split later:

```json
{
  "BunkFy": {
    "OrganizationsMaintenance": {
      "ProductionAdmission": {
        "ApprovalState": "Approved",
        "ApprovalReference": "ops/organizations-maintenance-2026-08",
        "MaintenanceOwner": "Worker",
        "MaintenanceOwnerInstanceCount": 1,
        "CurrentProcessOwnsMaintenance": false,
        "ExistingHistoryDisposition": "ApplyApprovedWindows",
        "InvitationHistoryDays": 90,
        "EnrollmentHistoryDays": 90
      }
    }
  }
}
```

These values are examples, not legal or security approval. Replace references
and windows with reviewed deployment decisions. References are non-secret
evidence identifiers and must not contain credentials or personal data.

`ApplyApprovedWindows` is an explicit acknowledgement that the first cleanup
run may remove pre-existing records older than the approved windows. Inspect
backlog age and preserve any required investigation or legal-hold evidence
before changing this value from `Unspecified`.

## Maintenance Owners

Exactly one Auth-maintenance Worker profile sets:

```text
BunkFy__AuthRetention__ProductionAdmission__CurrentProcessOwnsMaintenance=true
Auth__Retention__Enabled=true
Worker__Modules__Auth=true
```

Exactly one Organizations-maintenance Worker profile sets:

```text
BunkFy__OrganizationsMaintenance__ProductionAdmission__CurrentProcessOwnsMaintenance=true
Organizations__Lifecycle__Enabled=true
Organizations__Retention__Enabled=true
Worker__Modules__Organizations=true
```

The same Worker profile may own both policies. API, Admin API, and every
non-owner Worker keep the corresponding current-owner and runtime switches
false. Startup validates declarations and local composition, while deployment
evidence must reconcile each declared owner count against actual replicas.

## Composition Failures

Production startup rejects:

- pending approval, missing evidence, unacknowledged existing history, or an
  owner instance count other than one;
- maintenance ownership outside a Worker or without the required module;
- an enabled job on a non-owner or a disabled job on the selected owner;
- Organizations lifecycle and retention split inside one owner policy;
- invalid or runtime-mismatched Auth and Organizations history windows.

## Activation Evidence

Before real tenant data, retain both approved policies, exact release/image,
orchestrator owner replica counts, database backup and restore results, initial
eligible-row counts, cleanup duration and deletion counts, oldest-history
alerts, and a restart/failure drill. Auth active sessions and Organizations
active or pending workflow records are protected by their module semantics;
never manually delete module tables to force a clean dashboard.
