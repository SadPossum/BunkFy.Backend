# Backend Release-Candidate Publication Task

Status: published; exact backend, web, and product candidate verification complete
Date: 2026-08-05

## Scope

Publish the completed backend and reusable GMA slices as one coherent
source-first graph. Product behavior stays in BunkFy; only reusable mechanics
and generic module capabilities are published from GMA repositories.

This task does not claim that a hosted deployment is production-ready. Private
infrastructure, secrets, legal policy, external observability, backup/restore,
and deployed multi-account evidence remain deployment-owned.

## Local Candidate Evidence

- Operations Notifications contributor and lifecycle coverage: 99 tests pass.
- `eng/verify.ps1 -SkipRestore`: synchronized solution and source packages,
  zero-warning build, zero migration drift, and 4,377 non-Docker tests pass.
- Repository security and release-evidence checks pass.
- NuGet audit reports zero projects with vulnerable direct or transitive
  packages.
- `git diff --check` passes for BunkFy Backend, each changed GMA repository,
  and GMA-Skeleton. Existing line-ending normalization notices are non-errors.
- Every local repository base matched its remote `dev` head before publication
  preparation.
- The exported BunkFy source set resolves every reusable dependency to a clean,
  published commit; only the expected uncommitted BunkFy root is dirty before
  its candidate commit.

The changed persistence paths already have exact relational evidence in their
own slice ledgers. The final compatibility and documentation edits do not add a
new persistence behavior, so another full Docker run would duplicate evidence
without reducing risk. Exact candidate CI remains required after publication.

## Exact Candidate Evidence

- BunkFy Backend `9b776a5` passed
  [validation](https://github.com/SadPossum/BunkFy.Backend/actions/runs/31026806385),
  [security](https://github.com/SadPossum/BunkFy.Backend/actions/runs/31026806198),
  [Docker integration](https://github.com/SadPossum/BunkFy.Backend/actions/runs/31046144477),
  and [release evidence](https://github.com/SadPossum/BunkFy.Backend/actions/runs/31046151810).
- BunkFy Web `e09f4e6` passed
  [validation](https://github.com/SadPossum/BunkFy.Web/actions/runs/31047540673)
  and [security](https://github.com/SadPossum/BunkFy.Web/actions/runs/31047540850).
  Its generated OpenAPI contracts match backend `9b776a5`; the local coherent
  gate passed type checking, lint, 150 tests, and the production build.
- BunkFy root `74a334e` records those exact backend and web commits and passed
  [validation](https://github.com/SadPossum/BunkFy/actions/runs/31047724330),
  [security](https://github.com/SadPossum/BunkFy/actions/runs/31047724323),
  [CodeQL](https://github.com/SadPossum/BunkFy/actions/runs/31047725182),
  [source release evidence](https://github.com/SadPossum/BunkFy/actions/runs/31047867616),
  and [unpublished OCI image evidence](https://github.com/SadPossum/BunkFy/actions/runs/31047869364).
  The image workflow built both candidates, generated SBOMs, scanned them, and
  attested the closed evidence set; it did not publish or deploy an image.

## Publication Order

1. [x] Publish GMA Framework at `0d84c22`.
2. [x] Publish Access Control `32b33cb`, Administration `24803d7`, Auth
   `7d67e11`, Files `a861666`, Notifications `1511fad`, Organizations
   `2e97b22`, Task Runtime `930e6a8`, and Tenancy `039dd5d`.
3. [x] Publish GMA-Skeleton at `3c77808` against that exact reusable source set;
   its generated-selection matrix, zero-warning build, migration drift, and
   2,385 non-Docker tests pass.
4. [x] Publish BunkFy Backend `9b776a5` and BunkFy Web `e09f4e6`, then collect
   their exact GitHub Actions evidence.
5. [x] Publish product root `74a334e` with those exact pointers without staging
   or rewriting the user-owned root solution, README, or private
   company-readiness material.

Use GitHub Actions once per exact candidate. Do not use remote CI as an
edit-by-edit development loop.

## Publication Acceptance

- Each source repository is clean, based on its current remote `dev`, and has
  security/release checks green at the exact commit.
- GMA-Skeleton generates and builds every supported selection against the
  published source set, including Notifications Admin CLI composition.
- BunkFy Backend records only published submodule commits and passes validation
  plus the exact Docker workflow at its candidate commit.
- The product superproject records the published backend and web candidates
  without unrelated root-file churn.

## Deployment-Owned Follow-Ups

- hosted exporter destination, access, retention, and canary evidence;
- durable-runtime and tenant-termination production activation approvals;
- deployed owner/applicant multi-account onboarding and access-profile smoke;
- production migration plan/apply, edge, object-store, key continuity, backup,
  restore, alert routing, and rollback evidence; and
- market-specific legal and retention policy approval.
