# Task Runtime Data Rights Adapter

This extension maps a BunkFy workspace id to GMA Task Runtime's generic
`ScopeId` lifecycle. It contributes mandatory tenant destruction after Access
Control and owns no database.

Task runs and control messages are operational copies. Their task-owning domain
modules remain authoritative, so this adapter deliberately exposes no tenant
portability export. It closes new enqueue, retry, control, and claim admission;
waits for leased work to become terminal; and validates the exact bounded GMA
removal receipt against the frozen Workspace fence.

Final Task Runtime cleanup must run outside the target workspace task scope. A
tenant-scoped task cannot delete its own durable run before the worker records
completion.
