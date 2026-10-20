$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$runRoot = Join-Path $repo 'output/village-intro/tavern-checks'
$scratch = Join-Path $runRoot 'UnityProject'
python (Join-Path $PSScriptRoot 'stage.py')
if ($LASTEXITCODE -ne 0) { throw 'Could not stage tavern checks.' }
$version = ((Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt')) | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
$unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
$arguments = @('-batchmode','-projectPath',('"'+$scratch+'"'),'-executeMethod','TavernBatchChecks.Run',
    '-logFile',('"'+(Join-Path $runRoot 'unity.log')+'"'),'-screen-width','1920','-screen-height','1080','-job-worker-count','2')
$started = Get-Date
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
Write-Output ('Tavern Unity process: '+$process.Id)
$deadline = (Get-Date).AddMinutes(6)
while (!$process.WaitForExit(1000)) {
    if ((Get-Date) -gt $deadline) { Stop-Process -Id $process.Id; throw 'Tavern checks timed out.' }
}
$resultPath = Join-Path $runRoot 'result.json'
if (!(Test-Path -LiteralPath $resultPath) -or (Get-Item -LiteralPath $resultPath).LastWriteTime -lt $started) {
    if (Test-Path -LiteralPath (Join-Path $runRoot 'unity.log')) {
        Get-Content -LiteralPath (Join-Path $runRoot 'unity.log') -Tail 30
    }
    throw 'Tavern checks produced no fresh result.'
}
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Output ("Tavern checks: " + $result.checks.Count + "; passed=" + $result.passed)
if ($process.ExitCode -ne 0 -or !$result.passed) {
    Write-Output ($result.errors -join "`n")
    throw 'Tavern validation failed.'
}
