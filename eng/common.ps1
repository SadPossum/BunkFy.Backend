Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

function Get-GmaRepositoryRoot {
    return $script:RepositoryRoot
}

function Join-GmaPath {
    param([Parameter(Mandatory = $true)][string] $Path)
    return Join-Path $script:RepositoryRoot $Path
}

function Resolve-GmaDotNet {
    if (-not [string]::IsNullOrWhiteSpace($env:GMA_DOTNET)) {
        return $env:GMA_DOTNET
    }

    return 'dotnet'
}

function Invoke-GmaDotNet {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [string] $WorkingDirectory = $script:RepositoryRoot
    )

    Push-Location -LiteralPath $WorkingDirectory
    try {
        & (Resolve-GmaDotNet) @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
