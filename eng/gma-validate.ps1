param(
    [switch] $SkipRestore,
    [switch] $SkipBuild,
    [switch] $SkipTests
)

. (Join-Path $PSScriptRoot 'common.ps1')

$solutionPath = Join-GmaPath 'BunkFy.slnx'
if (-not $SkipRestore) {
    Invoke-GmaDotNet -Arguments @('restore', $solutionPath)
}

if (-not $SkipBuild) {
    Invoke-GmaDotNet -Arguments @('build', $solutionPath, '--no-restore', '-m:1')
}

if (-not $SkipTests) {
    Invoke-GmaDotNet -Arguments @('test', $solutionPath, '--no-build', '--filter', 'Category!=Docker', '--logger', 'console;verbosity=minimal')
}
