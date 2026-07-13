param(
    [switch] $SkipRestore,
    [switch] $SkipBuild,
    [switch] $SkipTests,
    [switch] $FocusedSolutions
)

. (Join-Path $PSScriptRoot 'common.ps1')

$solutions = @('BunkFy.slnx')

if ($FocusedSolutions) {
    $solutions += @(
        'gma\framework\Gma.Framework.slnx',
        'gma\extensions\Gma.Extensions.slnx',
        'gma\modules\access-control\Gma.Modules.AccessControl.slnx',
        'gma\modules\administration\Gma.Modules.Administration.slnx',
        'gma\modules\auth\Gma.Modules.Auth.slnx',
        'gma\modules\files\Gma.Modules.Files.slnx',
        'gma\modules\notifications\Gma.Modules.Notifications.slnx',
        'gma\modules\task-runtime\Gma.Modules.TaskRuntime.slnx',
        'gma\modules\tenancy\Gma.Modules.Tenancy.slnx'
    )
}

foreach ($solution in $solutions) {
    $solutionPath = Join-GmaPath $solution

    if (-not $SkipRestore) {
        Invoke-GmaDotNet -Arguments @(
            'restore',
            $solutionPath,
            '--disable-parallel',
            '-m:1',
            '-p:BuildInParallel=false'
        )
    }

    if (-not $SkipBuild) {
        Invoke-GmaDotNet -Arguments @('build', $solutionPath, '--no-restore', '-m:1')
    }

    if (-not $SkipTests) {
        Invoke-GmaDotNet -Arguments @('test', $solutionPath, '--no-build', '--filter', 'Category!=Docker', '--logger', 'console;verbosity=minimal')
    }
}
