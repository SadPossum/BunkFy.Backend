# task-runtime Personal-Data Inventory v1

Generated from `task-runtime.personal-data` schema v1.
Catalogue approval: `engineering-default`.

Engineering metadata is not legal or country-launch approval.

## Access Policies

| Id | Scope | Readers | Writers |
|---|---|---|---|
| task-runtime-operational-state | task-runtime-maintenance-and-tenant-termination | system:data-rights-termination<br>system:task-runtime | system:task-runtime |

## Retention Policies

| Id | Approval | Starts | Ends or duration | Legal hold |
|---|---|---|---|---|
| task-runtime-product-configured-retention | engineering-default | task-copy-created | product-configured-retention-or-approved-tenant-termination | product-policy-required |

## Rights Policies

| Id | Export | Correction | Restriction | Erasure |
|---|---|---|---|---|
| task-runtime-operational-copy | excluded-non-authoritative-operational-copy | correct-through-authoritative-task-owning-domain | close-scope-and-drain-active-work | destroy-after-approved-tenant-termination |

## Fields

| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| task-runtime.actor-id | staff | pseudonymous-identifier | elevated | task-execution<br>tenant-termination | auth<br>task-runtime | auth | customer-controller-bunk-fy-processor | task-runtime-operational-state | task-runtime.actor-id | task-runtime-product-configured-retention | task-runtime-operational-copy | persistence | cross-module | engineering-default |
| task-runtime.correlation-id | subject-scoped | pseudonymous-identifier | standard | task-execution<br>tenant-termination | product-task-owner<br>task-runtime | product-task-owner | customer-controller-bunk-fy-processor | task-runtime-operational-state | task-runtime.correlation-id | task-runtime-product-configured-retention | task-runtime-operational-copy | persistence | cross-module | engineering-default |
| task-runtime.deduplication-key | subject-scoped | linked-operational | elevated | task-execution<br>tenant-termination | product-task-owner<br>task-runtime | product-task-owner | customer-controller-bunk-fy-processor | task-runtime-operational-state | task-runtime.deduplication-key | task-runtime-product-configured-retention | task-runtime-operational-copy | persistence | cross-module | engineering-default |
| task-runtime.operational-message | subject-scoped | free-text | elevated | task-execution<br>tenant-termination | product-task-owner<br>task-runtime | product-task-owner | customer-controller-bunk-fy-processor | task-runtime-operational-state | task-runtime.operational-message | task-runtime-product-configured-retention | task-runtime-operational-copy | persistence | cross-module | engineering-default |
| task-runtime.payload | subject-scoped | free-text | elevated | task-execution<br>tenant-termination | product-task-owner<br>task-runtime | product-task-owner | customer-controller-bunk-fy-processor | task-runtime-operational-state | task-runtime.payload | task-runtime-product-configured-retention | task-runtime-operational-copy | persistence | cross-module | engineering-default |
| task-runtime.scope-id | subject-scoped | pseudonymous-identifier | standard | task-execution<br>tenant-termination | task-runtime<br>workspaces | workspaces | customer-controller-bunk-fy-processor | task-runtime-operational-state | task-runtime.scope-id | task-runtime-product-configured-retention | task-runtime-operational-copy | persistence | cross-module | engineering-default |
| task-runtime.worker-id | staff | pseudonymous-identifier | standard | task-execution<br>tenant-termination | task-runtime<br>worker-host | worker-host | customer-controller-bunk-fy-processor | task-runtime-operational-state | task-runtime.worker-id | task-runtime-product-configured-retention | task-runtime-operational-copy | persistence | cross-module | engineering-default |

## Code Bindings

| Field | Assembly | Type | Member | Surface | Effective retention |
|---|---|---|---|---|---|
| task-runtime.actor-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskControlMessageState | RequestedBy | persistence | task-runtime-product-configured-retention |
| task-runtime.actor-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | CancellationRequestedBy | persistence | task-runtime-product-configured-retention |
| task-runtime.actor-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | RequestedBy | persistence | task-runtime-product-configured-retention |
| task-runtime.correlation-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | CorrelationId | persistence | task-runtime-product-configured-retention |
| task-runtime.deduplication-key | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | DeduplicationKey | persistence | task-runtime-product-configured-retention |
| task-runtime.operational-message | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskControlMessageState | LastError | persistence | task-runtime-product-configured-retention |
| task-runtime.operational-message | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | LastError | persistence | task-runtime-product-configured-retention |
| task-runtime.operational-message | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | ProgressMessage | persistence | task-runtime-product-configured-retention |
| task-runtime.payload | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskControlMessageState | Payload | persistence | task-runtime-product-configured-retention |
| task-runtime.payload | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | Payload | persistence | task-runtime-product-configured-retention |
| task-runtime.scope-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskControlMessageState | ScopeId | persistence | task-runtime-product-configured-retention |
| task-runtime.scope-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | ScopeId | persistence | task-runtime-product-configured-retention |
| task-runtime.worker-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | LockedBy | persistence | task-runtime-product-configured-retention |
| task-runtime.worker-id | Gma.Framework.Tasks.Infrastructure | Gma.Framework.Tasks.Infrastructure.TaskRun | NodeId | persistence | task-runtime-product-configured-retention |
