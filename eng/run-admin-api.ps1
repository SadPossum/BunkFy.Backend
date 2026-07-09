param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$projectPath = Join-GmaPath 'src\BunkFy.Host.AdminApi\BunkFy.Host.AdminApi.csproj'
$projectDirectory = Split-Path -Parent $projectPath

Invoke-GmaDotNet -Arguments (@(
    'run',
    '--project',
    $projectPath,
    '--launch-profile',
    'BunkFy.Host.AdminApi'
) + ($DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) -WorkingDirectory $projectDirectory
