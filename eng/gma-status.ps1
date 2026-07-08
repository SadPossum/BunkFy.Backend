. (Join-Path $PSScriptRoot 'common.ps1')

Write-Host "Repository: $(Get-GmaRepositoryRoot)"
if (Test-Path -LiteralPath (Join-GmaPath 'Gma.SourceRoots.props')) {
    Write-Host 'Source roots: configured'
}
else {
    Write-Host 'Source roots: using checked-in defaults'
}
Write-Host ''
$repositoryRoot = Get-GmaRepositoryRoot
$gitRoot = git -C $repositoryRoot rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -eq 0 -and [string]::Equals([System.IO.Path]::GetFullPath($gitRoot).TrimEnd('\', '/'), [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\', '/'), [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host 'Git status:'
    git -C $repositoryRoot status --short --branch
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
else {
    Write-Host 'Git status: app repository not initialized'
}

if (Test-Path -LiteralPath (Join-GmaPath '.gitmodules')) {
    Write-Host ''
    Write-Host 'Submodules:'
    git -C (Get-GmaRepositoryRoot) submodule status --recursive
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
