param(
    [switch] $NoBuild,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$previousRequireDockerTests = $env:GMA_REQUIRE_DOCKER_TESTS
$env:GMA_REQUIRE_DOCKER_TESTS = 'true'

try {
    $filter = @(
        'Category=IngestionRuntime',
        'FullyQualifiedName~IngestionOperationsIntegrationTests',
        'FullyQualifiedName~MessageJournalCleanupIntegrationTests',
        'FullyQualifiedName~NatsEventBusIntegrationTests',
        'FullyQualifiedName~TaskRuntimeIntegrationTests',
        'FullyQualifiedName~WorkerHostIntegrationTests',
        'FullyQualifiedName~ImapReservationMailIntegrationTests',
        'FullyQualifiedName~FileStorageIntegrationTests'
    ) -join '|'

    $arguments = @(
        'test',
        (Join-GmaPath 'tests\Integration.Tests\Integration.Tests.csproj'),
        '--filter',
        $filter,
        '--logger',
        'console;verbosity=detailed'
    )

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    $arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    Invoke-GmaDotNet -Arguments $arguments
}
finally {
    $env:GMA_REQUIRE_DOCKER_TESTS = $previousRequireDockerTests
}
