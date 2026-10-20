param([string]$OutputDirectory = 'output/battle-targeting')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$runRoot = Join-Path $repo $OutputDirectory
$scratch = Join-Path $repo 'output/encounter-balance-v1/targeting/UnityProject'
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
python (Join-Path $repo 'Tools/EncounterBalanceSimulation/stage.py') $scratch
if ($LASTEXITCODE -ne 0) { throw 'Could not stage targeting validation.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityValidation.cs') -Destination (Join-Path $scratch 'Assets/Editor/BattleTargetingValidation.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PlayerStatusNativeChecks.cs') -Destination (Join-Path $scratch 'Assets/Editor/PlayerStatusNativeChecks.cs')
$resultPath = Join-Path $runRoot 'native-result.json'
if (Test-Path -LiteralPath $resultPath) {
    Move-Item -LiteralPath $resultPath -Destination (Join-Path $runRoot ('previous-result-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json'))
}
$version = ((Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt')) | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
$unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
$arguments = @('-batchmode', '-projectPath', ('"' + $scratch + '"'), '-executeMethod', 'BattleTargetingValidation.Run',
    '-logFile', ('"' + (Join-Path $runRoot 'unity.log') + '"'), '-targetingResult', ('"' + $resultPath + '"'),
    '-screen-width', '1600', '-screen-height', '900', '-job-worker-count', '2')
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
@{ processId = $process.Id; project = $scratch } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'process.json') -Encoding UTF8
Write-Output ('Unity targeting validation process ' + $process.Id)
$deadline = (Get-Date).AddMinutes(4)
while (!$process.WaitForExit(1000)) {
    if ((Get-Date) -gt $deadline) { Stop-Process -Id $process.Id; throw 'Native targeting validation timed out.' }
}
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $resultPath)) {
    Get-Content -LiteralPath (Join-Path $runRoot 'unity.log') -Tail 25 -Encoding UTF8
    if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Encoding UTF8 }
    throw 'Native targeting validation failed.'
}
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
[PSCustomObject]@{ passed = $result.passed; checks = $result.checks.Count; output = $runRoot } | ConvertTo-Json
