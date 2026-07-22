param(
    [switch] $NoBuild,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$solutionPath = Join-GmaPath 'BunkFy.slnx'
[xml] $solution = Get-Content -LiteralPath $solutionPath -Raw
$testProjects = @(
    $solution.SelectNodes('/Solution//Project[@Path]') |
        ForEach-Object {
            $projectPath = Join-GmaPath $_.Path
            [xml] $project = Get-Content -LiteralPath $projectPath -Raw
            $isTestProject = $project.SelectSingleNode(
                '/Project/PropertyGroup/IsTestProject[text()="true"]')
            if ($null -ne $isTestProject) {
                $projectPath
            }
        }
)

if ($testProjects.Count -eq 0) {
    throw "No test projects were found in '$solutionPath'."
}

foreach ($testProject in $testProjects) {
    $arguments = @(
        'test',
        $testProject,
        '--filter',
        'Category!=Docker',
        '--logger',
        'console;verbosity=minimal'
    )

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    $arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    Invoke-GmaDotNet -Arguments $arguments
}
