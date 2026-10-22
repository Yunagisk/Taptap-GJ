param([string]$UnityPath = '', [string]$ExistingRunRoot = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$UnityPath) {
    $version = (Get-Content (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
$runRoot = if ($ExistingRunRoot) { (Resolve-Path -LiteralPath $ExistingRunRoot).Path } else {
    Join-Path $repo ('output/dialogue-ui/native-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
if (!$runRoot.StartsWith((Join-Path $repo 'output/dialogue-ui/'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation directory must stay under output/dialogue-ui.'
}
$scratch = Join-Path $runRoot 'UnityProject'
python (Join-Path $PSScriptRoot 'stage.py') $scratch
if ($LASTEXITCODE -ne 0) { throw 'Could not stage the dialogue project.' }
$log = Join-Path $runRoot 'unity.log'
foreach ($name in @('result.json', 'failure.txt')) {
    Remove-Item -LiteralPath (Join-Path $runRoot $name) -Force -ErrorAction SilentlyContinue
}
$arguments = @('-batchmode', '-projectPath', ('"' + $scratch + '"'),
    '-executeMethod', 'DialogueUiBatchChecks.Run', '-logFile', ('"' + $log + '"'),
    '-screen-width', '1920', '-screen-height', '1080', '-job-worker-count', '2')
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
@{ processId = $process.Id; project = $scratch } | ConvertTo-Json | Set-Content (Join-Path $runRoot 'process.json') -Encoding UTF8
Write-Output "Native dialogue checks: $runRoot (process $($process.Id))"
$deadline = (Get-Date).AddMinutes(4)
while (!$process.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1; $process.Refresh() }
if (!$process.HasExited) { $process.Kill(); throw "Dialogue checks timed out: $log" }
$result = Join-Path $runRoot 'result.json'
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)) {
    Get-Content -LiteralPath $log -Tail 40
    throw "Native dialogue checks failed: $log"
}
Get-Content -LiteralPath $result
