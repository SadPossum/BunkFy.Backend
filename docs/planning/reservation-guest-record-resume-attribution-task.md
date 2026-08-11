# Reservation Guest Record Resume Attribution Task

Status: completed

## Goal

Preserve the original authenticated actor when a later authorized caller resumes
a prepared Reservation Guest Record process before the Guest profile exists.
The resumer must still pass current endpoint permissions and module policy, but
must not silently replace the accepted operation's audit attribution.

## Finding

Reservations already persists the initiating actor on the link process and uses
it for the eventual Reservation mutation. The stateless cross-module extension,
however, supplied the current resumer to Guests. If the first request prepared
the process but did not create the Guest, the Guest and Reservation mutations
could therefore record different actors for one accepted operation.

## Boundary Decision

- Reservations remains the source of truth for accepted process coordinates and
  original actor attribution.
- Its narrow preparation capability may return that actor to the extension while
  the process remains actionable.
- The public process/status DTO, HTTP response, ready event, task payload, logs,
  and metrics must not gain actor data.
- The extension continues to send the current caller to `PrepareAsync`, preserving
  current authorization and policy evaluation, then uses Reservations' persisted
  actor only for the idempotent Guest creation command.
- No profile fields or profile fingerprint are persisted outside Guests.
- Impossible workflow states and cross-module identity mismatches are server
  contract failures, not caller validation errors, and return HTTP 500.

## Verification

- Reservations tests prove fresh-operation resume returns the original actor.
- Extension tests prove the current caller prepares the resume while Guest
  creation uses the original actor.
- Invalid prepared capability state fails closed before Guests is invoked.
- Endpoint tests prove internal workflow contract failures use server-error
  semantics rather than the generic HTTP 400 fallback.
- Contract-shape tests keep actor attribution out of the public process DTO.
- Run focused Reservations and extension tests while editing, then one
  consolidated non-Docker gate for the completed slice.

## Completion Evidence

- Focused Reservations tests passed 263/263 and Reservation Guest Records
  extension tests passed 15/15.
- The deterministic Reservations personal-data inventory was regenerated and
  its completeness guard classifies the capability-only actor response.
- `eng/verify.ps1 -SkipRestore` passed solution synchronization, source-package
  checks, a zero-warning solution build, all migration drift checks, every
  non-Docker project suite, 102 architecture tests, and 60 non-Docker
  integration tests.
- Docker and hosted CI were intentionally not rerun for this schema-free,
  in-process contract slice.
