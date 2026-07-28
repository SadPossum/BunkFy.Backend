# BunkFy Backend Support Policy

## Release Channels

BunkFy Backend is a component of the composed BunkFy product, not an
independently supported distribution. The `dev` branch is its changing
integration line. Workflow-dispatch runs create reviewable component evidence
only.

| Channel | Status |
| --- | --- |
| `dev` | Pre-release integration |
| Component candidate | Evidence for a BunkFy composition candidate |
| Independent production release | None |

## Compatibility

A candidate contains the owned repository archive plus a source-set manifest
identifying every composed repository commit. The BunkFy product composition
must pin the exact backend commit and source set it validates.

No standalone backend compatibility promise is made. Product compatibility
belongs to the composed BunkFy release and its release notes.

## End Of Life

Backend candidates follow the lifecycle of the BunkFy composition that pins
them. An unreferenced component candidate is not a supported release.

## Support

Security reports follow `SECURITY.md`. Component maintenance is best effort and
has no contractual support SLA independent of the composed BunkFy product.
