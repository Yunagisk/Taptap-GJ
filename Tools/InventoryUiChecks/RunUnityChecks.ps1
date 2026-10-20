param([string]$UnityPath = '', [string]$ExistingRunRoot = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$UnityPath) {
    $version = (Get-Content (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
$runRoot = if ($ExistingRunRoot) { (Resolve-Path -LiteralPath $ExistingRunRoot).Path } else { Join-Path $repo ('output/inventory-category/native-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
if (!$runRoot.StartsWith((Join-Path $repo 'output/inventory-category/'), [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Validation directory must stay under output/inventory-category.' }
$scratch = Join-Path $runRoot 'UnityProject'
python (Join-Path $PSScriptRoot 'stage.py') $scratch
if ($LASTEXITCODE -ne 0) { throw 'Could not stage the isolated project.' }
$log = Join-Path $runRoot 'unity.log'
foreach ($artifact in @('native-result.json', 'native-failure.txt')) {
    Remove-Item -LiteralPath (Join-Path $runRoot $artifact) -Force -ErrorAction SilentlyContinue
}
$arguments = @('-batchmode', '-projectPath', ('"' + $scratch + '"'), '-executeMethod', 'InventoryUiBatchChecks.Run',
    '-logFile', ('"' + $log + '"'), '-screen-width', '1600', '-screen-height', '900', '-job-worker-count', '2')
Write-Output "Running native checks in $scratch"
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
@{ processId = $process.Id; project = $scratch } | ConvertTo-Json | Set-Content (Join-Path $runRoot 'process.json') -Encoding UTF8
$deadline = (Get-Date).AddMinutes(3)
while (!$process.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2; $process.Refresh() }
if (!$process.HasExited) { $process.Kill(); throw "Native checks timed out: $log" }
$result = Join-Path $runRoot 'native-result.json'
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)) {
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 45 }
    throw "Native checks failed: $log"
}
Get-Content -LiteralPath $result
Write-Output "Native UI previews: $runRoot"
