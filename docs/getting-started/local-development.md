# Local Development

Current backend validation:

```powershell
.\eng\verify.ps1
```

The repository includes copied GMA skeleton examples under `src/Modules/Catalog`, `src/Modules/Ordering`, and `src/Modules/TaskSamples`. Use them as working references for module shape, contracts, persistence, migrations, front-door wiring, and tests. Future backend development should add product modules in focused slices under `src/Modules`.

Expected future product modules include:

- Properties.
- Inventory.
- Reservations.
- Guests.
- Billing.
- Housekeeping.

Keep reusable framework changes in the appropriate nested GMA repository and update the backend submodule pointer deliberately after validation.

For backend-only composition, run:

```powershell
.\eng\run-aspire.ps1
```

