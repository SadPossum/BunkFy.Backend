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
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldUrls = $env:ASPNETCORE_URLS
$exportEnvironment = @{
    'Notifications__Delivery__Enabled' = $env:Notifications__Delivery__Enabled
    'Notifications__Retention__Enabled' = $env:Notifications__Retention__Enabled
    'Auth__Retention__Enabled' = $env:Auth__Retention__Enabled
    'Organizations__Retention__Enabled' = $env:Organizations__Retention__Enabled
    'MessageJournalCleanup__Enabled' = $env:MessageJournalCleanup__Enabled
    'NatsJetStream__Enabled' = $env:NatsJetStream__Enabled
    'NatsConsumers__Enabled' = $env:NatsConsumers__Enabled
}
$process = $null

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $url
    $env:Notifications__Delivery__Enabled = 'false'
    $env:Notifications__Retention__Enabled = 'false'
    $env:Auth__Retention__Enabled = 'false'
    $env:Organizations__Retention__Enabled = 'false'
    $env:MessageJournalCleanup__Enabled = 'false'
    $env:NatsJetStream__Enabled = 'false'
    $env:NatsConsumers__Enabled = 'false'
    $process = Start-Process `
        -FilePath $dotnet `
        -ArgumentList @($assembly, '--urls', $url) `
        -WorkingDirectory (Split-Path -Parent $project) `
        -WindowStyle Hidden `
        -PassThru

    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $response = $null
    $lastFailure = $null
    do {
        if ($process.HasExited) {
            throw "API process exited with code $($process.ExitCode) before OpenAPI became available."
        }

        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $swaggerUrl -TimeoutSec 2
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
        throw "OpenAPI endpoint '$swaggerUrl' did not become ready within 30 seconds. $failureContext"
    }

    $expected = $response.Content.TrimEnd() + "`n"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }

    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    $env:ASPNETCORE_URLS = $oldUrls
    foreach ($entry in $exportEnvironment.GetEnumerator()) {
        [System.Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
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
