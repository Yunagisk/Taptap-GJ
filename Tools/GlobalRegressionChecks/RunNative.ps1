param([string[]]$Only = @(), [ValidatePattern('^[a-z0-9][a-z0-9-]*$')][string]$RunName = 'global-fixes-20261021')
$ErrorActionPreference = 'Stop'
if ($Only.Count -eq 1 -and $Only[0].Contains(',')) { $Only = $Only[0].Split(',') }
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$runRoot = Join-Path $repo ('output/village-intro/' + $RunName)
$scratch = Join-Path $runRoot 'UnityProject'
$unity = Join-Path $env:ProgramFiles 'Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe'
$suites = @(
    @('save-load', 'DayStartSaveValidation.Run', '', 'save-load/result.json'),
    @('save-contract', 'DayStartSaveBatchChecks.Run', '', 'result.json'),
    @('weapon-catalog', 'WeaponCatalogBatchChecks.Run', '', 'catalogue-result.json'),
    @('tavern', 'TavernBatchChecks.Run', '', 'result.json'),
    @('opening', 'MainMenuOpeningValidation.Run', '', 'result.json'),
    @('intro', 'VillageIntroBatchChecks.Run', '', 'result.json'),
    @('chapter', 'SlothChapterBatchChecks.Run', '', 'sloth-chapter/result.json'),
    @('sidequests', 'SidequestBatchChecks.Run', '', 'sidequests/result.json'),
    @('inventory', 'InventoryUiBatchChecks.Run', '', 'native-result.json'),
    @('services', 'VillageServiceBatchChecks.Run', '', 'result.json'),
    @('ambience', 'VillageAmbienceBatchChecks.Run', '', 'result.json'),
    @('dialogue', 'DialogueUiBatchChecks.Run', '', 'result.json'),
    @('encounter', 'SmallMonsterValidation.Run', '-monsterResult', 'native-result.json'),
    @('targeting', 'BattleTargetingValidation.Run', '-targetingResult', 'native-result.json'),
    @('map-menu', 'MapMenuValidation.Run', '-menuResult', 'native-result.json'),
    @('fast-travel', 'MapFastTravelValidation.Run', '-travelResult', 'native-result.json'),
    @('boss-camera', 'MapBossCameraValidation.Run', '-bossCameraResult', 'native-result.json'),
    @('attack-motion', 'EnemyAttackMotionValidation.Run', '-attackResult', 'native-result.json'),
    @('hurt-ash', 'EnemyHurtAshValidation.Run', '-deathResult', 'native-result.json'),
    @('sloth-animation', 'SlothValidation.Run', '-slothResult', 'native-result.json'),
    @('global-visual', 'GlobalVisualAudit.Run', '', 'global-visual/result.json'),
    @('battle-resize', 'BattleResizeAudit.Run', '', 'battle-resize/result.json'),
    @('full-run', 'FullRunBatchChecks.Run', '', 'full-run/result.json')
)
$resultRoot = Join-Path $repo ('output/' + $RunName)
$summary = @()
if ($Only.Count -gt 0 -and (Test-Path -LiteralPath (Join-Path $resultRoot 'native-summary.json'))) {
    $previous = Get-Content -LiteralPath (Join-Path $resultRoot 'native-summary.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $previous) {
        if ($entry.suite -and $entry.suite -notin $Only) { $summary += $entry }
    }
}
foreach ($suite in $suites) {
    if ($Only.Count -gt 0 -and $suite[0] -notin $Only) { continue }
    $folder = Join-Path $runRoot $suite[0]
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $log = Join-Path $folder 'unity.log'
    $result = if ($suite[2]) { Join-Path $folder 'native-result.json' } else { Join-Path $runRoot $suite[3] }
    $args = @('-batchmode','-projectPath',('"'+$scratch+'"'),'-executeMethod',$suite[1],'-logFile',('"'+$log+'"'),'-screen-width','1920','-screen-height','1080','-job-worker-count','2')
    if ($suite[2]) { $args += @($suite[2],('"'+$result+'"')) }
    $start = Get-Date
    $env:TAPTAP_GJ_QA_SAVE_PATH = Join-Path $folder ('save-' + [guid]::NewGuid().ToString('N') + '/day-start.json')
    $process = Start-Process -FilePath $unity -ArgumentList $args -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
    Write-Output ($suite[0] + ' started, PID ' + $process.Id)
    $deadline = (Get-Date).AddMinutes($(if ($suite[0] -eq 'full-run') { 10 } else { 6 }))
    while (!$process.WaitForExit(1000)) {
        if ((Get-Date) -gt $deadline) { $process.Kill(); break }
    }
    $process.Refresh()
    $ok = $false
    $checks = 0
    $errorText = ''
    if ((Test-Path -LiteralPath $result) -and (Get-Item -LiteralPath $result).LastWriteTime -gt $start) {
        $data = Get-Content -LiteralPath $result -Raw -Encoding UTF8 | ConvertFrom-Json
        $ok = $data.passed -eq $true -and $process.ExitCode -eq 0
        $checks = if ($data.checks -is [array]) { $data.checks.Count } else { $data.checks }
        Copy-Item -LiteralPath $result -Destination (Join-Path $resultRoot ($suite[0] + '-result.json'))
        $errorText = if ($data.error) { $data.error } else { @($data.errors) -join "`n" }
    }
    if (!$ok -and !$errorText -and (Test-Path -LiteralPath $log)) {
        $errorText = @(Select-String -LiteralPath $log -Pattern 'Exception:|FAILED:|error CS\d+' |
            Select-Object -First 5 | ForEach-Object { $_.Line }) -join "`n"
    }
    $entry = [PSCustomObject]@{suite=$suite[0];passed=$ok;exitCode=$process.ExitCode;checks=$checks;seconds=[int]((Get-Date)-$start).TotalSeconds;log=$log;error=$errorText}
    $summary += $entry
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resultRoot 'native-summary.json') -Encoding UTF8
    $entry | ConvertTo-Json -Compress
}
if (@($summary | Where-Object { !$_.passed }).Count -gt 0) { exit 1 }
