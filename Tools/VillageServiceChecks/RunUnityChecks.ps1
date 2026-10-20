$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$runRoot = Join-Path $repo 'output/village-services'
$scratch = Join-Path $runRoot 'UnityProject'
python (Join-Path $PSScriptRoot 'stage.py')
if ($LASTEXITCODE -ne 0) { throw 'Could not stage village service checks.' }
foreach ($oldName in @('result.json','failure.txt')) {
    $oldPath = Join-Path $runRoot $oldName
    if (Test-Path -LiteralPath $oldPath) {
        Move-Item -LiteralPath $oldPath -Destination (Join-Path $runRoot ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+$oldName))
    }
}
$version = ((Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt')) | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
$unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
$arguments = @('-batchmode','-projectPath',('"'+$scratch+'"'),'-executeMethod','VillageServiceBatchChecks.Run',
    '-logFile',('"'+(Join-Path $runRoot 'unity.log')+'"'),'-screen-width','1920','-screen-height','1080','-job-worker-count','2')
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
Write-Output ('Village service Unity process: '+$process.Id)
$deadline = (Get-Date).AddMinutes(6)
while (!$process.WaitForExit(1000)) {
    if ((Get-Date) -gt $deadline) { Stop-Process -Id $process.Id; throw 'Village service checks timed out.' }
}
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $runRoot 'result.json'))) {
    if (Test-Path -LiteralPath (Join-Path $runRoot 'unity.log')) {
        Select-String -LiteralPath (Join-Path $runRoot 'unity.log') -Pattern 'error CS|FAILED:|Exception:' | Select-Object -Last 12
    }
    if (Test-Path -LiteralPath (Join-Path $runRoot 'failure.txt')) { Get-Content -LiteralPath (Join-Path $runRoot 'failure.txt') -Encoding UTF8 }
    throw 'Village service validation failed.'
}
Get-Content -LiteralPath (Join-Path $runRoot 'result.json') -Encoding UTF8
