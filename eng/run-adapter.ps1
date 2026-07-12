param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$projectPath = Join-GmaPath 'src\BunkFy.AdapterHost\BunkFy.AdapterHost.csproj'
$projectDirectory = Split-Path -Parent $projectPath

$previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
if ([string]::IsNullOrWhiteSpace($previousAspNetCoreEnvironment)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
}

try {
    Invoke-GmaDotNet -Arguments (@(
        'run',
        '--project',
        $projectPath
    ) + ($DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) -WorkingDirectory $projectDirectory
}
finally {
    if ([string]::IsNullOrWhiteSpace($previousAspNetCoreEnvironment)) {
        Remove-Item Env:\ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    }
}
