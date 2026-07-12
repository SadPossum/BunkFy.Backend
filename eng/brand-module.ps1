param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name
)

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-GmaRepositoryRoot
$moduleRoot = Join-Path $repositoryRoot "src\Modules\$Name"
$solution = Join-Path $repositoryRoot 'BunkFy.slnx'

if (-not (Test-Path -LiteralPath $moduleRoot -PathType Container)) {
    throw "Module '$Name' was not found at '$moduleRoot'."
}

$projectDirectories = @(Get-ChildItem -LiteralPath $moduleRoot -Directory |
    Where-Object { $_.Name.StartsWith("$Name.", [System.StringComparison]::Ordinal) })
$testsRoot = Join-Path $moduleRoot 'tests'
if (Test-Path -LiteralPath $testsRoot -PathType Container) {
    $projectDirectories += Get-ChildItem -LiteralPath $testsRoot -Directory |
        Where-Object { $_.Name.StartsWith("$Name.", [System.StringComparison]::Ordinal) }
}

if ($projectDirectories.Count -eq 0) {
    throw "Module '$Name' has no unbranded project directories to migrate."
}

$oldProjects = foreach ($directory in $projectDirectories) {
    $project = Join-Path $directory.FullName "$($directory.Name).csproj"
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "Expected project file was not found at '$project'."
    }

    $project
}

$removeArguments = @('sln', $solution, 'remove') + @($oldProjects)
Invoke-GmaDotNet -Arguments $removeArguments

foreach ($directory in $projectDirectories) {
    $oldName = $directory.Name
    $newName = "BunkFy.Modules.$oldName"
    $newDirectory = Join-Path $directory.Parent.FullName $newName
    if (Test-Path -LiteralPath $newDirectory) {
        throw "Branded project directory already exists at '$newDirectory'."
    }

    Move-Item -LiteralPath (Join-Path $directory.FullName "$oldName.csproj") `
        -Destination (Join-Path $directory.FullName "$newName.csproj")
    Move-Item -LiteralPath $directory.FullName -Destination $newDirectory
}

$prefixes = @(
    "$Name.Persistence.PostgreSqlMigrations",
    "$Name.Persistence.SqlServerMigrations",
    "$Name.Admin.Contracts",
    "$Name.Application",
    "$Name.Persistence",
    "$Name.Contracts",
    "$Name.Domain",
    "$Name.AdminApi",
    "$Name.AdminCli",
    "$Name.Tests",
    "$Name.Api"
)
$extensions = @('.cs', '.csproj', '.md')
$encoding = [System.Text.UTF8Encoding]::new($false)
$files = Get-ChildItem -LiteralPath $moduleRoot -Recurse -File |
    Where-Object { $extensions -contains $_.Extension }

foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $updated = $content
    foreach ($prefix in $prefixes) {
        $pattern = '(?<!BunkFy\.Modules\.)\b' + [regex]::Escape($prefix)
        $updated = [regex]::Replace($updated, $pattern, "BunkFy.Modules.$prefix")
    }

    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($file.FullName, $updated, $encoding)
    }
}

& (Join-Path $PSScriptRoot 'update-solutions.ps1') -IncludeRootWorkspace
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Branded module '$Name' as BunkFy.Modules.$Name.*."
