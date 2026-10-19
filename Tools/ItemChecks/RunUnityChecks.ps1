param([string]$UnityPath = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$UnityPath) {
    $version = (Get-Content (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion:').ToString().Split(' ')[1]
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity was not found: $UnityPath" }
$runRoot = Join-Path $repo ('output/ItemValidation/unity-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$scratch = Join-Path $runRoot 'UnityProject'
New-Item -ItemType Directory -Path $scratch | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'Assets') -Destination $scratch -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'ProjectSettings') -Destination $scratch -Recurse
$packages = Join-Path $scratch 'Packages'
New-Item -ItemType Directory -Path $packages | Out-Null
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
    if (!(Test-Path -LiteralPath $source)) { throw "Open the main project once to populate its package cache: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $packages $entry.Key) -Recurse
    $dependencies[$entry.Key] = $entry.Value
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $packages 'manifest.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity/ItemPlayModeChecks.cs') -Destination (Join-Path $scratch 'Assets/ItemPlayModeChecks.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity/ItemBatchChecks.cs') -Destination (Join-Path $scratch 'Assets/Editor/ItemBatchChecks.cs')
$log = Join-Path $runRoot 'unity.log'
$arguments = @('-batchmode', '-projectPath', ('"' + $scratch + '"'), '-executeMethod', 'ItemBatchChecks.Run',
    '-logFile', ('"' + $log + '"'), '-screen-width', '1600', '-screen-height', '900')
Write-Output "Running native Unity checks in $scratch"
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(600000)) { $process.Kill(); throw "Unity validation timed out. See $log" }
$result = Join-Path $runRoot 'native-result.json'
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)) {
    Get-Content -LiteralPath $log -Tail 50
    throw "Unity validation failed. See $log"
}
Get-Content -LiteralPath $result
Write-Output "Rendered previews and log: $runRoot"
