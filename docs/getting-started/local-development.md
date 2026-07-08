# Local Development

Current backend validation:

```powershell
.\eng\verify.ps1
```

The initial repository foundation keeps runtime behavior intentionally small. Future backend development should add product modules in focused slices under `src/Modules`.

Expected future product modules include:

- Properties.
- Inventory.
- Reservations.
- Guests.
- Billing.
- Housekeeping.

Keep reusable framework changes in the appropriate GMA repository and update the root superproject submodule pointer deliberately after validation.

