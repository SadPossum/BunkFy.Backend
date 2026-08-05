# Files Tenant Termination Boundary Task

Status: completed by composition boundary
Date: 2026-08-04

## Goal

Decide whether BunkFy's current tenant-termination catalogue needs a generic
Files owner without confusing object-storage infrastructure with ownership of
the records and bytes stored through it.

## Decision

BunkFy does not compose `Gma.Modules.Files.Api` in any production host. The
generic Files module therefore owns no BunkFy object and must not be registered
as a mandatory tenant-termination contributor.

`Gma.Framework.FileManagement` remains shared storage infrastructure. It is not
an owner facade and must not enumerate or erase a tenant merely because an
object key happens to contain a scope-derived segment.

The current BunkFy object namespaces have explicit product owners:

- Ingestion owns raw source payload purpose, references, legal holds,
  retention, export decisions, and deletion through `IRawPayloadStore`.
- Data Rights owns protected subject exports, tenant-export fragments, final
  artifacts, expiry, restore consequences, and their opaque storage keys.

Those objects stay inside their owning domain's lifecycle. A second generic
Files contributor would duplicate authority and could delete an export proof
or held source payload before its owner permits removal.

## Why No Generic Cleanup Facade Was Added

The current Files front door intentionally has no persistent catalogue. Its
storage contract supports exact-key reads and deletes only. The legacy Files
namespace also uses truncated scope and subject digests. Prefix deletion would
therefore provide neither exact ownership proof nor a safe replay receipt, and
could not coordinate an in-flight upload with scope closure.

Adding a nominal `DeletePrefix` method would hide those correctness gaps. A
future reusable lifecycle must instead accompany an admitted file workflow and
provide all of the following as one coherent GMA Files change:

- a durable, scope-addressable object catalogue or an equivalently exact
  provider-neutral ownership index;
- admission closure serialized with object reservation before bytes are
  written;
- recovery for reserved, uploaded, deleting, and orphaned objects across a
  database/object-store partial failure;
- bounded destruction, immutable operation-bound replay proof, and conflict
  handling;
- explicit treatment or migration of legacy truncated-digest objects;
- product-owned retention, legal-hold, portability, derivative, cache, and
  backup policy; and
- exact LocalStorage and MinIO provider scenarios.

That work belongs in GMA Files when a real reusable private-object workflow
needs it. BunkFy-specific document, image, invoice, or attachment policy must
remain in its product domain.

## Guardrail

Architecture checks keep the generic Files API absent from every BunkFy host
and pin direct production `IFileStorage` use to the reviewed Ingestion and Data
Rights owner adapters. Introducing another object-backed module requires an
explicit ownership review and a deliberate guard update.

## Acceptance

- no BunkFy host composes or references `Gma.Modules.Files.Api`;
- the tenant-termination owner topology contains no false Files owner;
- current object storage use is limited to exact product-owned adapters;
- documentation distinguishes GMA Files from Framework FileManagement; and
- future Files admission is blocked on an exact lifecycle rather than prefix
  deletion or host-wide bucket cleanup.

No GMA source change, migration, or Docker verification is required for this
composition-only decision.
