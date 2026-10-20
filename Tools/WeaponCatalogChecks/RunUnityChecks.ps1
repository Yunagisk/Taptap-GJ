$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$runRoot = Join-Path $repo 'output/village-intro/weapon-catalog'
$scratch = Join-Path $runRoot 'UnityProject'
python (Join-Path $PSScriptRoot 'stage.py')
if ($LASTEXITCODE -ne 0) { throw 'Could not stage weapon checks.' }
$version = ((Get-Content -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt')) | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
$unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
foreach ($suite in @(@('catalogue','WeaponCatalogBatchChecks.Run','catalogue-result.json'),@('inventory','InventoryUiBatchChecks.Run','native-result.json'))) {
    $log = Join-Path $runRoot ($suite[0] + '.log')
    $arguments = @('-batchmode','-projectPath',('"'+$scratch+'"'),'-executeMethod',$suite[1],
        '-logFile',('"'+$log+'"'),'-screen-width','1920','-screen-height','1080','-job-worker-count','2')
    $started = Get-Date
    $process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
    Write-Output ($suite[0] + ' Unity process: '+$process.Id)
    $deadline = (Get-Date).AddMinutes(6)
    while (!$process.WaitForExit(1000)) {
        if ((Get-Date) -gt $deadline) { Stop-Process -Id $process.Id; throw 'Weapon checks timed out.' }
    }
    $resultPath = Join-Path $runRoot $suite[2]
    if (!(Test-Path -LiteralPath $resultPath) -or (Get-Item -LiteralPath $resultPath).LastWriteTime -lt $started) {
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 30 }
        throw 'Weapon checks produced no fresh result.'
    }
    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $count = if ($result.checks -is [array]) { $result.checks.Count } else { $result.checks }
    Write-Output ($suite[0] + ': ' + $count + ' checks; passed=' + $result.passed)
    if ($process.ExitCode -ne 0 -or !$result.passed -or $result.runtimeErrors -gt 0) {
        Write-Output ($result.errors -join "`n")
        if (Test-Path -LiteralPath (Join-Path $runRoot 'native-failure.txt')) {
            Get-Content -LiteralPath (Join-Path $runRoot 'native-failure.txt')
        }
        throw 'Weapon validation failed.'
    }
}
