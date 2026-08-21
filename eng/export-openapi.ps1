param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,
    [switch] $Check,
    [switch] $NoBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-GmaRepositoryRoot
$dotnet = Resolve-GmaDotNet
$project = Join-GmaPath 'src\BunkFy.Host.Api\BunkFy.Host.Api.csproj'
$assembly = Join-GmaPath 'src\BunkFy.Host.Api\bin\Debug\net10.0\BunkFy.Host.Api.dll'
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)

if (-not $NoBuild) {
    Invoke-GmaDotNet -Arguments @('build', $project, '--no-restore', '-m:1', '-p:BuildInParallel=false')
}

if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
    throw "API assembly '$assembly' does not exist. Build the API or omit -NoBuild."
}

$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port
$listener.Stop()

$url = "http://127.0.0.1:$port"
$swaggerUrl = "$url/swagger/v1/swagger.json"
$exportEnvironmentNames = @(
    'ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'Notifications__Delivery__Enabled',
    'Notifications__Retention__Enabled',
    'Notifications__DurableStreams__MonitorEnabled',
    'Auth__Retention__Enabled',
    'Organizations__Retention__Enabled',
    'MessageJournalCleanup__Enabled',
    'NatsJetStream__Enabled',
    'NatsConsumers__Enabled'
)
$processEnvironment = [System.Environment]::GetEnvironmentVariables('Process')
$exportEnvironment = @{}
foreach ($name in $exportEnvironmentNames) {
    $wasDefined = $processEnvironment.Contains($name)
    $exportEnvironment[$name] = [pscustomobject]@{
        WasDefined = $wasDefined
        Value = if ($wasDefined) { $processEnvironment[$name] } else { $null }
    }
}
$process = $null

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $url
    $env:Notifications__Delivery__Enabled = 'false'
    $env:Notifications__Retention__Enabled = 'false'
    $env:Notifications__DurableStreams__MonitorEnabled = 'false'
    $env:Auth__Retention__Enabled = 'false'
    $env:Organizations__Retention__Enabled = 'false'
    $env:MessageJournalCleanup__Enabled = 'false'
    $env:NatsJetStream__Enabled = 'false'
    $env:NatsConsumers__Enabled = 'false'
    $startProcess = @{
        FilePath = $dotnet
        ArgumentList = @($assembly, '--urls', $url)
        WorkingDirectory = Split-Path -Parent $project
        PassThru = $true
    }
    $isWindowsHost = $PSVersionTable.PSEdition -eq 'Desktop' -or
        ($PSVersionTable.ContainsKey('Platform') -and $PSVersionTable.Platform -eq 'Win32NT')
    if ($isWindowsHost) {
        $startProcess.WindowStyle = 'Hidden'
    }

    $process = Start-Process @startProcess

    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    $response = $null
    $lastFailure = $null
    do {
        if ($process.HasExited) {
            throw "API process exited with code $($process.ExitCode) before OpenAPI became available."
        }

        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $swaggerUrl -TimeoutSec 15
        }
        catch {
            $lastFailure = $_.Exception.Message
            Start-Sleep -Milliseconds 250
        }
    } while ($null -eq $response -and [DateTime]::UtcNow -lt $deadline)

    if ($null -eq $response) {
        $failureContext = if ([string]::IsNullOrWhiteSpace($lastFailure)) {
            'No HTTP response was received.'
        }
        else {
            "Last request failure: $lastFailure"
        }
        throw "OpenAPI endpoint '$swaggerUrl' did not become ready within 60 seconds. $failureContext"
    }

    $expected = $response.Content.TrimEnd() + "`n"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }

    foreach ($entry in $exportEnvironment.GetEnumerator()) {
        if (-not $entry.Value.WasDefined) {
            Remove-Item -LiteralPath "Env:\$($entry.Key)" -ErrorAction SilentlyContinue
            continue
        }

        [System.Environment]::SetEnvironmentVariable(
            $entry.Key,
            $entry.Value.Value,
            'Process')
    }
}

if ($Check) {
    if (-not (Test-Path -LiteralPath $resolvedOutputPath -PathType Leaf)) {
        throw "OpenAPI snapshot '$resolvedOutputPath' is missing. Run export-openapi.ps1 without -Check."
    }

    $actual = [System.IO.File]::ReadAllText($resolvedOutputPath).Replace("`r`n", "`n")
    if ($actual -ne $expected) {
        throw "OpenAPI snapshot '$resolvedOutputPath' is stale. Regenerate the web API contracts."
    }

    Write-Host "OpenAPI snapshot is current: $resolvedOutputPath"
    return
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedOutputPath, $expected, $utf8WithoutBom)
Write-Host "OpenAPI snapshot updated: $resolvedOutputPath"
