param([string]$UnityPath = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$UnityPath) {
    $version = (Get-Content (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity was not found: $UnityPath" }
$runRoot = Join-Path $repo 'output/village-ambience'
$scratch = Join-Path $runRoot 'UnityProject'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
# Retain the real scene, scripts, UI, fonts and data. Combat sprite sheets are unrelated.
$excluded = @('Assets/Sprite/Characters', 'Assets/Sprite/Enemies') | ForEach-Object { Join-Path $repo $_ }
& robocopy (Join-Path $repo 'Assets') (Join-Path $scratch 'Assets') /E /XD $excluded /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -gt 7) { throw "Asset copy failed: $LASTEXITCODE" }
& robocopy (Join-Path $repo 'ProjectSettings') (Join-Path $scratch 'ProjectSettings') /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -gt 7) { throw "Project settings copy failed: $LASTEXITCODE" }
$packages = Join-Path $scratch 'Packages'
New-Item -ItemType Directory -Path $packages -Force | Out-Null
$original = Get-Content (Join-Path $repo 'Packages/manifest.json') -Raw | ConvertFrom-Json
$dependencies = @{}
foreach ($entry in $original.dependencies.PSObject.Properties) {
    if ($entry.Name.StartsWith('com.unity.modules.')) { $dependencies[$entry.Name] = $entry.Value }
}
foreach ($entry in @{
    'com.unity.textmeshpro' = '3.0.7'; 'com.unity.ugui' = '1.0.0';
    'com.unity.test-framework' = '1.1.33'; 'com.unity.ext.nunit' = '1.0.6'
}.GetEnumerator()) {
    $source = Join-Path $repo ('Library/PackageCache/' + $entry.Key + '@' + $entry.Value)
    & robocopy $source (Join-Path $packages $entry.Key) /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) { throw "Package copy failed: $LASTEXITCODE" }
    $dependencies[$entry.Key] = $entry.Value
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $packages 'manifest.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VillageAmbiencePlayModeChecks.cs') -Destination (Join-Path $scratch 'Assets/VillageAmbiencePlayModeChecks.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VillageAmbienceBatchChecks.cs') -Destination (Join-Path $scratch 'Assets/Editor/VillageAmbienceBatchChecks.cs') -Force
$log = Join-Path $runRoot 'unity.log'
$arguments = @('-batchmode', '-projectPath', ('"' + $scratch + '"'), '-executeMethod', 'VillageAmbienceBatchChecks.Run',
    '-logFile', ('"' + $log + '"'), '-screen-width', '1920', '-screen-height', '1080')
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
Write-Output "Unity process: $($process.Id)"
Write-Output "Results, screenshots and log: $runRoot"
$deadline = (Get-Date).AddMinutes(3)
while (!$process.WaitForExit(1000)) {
    if ((Get-Date) -gt $deadline) { $process.Kill(); throw "Unity validation timed out. See $log" }
}
if ($process.ExitCode -ne 0) { throw "Unity checks exited with code $($process.ExitCode). See $log" }
Get-Content -LiteralPath (Join-Path $runRoot 'result.json')
