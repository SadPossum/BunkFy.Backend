# Access Control Data Rights Adapter

`BunkFy.Extensions.DataRights.AccessControl` composes GMA Access Control's
product-neutral scope lifecycle into BunkFy's workspace termination control
plane.

The extension owns no database. It maps the exact canonical workspace id to:

- `WorkspaceAccessScopes.Create(workspaceId)` as the Access Control root; and
- the same workspace id as the Access Control inbox transport scope.

It requires the frozen Workspace fence, depends on the `organizations`
termination owner, and exposes four typed portability streams: scoped role
assignments, profiles, profile assignments, and profile change history. Role
definitions remain global; each exported role assignment carries an ordered
permission snapshot. Auth accounts and sessions remain global subject-owned
identity data and are excluded.

The adapter references `Gma.Modules.AccessControl.Contracts` only. GMA owns
revision fencing, scope closure, bounded erasure, globally safe principal
cleanup, transport suppression, and exact replay proof. BunkFy owns owner
ordering, request/fence validation, result codes, export schema, and personal
data classification.
