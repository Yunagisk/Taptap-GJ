$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
python (Join-Path $PSScriptRoot 'stage.py')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$scratch = Join-Path $repo 'output/village-intro/day-start-save/UnityProject'
$unity = Join-Path $env:ProgramFiles 'Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe'
$log = Join-Path $repo 'output/village-intro/day-start-save/unity.log'
$process = Start-Process -FilePath $unity -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-projectPath', ('"' + $scratch + '"'), '-executeMethod', 'DayStartSaveBatchChecks.Run',
    '-logFile', ('"' + $log + '"'), '-screen-width', '1920', '-screen-height', '1080', '-job-worker-count', '2')
Write-Output ('Save validation started: PID ' + $process.Id)
$process.WaitForExit()
exit $process.ExitCode
