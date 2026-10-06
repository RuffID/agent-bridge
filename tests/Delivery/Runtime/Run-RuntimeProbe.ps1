param(
    [Parameter(Mandatory)][string]$DeliveryRoot,
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][string]$Evidence
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (Test-Path -LiteralPath $Workspace) { throw 'Workspace must be new.' }
if (Test-Path -LiteralPath $Evidence) { throw 'Evidence must be new.' }
New-Item -ItemType Directory -Path $Workspace, $Evidence | Out-Null
$receipts = [System.Collections.Generic.List[object]]::new()
$kits = [System.Collections.Generic.List[object]]::new()

# Verify before copying/building/loading; no assembly or native execution here.
function Test-Kit([string]$Root) {
    $manifestPath = Join-Path $Root 'delivery.manifest.json'
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
    foreach ($entry in $manifest.files) {
        $path = [IO.Path]::GetFullPath((Join-Path $Root $entry.path))
        if (!$path.StartsWith([IO.Path]::GetFullPath($Root) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes kit.' }
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing kit file: $($entry.path)" }
        if ((Get-Item -LiteralPath $path).Length -ne $entry.size -or (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash -ne $entry.sha256) { throw "Kit hash mismatch: $($entry.path)" }
    }
    return $manifest
}

function Invoke-Check([string]$Label, [string[]]$Arguments, [bool]$ExpectSuccess = $true) {
    $log = Join-Path $Evidence "$Label.log"
    $started = [DateTimeOffset]::UtcNow
    & dotnet @Arguments *> $log
    $code = $LASTEXITCODE
    $receipts.Add([pscustomobject]@{ label = $Label; executable = 'dotnet'; arguments = $Arguments; cwd = (Get-Location).Path; startedUtc = $started; finishedUtc = [DateTimeOffset]::UtcNow; exitCode = $code; log = $log; sha256 = (Get-FileHash $log).Hash })
    $receipts | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 (Join-Path $Evidence 'commands.json')
    if (($ExpectSuccess -and $code -ne 0) -or (!$ExpectSuccess -and $code -eq 0)) { throw "Unexpected outcome $Label Exit$code. See $log" }
}

function Test-Output([string]$Output, $Manifest) {
    foreach ($entry in $Manifest.files | Where-Object { $_.kind -in @('managed', 'xml', 'native', 'resource') }) {
        $relative = if ($entry.path.StartsWith('resources/')) { $entry.path.Substring(10) } else { [IO.Path]::GetFileName($entry.path) }
        $path = Join-Path $Output $relative
        if (!(Test-Path -LiteralPath $path) -or (Get-Item $path).Length -ne $entry.size -or (Get-FileHash $path).Hash -ne $entry.sha256) { throw "Output hash mismatch: $relative" }
    }
}

$variants = @(@('SqlServer','win-x64'), @('SqlServer','linux-x64'), @('SqlServer','linux-arm64'), @('Sqlite','win-x64'), @('PostgreSql','win-x64'))
foreach ($variant in $variants) {
    $provider = $variant[0]; $rid = $variant[1]; $label = "$provider-$rid"
    $original = Join-Path $DeliveryRoot "$provider/$rid"
    $manifest = Test-Kit $original
    $kits.Add([pscustomobject]@{ provider = $provider; rid = $rid; entries = $manifest.files.Count; manifestSha256 = (Get-FileHash (Join-Path $original 'delivery.manifest.json')).Hash; sourceRevision = $manifest.sourceRevision; efRevision = $manifest.efRevision; httpRevision = $manifest.httpRevision; sdk = $manifest.sdkVersion })
    $destination = Join-Path $Workspace $label
    New-Item -ItemType Directory -Path $destination | Out-Null
    Copy-Item -LiteralPath $original -Destination (Join-Path $destination 'kit') -Recurse
    $kit = Join-Path $destination 'kit'
    $null = Test-Kit $kit
    $consumer = Join-Path $destination 'consumer'
    New-Item -ItemType Directory -Path $consumer | Out-Null
    Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -in @('.cs','.csproj') } | Copy-Item -Destination $consumer
    $project = Join-Path $consumer 'AgentBridge.RuntimeProbe.csproj'
    $output = Join-Path $destination 'output'
    $common = @('-r', $rid, "-p:AgentBridgeDeliveryRoot=$kit", '-p:GeneratePackageOnBuild=false', '-p:EnableRuntimePackDownload=false', '-p:EnableAppHostPackDownload=false')
    Invoke-Check "$label-restore" (@('restore', $project) + $common + @('--source', 'C:/Users/Spike/.nuget/packages', '-p:NuGetAudit=false'))
    Copy-Item -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Destination (Join-Path $Evidence "$label-assets.json")
    Invoke-Check "$label-build" (@('build', $project, '-c', 'Debug', '--no-restore', '-o', $output) + $common)
    Copy-Item -LiteralPath (Join-Path $kit 'delivery.manifest.json') -Destination $output
    Test-Output $output $manifest
    Get-ChildItem -LiteralPath $output -File -Recurse | ForEach-Object { [pscustomobject]@{ path = $_.FullName; size = $_.Length; sha256 = (Get-FileHash $_.FullName).Hash } } | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $Evidence "$label-output.json")
    if ($rid -eq 'win-x64') {
        $null = Test-Kit $kit
        Invoke-Check "$label-runtime" @((Join-Path $output 'AgentBridge.RuntimeProbe.dll'), $provider, $rid)
        Test-Output $output $manifest
    }
}
$kits | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $Evidence 'kits.json')

# Negative controls use new disposable copies; original kit/output are preserved.
$baseKit = Join-Path $Workspace 'SqlServer-win-x64/kit'
$baseOutput = Join-Path $Workspace 'SqlServer-win-x64/output'
$baseManifest = Test-Kit $baseKit
$negative = Join-Path $Workspace 'negative-missing-kit'
New-Item -ItemType Directory -Path $negative | Out-Null
Get-ChildItem -LiteralPath $baseKit | Where-Object { $_.Name -ne 'lib' } | Copy-Item -Destination $negative -Recurse
New-Item -ItemType Directory -Path (Join-Path $negative 'lib') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $baseKit 'lib') | Where-Object { $_.Name -ne 'Microsoft.ML.Tokenizers.Data.O200kBase.dll' } | Copy-Item -Destination (Join-Path $negative 'lib')
$missingRejected = $false
try { $null = Test-Kit $negative } catch { if ($_.Exception.Message -like 'Missing kit file:*') { $missingRejected = $true } else { throw } }
if (!$missingRejected) { throw 'Missing dependency accepted.' }
$mixed = Join-Path $Workspace 'negative-mixed-kit'
Copy-Item -LiteralPath $baseKit -Destination $mixed -Recurse
$foreignNative = Get-ChildItem -LiteralPath (Join-Path $DeliveryRoot 'SqlServer/linux-arm64/native/linux-arm64') -File | Select-Object -First 1
$targetNative = Get-ChildItem -LiteralPath (Join-Path $mixed 'native/win-x64') -File | Select-Object -First 1
Copy-Item -LiteralPath $foreignNative.FullName -Destination $targetNative.FullName
$mixedRejected = $false
try { $null = Test-Kit $mixed } catch { if ($_.Exception.Message -like 'Kit hash mismatch:*') { $mixedRejected = $true } else { throw } }
if (!$mixedRejected) { throw 'Mixed native dependency accepted.' }

# Fault injection after a verified fresh build: assembly absence must fail, never resolve from original kit.
$missingOutput = Join-Path $Workspace 'negative-missing-output'
New-Item -ItemType Directory -Path $missingOutput | Out-Null
Get-ChildItem -LiteralPath $baseOutput | Where-Object { $_.Name -ne 'Microsoft.ML.Tokenizers.Data.O200kBase.dll' } | Copy-Item -Destination $missingOutput -Recurse
Invoke-Check 'missing-resource-runtime' @((Join-Path $missingOutput 'AgentBridge.RuntimeProbe.dll'), 'SqlServer', 'win-x64') $false
$failureText = Get-Content -Raw (Join-Path $Evidence 'missing-resource-runtime.log')
if ($failureText -notmatch 'FileNotFoundException' -or $failureText -notmatch 'Microsoft.ML.Tokenizers.Data.O200kBase') { throw 'Unexpected missing-resource failure.' }
Invoke-Check 'wrong-rid-runtime' @((Join-Path $baseOutput 'AgentBridge.RuntimeProbe.dll'), 'SqlServer', 'linux-arm64') $false
if ((Get-Content -Raw (Join-Path $Evidence 'wrong-rid-runtime.log')) -notmatch 'Runtime RID win-x64 does not match linux-arm64') { throw 'Unexpected RID failure.' }
Invoke-Check 'wrong-provider-runtime' @((Join-Path $baseOutput 'AgentBridge.RuntimeProbe.dll'), 'Sqlite', 'win-x64') $false
if ((Get-Content -Raw (Join-Path $Evidence 'wrong-provider-runtime.log')) -notmatch 'Manifest schema/framework/provider/RID does not match') { throw 'Unexpected provider failure.' }
$mixedOutput = Join-Path $Workspace 'negative-mixed-output'
Copy-Item -LiteralPath $baseOutput -Destination $mixedOutput -Recurse
Copy-Item -LiteralPath $foreignNative.FullName -Destination (Join-Path $mixedOutput $targetNative.Name)
Invoke-Check 'mixed-native-runtime' @((Join-Path $mixedOutput 'AgentBridge.RuntimeProbe.dll'), 'SqlServer', 'win-x64') $false
if ((Get-Content -Raw (Join-Path $Evidence 'mixed-native-runtime.log')) -notmatch 'Output hash mismatch') { throw 'Unexpected mixed-native failure.' }
@{ missingKitRejected = $missingRejected; mixedNativeRejected = $mixedRejected; missingResourceRuntimeFailed = $true; wrongRidRuntimeFailed = $true; wrongProviderRuntimeFailed = $true; mixedNativeRuntimeFailed = $true; nativePositiveLoadFree = $true } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $Evidence 'negative-controls.json')
