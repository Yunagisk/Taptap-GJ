$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$runRoot = Join-Path $repo 'output/main-menu-opening'
$project = Join-Path $runRoot 'UnityProject'
python (Join-Path $PSScriptRoot 'stage.py')
if ($LASTEXITCODE -ne 0) { throw 'Could not stage main menu checks.' }
$result = Join-Path $runRoot 'result.json'
if (Test-Path -LiteralPath $result) {
    Move-Item -LiteralPath $result -Destination (Join-Path $runRoot ('previous-result-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json'))
}
$version = ((Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt')) | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
$unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
$arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'MainMenuOpeningValidation.Run',
    '-logFile', ('"' + (Join-Path $runRoot 'unity.log') + '"'),
    '-screen-width', '1920', '-screen-height', '1080', '-job-worker-count', '2')
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $project -WindowStyle Hidden -PassThru
$process.Id | Set-Content -LiteralPath (Join-Path $runRoot 'process-id.txt')
$deadline = (Get-Date).AddMinutes(5)
while (!$process.WaitForExit(1000)) {
    if ((Get-Date) -gt $deadline) { Stop-Process -Id $process.Id; throw 'Main menu checks timed out.' }
}
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)) {
    Get-Content -LiteralPath (Join-Path $runRoot 'unity.log') -Tail 25 -Encoding UTF8
    if (Test-Path -LiteralPath $result) { Get-Content -LiteralPath $result -Encoding UTF8 }
    throw 'Main menu checks failed.'
}
$report = Get-Content -LiteralPath $result -Raw -Encoding UTF8 | ConvertFrom-Json
[PSCustomObject]@{ passed = $report.passed; checks = $report.checks.Count; output = $runRoot } | ConvertTo-Json
