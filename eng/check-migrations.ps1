param(
    [switch] $NoBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

Invoke-GmaDotNet -Arguments @('tool', 'restore')

$repositoryRoot = Get-GmaRepositoryRoot
$migrationProjects = Get-ChildItem -LiteralPath (Join-GmaPath 'src\Modules') -Recurse -Filter *.csproj -File |
    Where-Object {
        $_.BaseName.EndsWith('.Persistence.SqlServerMigrations', [System.StringComparison]::Ordinal) -or
        $_.BaseName.EndsWith('.Persistence.PostgreSqlMigrations', [System.StringComparison]::Ordinal)
    } |
    Sort-Object FullName

if ($migrationProjects.Count -eq 0) {
    throw 'No provider migration projects were found under src\Modules.'
}

foreach ($project in $migrationProjects) {
    $relativeProject = $project.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    Write-Host "Checking migration drift for $relativeProject"

    if (-not $NoBuild) {
        Invoke-GmaDotNet -Arguments @('build', $project.FullName, '--no-restore')
    }

    $arguments = @(
        'tool',
        'run',
        'dotnet-ef',
        'migrations',
        'has-pending-model-changes',
        '--project',
        $project.FullName,
        '--startup-project',
        $project.FullName
    )

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    Invoke-GmaDotNet -Arguments $arguments
}

Write-Host 'Migration drift checks passed.'
