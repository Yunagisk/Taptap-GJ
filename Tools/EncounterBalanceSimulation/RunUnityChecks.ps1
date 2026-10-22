$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$runRoot = Join-Path $repo 'output/encounter-balance-v1/native'
$scratch = Join-Path $runRoot 'UnityProject'
python (Join-Path $PSScriptRoot 'stage.py') $scratch
if ($LASTEXITCODE -ne 0) { throw 'Could not stage native encounter checks.' }
$resultPath = Join-Path $runRoot 'native-result.json'
if (Test-Path -LiteralPath $resultPath) {
    Move-Item -LiteralPath $resultPath -Destination (Join-Path $runRoot ('previous-result-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json'))
}
$version = ((Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt')) | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
$unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
$arguments = @('-batchmode', '-projectPath', ('"' + $scratch + '"'), '-executeMethod', 'SmallMonsterValidation.Run',
    '-logFile', ('"' + (Join-Path $runRoot 'unity.log') + '"'), '-monsterResult', ('"' + $resultPath + '"'),
    '-screen-width', '1600', '-screen-height', '900', '-job-worker-count', '2')
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
@{ processId = $process.Id; project = $scratch } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'process.json') -Encoding UTF8
Write-Output ('Unity validation process ' + $process.Id)
$deadline = (Get-Date).AddMinutes(4)
while (!$process.WaitForExit(1000)) {
    if ((Get-Date) -gt $deadline) { Stop-Process -Id $process.Id; throw 'Native encounter checks timed out.' }
}
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $resultPath)) {
    Get-Content -LiteralPath (Join-Path $runRoot 'unity.log') -Tail 25 -Encoding UTF8
    if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Encoding UTF8 }
    throw 'Native encounter checks failed.'
}
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
[PSCustomObject]@{ passed = $result.passed; checks = $result.checks.Count; completedRounds = $result.completedRounds; output = $runRoot } | ConvertTo-Json
