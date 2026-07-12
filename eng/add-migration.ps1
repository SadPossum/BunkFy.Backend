param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Module,

    [Parameter(Mandatory = $true)]
    [ValidateSet('SqlServer', 'PostgreSql')]
    [string] $Provider,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string] $Name,

    [string] $Connection,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*DbContext$')]
    [string] $Context
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Assert-GmaPathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description was not found at '$Path'."
    }
}

function Resolve-GmaDbContextName {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PersistenceRoot,

        [Parameter(Mandatory = $true)]
        [string] $ModuleName,

        [string] $RequestedContext
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedContext)) {
        return $RequestedContext
    }

    $contextNames = @()
    $sourceFiles = Get-ChildItem -LiteralPath $PersistenceRoot -Filter *.cs -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\Migrations\\' }

    foreach ($sourceFile in $sourceFiles) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
        $matches = [regex]::Matches($source, '\bclass\s+(?<name>[A-Za-z][A-Za-z0-9_]*DbContext)\b')

        foreach ($match in $matches) {
            $contextNames += $match.Groups['name'].Value
        }
    }

    $contextNames = @($contextNames | Sort-Object -Unique)

    if ($contextNames.Count -eq 1) {
        return $contextNames[0]
    }

    $conventionalName = "${ModuleName}DbContext"
    if ($contextNames -contains $conventionalName) {
        return $conventionalName
    }

    throw "Could not determine DbContext for module '$ModuleName'. Found: $($contextNames -join ', '). Pass -Context explicitly."
}

Invoke-GmaDotNet -Arguments @('tool', 'restore')

$moduleRoot = Join-GmaPath "src\Modules\$Module"
$projectPrefix = "BunkFy.Modules.$Module"
$persistenceRoot = Join-Path $moduleRoot "$projectPrefix.Persistence"
$migrationRoot = Join-Path $moduleRoot "$projectPrefix.Persistence.${Provider}Migrations"
$persistenceProject = Join-Path $persistenceRoot "$projectPrefix.Persistence.csproj"
$migrationProject = Join-Path $migrationRoot "$projectPrefix.Persistence.${Provider}Migrations.csproj"
$startupProject = $migrationProject

Assert-GmaPathExists -Path $moduleRoot -Description "Module '$Module'"
Assert-GmaPathExists -Path $persistenceProject -Description "Persistence project for module '$Module'"
Assert-GmaPathExists -Path $migrationProject -Description "$Provider migration project for module '$Module'"

$contextName = Resolve-GmaDbContextName -PersistenceRoot $persistenceRoot -ModuleName $Module -RequestedContext $Context

Invoke-GmaDotNet -Arguments @('build', $migrationProject, '--no-restore')

$arguments = @(
    'ef',
    'migrations',
    'add',
    $Name,
    '--no-build',
    '--project',
    $migrationProject,
    '--startup-project',
    $startupProject,
    '--context',
    $contextName,
    '--output-dir',
    'Migrations',
    '--',
    '--provider',
    $Provider
)

if (-not [string]::IsNullOrWhiteSpace($Connection)) {
    $arguments += @('--connection', $Connection)
}

Invoke-GmaDotNet -Arguments $arguments
