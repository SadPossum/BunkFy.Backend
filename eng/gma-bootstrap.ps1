[CmdletBinding(SupportsShouldProcess = $true)]
param([switch] $Force)

. (Join-Path $PSScriptRoot 'common.ps1')

function Write-GmaSourceRootsFile {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string[]] $Lines,
        [Parameter(Mandatory = $true)][string] $Description
    )

    if (Test-Path -LiteralPath $Path) {
        if (-not $Force) {
            $existingLines = [System.IO.File]::ReadAllLines($Path)
            if ([string]::Join("`n", $existingLines) -eq [string]::Join("`n", $Lines)) {
                Write-Host "$Description already exists. Use -Force to refresh it: $Path"
                return
            }

            throw "$Description already exists with different contents. Use -Force to refresh it: $Path"
        }
    }

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Cannot write $Description because '$directory' does not exist. Initialize submodules or local source mounts first."
    }

    $action = if (Test-Path -LiteralPath $Path) { 'Overwrite' } else { 'Create' }
    if ($PSCmdlet.ShouldProcess($Path, "$action $Description")) {
        [System.IO.File]::WriteAllLines($Path, $Lines, [System.Text.UTF8Encoding]::new($false))
        Write-Host "$action ${Description}: $Path"
    }
}

$selectedModuleAliases = @('administration', 'auth', 'files', 'notifications', 'task-runtime', 'tenancy')
$moduleRootProperties = @{
    'administration' = 'GmaModuleAdministrationRoot'
    'auth' = 'GmaModuleAuthRoot'
    'files' = 'GmaModuleFilesRoot'
    'notifications' = 'GmaModuleNotificationsRoot'
    'task-runtime' = 'GmaModuleTaskRuntimeRoot'
    'tenancy' = 'GmaModuleTenancyRoot'
}

$rootLinesBuilder = New-Object 'System.Collections.Generic.List[string]'
$rootLinesBuilder.Add('<Project>')
$rootLinesBuilder.Add('  <PropertyGroup>')
$rootLinesBuilder.Add('    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)gma\framework\src\</GmaFrameworkRoot>')
$rootLinesBuilder.Add('    <GmaModulesRoot>$(MSBuildThisFileDirectory)gma\modules\</GmaModulesRoot>')
foreach ($moduleAlias in $selectedModuleAliases) {
    $propertyName = $moduleRootProperties[$moduleAlias]
    $rootLinesBuilder.Add("    <$propertyName>`$(GmaModulesRoot)$moduleAlias\src\</$propertyName>")
}
$rootLinesBuilder.Add('    <GmaModuleCatalogRoot>$(MSBuildThisFileDirectory)src\Modules\Catalog\</GmaModuleCatalogRoot>')
$rootLinesBuilder.Add('    <GmaModuleOrderingRoot>$(MSBuildThisFileDirectory)src\Modules\Ordering\</GmaModuleOrderingRoot>')
$rootLinesBuilder.Add('    <GmaModuleTaskSamplesRoot>$(MSBuildThisFileDirectory)src\Modules\TaskSamples\</GmaModuleTaskSamplesRoot>')
$rootLinesBuilder.Add('  </PropertyGroup>')
$rootLinesBuilder.Add('</Project>')
$rootLines = $rootLinesBuilder.ToArray()

$frameworkLines = @(
    '<Project>',
    '  <PropertyGroup>',
    '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)src\</GmaFrameworkRoot>',
    '  </PropertyGroup>',
    '</Project>'
)

$moduleLinesBuilder = New-Object 'System.Collections.Generic.List[string]'
$moduleLinesBuilder.Add('<Project>')
$moduleLinesBuilder.Add('  <PropertyGroup>')
$moduleLinesBuilder.Add('    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)..\..\framework\src\</GmaFrameworkRoot>')
$moduleLinesBuilder.Add('    <GmaModulesRoot>$(MSBuildThisFileDirectory)..\</GmaModulesRoot>')
foreach ($moduleAlias in $selectedModuleAliases) {
    $propertyName = $moduleRootProperties[$moduleAlias]
    $moduleLinesBuilder.Add("    <$propertyName>`$(GmaModulesRoot)$moduleAlias\src\</$propertyName>")
}
$moduleLinesBuilder.Add('  </PropertyGroup>')
$moduleLinesBuilder.Add('</Project>')
$moduleLines = $moduleLinesBuilder.ToArray()

Write-GmaSourceRootsFile -Path (Join-GmaPath 'Gma.SourceRoots.props') -Lines $rootLines -Description 'root source-root configuration'
Write-GmaSourceRootsFile -Path (Join-GmaPath 'gma\framework\Gma.SourceRoots.props') -Lines $frameworkLines -Description 'framework source-root configuration'

foreach ($moduleAlias in $selectedModuleAliases) {
    $moduleRoot = Join-GmaPath "gma\modules\$moduleAlias"
    if (Test-Path -LiteralPath $moduleRoot -PathType Container) {
        Write-GmaSourceRootsFile -Path (Join-Path $moduleRoot 'Gma.SourceRoots.props') -Lines $moduleLines -Description "$moduleAlias module source-root configuration"
    }
}
