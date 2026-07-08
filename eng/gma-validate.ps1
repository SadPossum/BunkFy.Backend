param(
    [switch] $SkipRestore,
    [switch] $SkipBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

$solutionPath = Join-GmaPath 'BunkFy.slnx'
if (-not $SkipRestore) {
    Invoke-GmaDotNet -Arguments @('restore', $solutionPath)
}

if (-not $SkipBuild) {
    Invoke-GmaDotNet -Arguments @('build', $solutionPath, '--no-restore', '-m:1')
}
