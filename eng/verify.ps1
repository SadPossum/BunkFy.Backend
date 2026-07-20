param(
    [switch] $SkipRestore,
    [switch] $SkipBuild,
    [switch] $SkipTests,
    [switch] $SkipMigrationCheck
)

. (Join-Path $PSScriptRoot 'common.ps1')

& (Join-Path $PSScriptRoot 'update-solutions.ps1') -Check

& (Join-Path $PSScriptRoot 'check-source-packages.ps1') -SkipRestore -SkipBuild

if (-not $SkipRestore) {
    & (Join-Path $PSScriptRoot 'restore.ps1')
}

if (-not $SkipBuild) {
    Invoke-GmaDotNet -Arguments @('build', (Join-GmaPath 'BunkFy.slnx'), '--no-restore', '-m:1')
}

if (-not $SkipMigrationCheck) {
    & (Join-Path $PSScriptRoot 'check-migrations.ps1') -NoBuild
}

if (-not $SkipTests) {
    & (Join-Path $PSScriptRoot 'test-fast.ps1') -NoBuild
}
