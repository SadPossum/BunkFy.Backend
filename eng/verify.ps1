param(
    [switch] $SkipRestore,
    [switch] $SkipBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

& (Join-Path $PSScriptRoot 'gma-validate.ps1') -SkipRestore:$SkipRestore -SkipBuild:$SkipBuild
