# GMA Source Mounts

BunkFy consumes GMA as editable source under stable Git submodule paths:

- framework: `gma/framework`;
- reusable modules: `gma/modules/<alias>`.

After cloning or updating the mounts, prepare and validate the backend with:

```powershell
.\eng\gma-update.ps1 -Init
.\eng\gma-bootstrap.ps1 -Force
.\eng\update-solutions.ps1
.\eng\verify.ps1
```

Application modules stay under `src/Modules` and use the `BunkFy.Modules.<Module>` project prefix. Create one through `eng/new-module.ps1`; its implementation is owned by the mounted framework.

Make reusable fixes inside the owning GMA checkout on a branch, validate its focused solution and BunkFy, then publish GMA before updating BunkFy's submodule pointer. Product workflows, policies, and adapter semantics remain in BunkFy.
