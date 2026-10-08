# Собирает комплект из fresh SDK Build output; не запускает DLL, native code, приложение или внешние команды.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BuildOutput,
    [Parameter(Mandatory)][string] $AssetsFile,
    [Parameter(Mandatory)][string] $Destination,
    [Parameter(Mandatory)][ValidateSet('SqlServer', 'Sqlite', 'PostgreSql')][string] $Provider,
    [Parameter(Mandatory)][ValidateSet('win-x64', 'linux-x64', 'linux-arm64')][string] $Rid,
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug',
    [Parameter(Mandatory)][string] $SourceRevision,
    [Parameter(Mandatory)][string] $EfRevision,
    [Parameter(Mandatory)][string] $HttpRevision,
    [Parameter(Mandatory)][string] $SdkVersion
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$buildRoot = (Resolve-Path -LiteralPath $BuildOutput).Path
$destinationRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationRoot) { throw 'Destination must be new; never reuse a stale kit.' }
$assets = Get-Content -LiteralPath $AssetsFile -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$depsFile = Join-Path $buildRoot 'AgentBridge.Delivery.deps.json'
$deps = Get-Content -LiteralPath $depsFile -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$targetName = $deps.runtimeTarget.name
if (-not $targetName.EndsWith('/' + $Rid, [StringComparison]::Ordinal)) { throw "SDK deps RID mismatch: $targetName" }
$target = $deps.targets[$targetName]
if (-not $target.ContainsKey('AgentBridge.Integration/1.0.0')) { throw 'SDK graph does not contain Integration facade.' }
$migrations = @($target.Keys | Where-Object { $_.StartsWith('AgentBridge.Persistence.Migrations.', [StringComparison]::Ordinal) })
if ($migrations.Count -ne 1 -or -not $migrations[0].StartsWith("AgentBridge.Persistence.Migrations.$Provider/", [StringComparison]::Ordinal)) { throw 'Selected migrations graph mismatch.' }
$null = New-Item -ItemType Directory -Path (Join-Path $destinationRoot 'lib'), (Join-Path $destinationRoot "native/$Rid"), (Join-Path $destinationRoot 'evidence')
$entries = [Collections.Generic.List[object]]::new()

# SHA и metadata читаются без Assembly.Load/native loading.
function Add-DeliveryFile([string] $source, [string] $relative, [string] $kind, [string] $origin, [string] $asset) {
    $targetPath = Join-Path $destinationRoot $relative
    if (Test-Path -LiteralPath $targetPath) {
        $existing = @($entries | Where-Object { $_.path -ceq $relative })
        if ($existing.Count -ne 1 -or $existing[0].kind -cne $kind -or (Get-FileHash -LiteralPath $source).Hash -cne (Get-FileHash -LiteralPath $targetPath).Hash) { throw "Conflicting delivery path: $relative" }
        # SDK может перечислить ту же проектную DLL также как Reference; обе provenance сохраняются.
        if (-not $existing[0].Contains('aliases')) { $existing[0].aliases = @() }
        $existing[0].aliases += [ordered]@{ origin = $origin; asset = $asset }
        return
    }
    $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($targetPath)) -Force
    Copy-Item -LiteralPath $source -Destination $targetPath
    $entry = [ordered]@{ path = $relative; kind = $kind; origin = $origin; asset = $asset; size = (Get-Item -LiteralPath $targetPath).Length; sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    if ($kind -in @('managed', 'resource')) {
        $assembly = [Reflection.AssemblyName]::GetAssemblyName($targetPath)
        $entry.assemblyName = $assembly.Name
        $entry.assemblyVersion = $assembly.Version.ToString()
        $entry.fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($targetPath).FileVersion
        $entry.culture = $assembly.CultureName
    }
    $entries.Add($entry)
}

foreach ($library in @($target.Keys | Sort-Object -CaseSensitive)) {
    if ($library.StartsWith('AgentBridge.Delivery/', [StringComparison]::Ordinal)) { continue }
    $definition = $target[$library]
    foreach ($kind in @('runtime', 'native', 'resources')) {
        if (-not $definition.ContainsKey($kind)) { continue }
        foreach ($asset in @($definition[$kind].Keys | Sort-Object -CaseSensitive)) {
            if ($asset.EndsWith('/_._', [StringComparison]::Ordinal)) { continue }
            $fileName = [IO.Path]::GetFileName($asset)
            $outputFile = Join-Path $buildRoot $fileName
            if ($kind -eq 'resources') {
                $locale = $definition[$kind][$asset].locale
                if ($locale -cnotmatch '^[A-Za-z0-9-]+$') { throw "Invalid resource locale: $asset" }
                $outputFile = Join-Path $buildRoot "$locale/$fileName"
            }
            if (-not (Test-Path -LiteralPath $outputFile -PathType Leaf)) { throw "SDK output missing $library : $asset" }
            $packageFile = $null
            if ($deps.libraries[$library].type -eq 'package') {
                $packagePath = $assets.libraries[$library].path
                foreach ($folder in $assets.packageFolders.Keys) {
                    $candidate = Join-Path $folder "$packagePath/$asset"
                    if (Test-Path -LiteralPath $candidate -PathType Leaf) { $packageFile = $candidate; break }
                }
                if (-not $packageFile) { throw "Package asset missing: $library : $asset" }
                if ((Get-FileHash -LiteralPath $packageFile).Hash -cne (Get-FileHash -LiteralPath $outputFile).Hash) { throw "SDK/package hash mismatch: $asset" }
            }
            if ($kind -eq 'resources') {
                $resourceAssembly = [Reflection.AssemblyName]::GetAssemblyName($outputFile)
                if ($resourceAssembly.CultureName -cne $locale) { throw "Resource culture mismatch: $asset" }
                Add-DeliveryFile $outputFile "resources/$locale/$fileName" 'resource' $library $asset
            } elseif ($kind -eq 'runtime') {
                $null = [Reflection.AssemblyName]::GetAssemblyName($outputFile)
                Add-DeliveryFile $outputFile "lib/$fileName" 'managed' $library $asset
                $xmlFile = [IO.Path]::ChangeExtension($outputFile, '.xml')
                if (-not (Test-Path -LiteralPath $xmlFile) -and $packageFile) { $xmlFile = [IO.Path]::ChangeExtension($packageFile, '.xml') }
                if (Test-Path -LiteralPath $xmlFile -PathType Leaf) { Add-DeliveryFile $xmlFile ('lib/' + [IO.Path]::GetFileName($xmlFile)) 'xml' $library ([IO.Path]::ChangeExtension($asset, '.xml')) }
            } else {
                $bytes = [IO.File]::ReadAllBytes($outputFile)
                if ($Rid -eq 'win-x64') {
                    if ($bytes.Length -lt 64 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) { throw "Expected native PE: $asset" }
                    $offset = [BitConverter]::ToInt32($bytes, 0x3c)
                    if ($offset -lt 0 -or $offset + 6 -gt $bytes.Length -or [BitConverter]::ToUInt32($bytes, $offset) -ne 0x4550 -or [BitConverter]::ToUInt16($bytes, $offset + 4) -ne 0x8664) { throw "Expected AMD64 PE: $asset" }
                    try { $null = [Reflection.AssemblyName]::GetAssemblyName($outputFile); throw "Native asset has managed metadata: $asset" } catch [BadImageFormatException] { }
                } else {
                    $machine = if ($Rid -eq 'linux-arm64') { 183 } else { 62 }
                    if ($bytes.Length -lt 64 -or $bytes[0] -ne 0x7f -or $bytes[1] -ne 0x45 -or $bytes[2] -ne 0x4c -or $bytes[3] -ne 0x46 -or $bytes[4] -ne 2 -or $bytes[5] -ne 1 -or [BitConverter]::ToUInt16($bytes, 18) -ne $machine) { throw "Expected ELF64 little-endian machine $machine : $asset" }
                }
                Add-DeliveryFile $outputFile "native/$Rid/$fileName" 'native' $library $asset
            }
        }
    }
    if ($definition.ContainsKey('runtimeTargets')) { throw "Unresolved runtimeTargets in RID-specific deps: $library" }
}
Add-DeliveryFile (Join-Path $PSScriptRoot '../AgentBridge.Delivery.props') 'AgentBridge.Delivery.props' 'props' 'source' 'tests/Delivery/AgentBridge.Delivery.props'
$variantPath = Join-Path $destinationRoot 'AgentBridge.Delivery.variant.props'
[IO.File]::WriteAllText($variantPath, "<Project><PropertyGroup><AgentBridgeDeliveryRid>$Rid</AgentBridgeDeliveryRid><AgentBridgeDeliveryProvider>$Provider</AgentBridgeDeliveryProvider></PropertyGroup></Project>`n", [Text.UTF8Encoding]::new($false))
$entries.Add([ordered]@{ path = 'AgentBridge.Delivery.variant.props'; kind = 'props'; origin = 'generated'; asset = $Rid; size = (Get-Item -LiteralPath $variantPath).Length; sha256 = (Get-FileHash -LiteralPath $variantPath).Hash.ToLowerInvariant() })
Add-DeliveryFile $depsFile 'evidence/AgentBridge.Delivery.deps.json' 'deps' 'sdk' $targetName
$manifest = [ordered]@{ schemaVersion = 1; framework = 'net10.0'; frameworkDependent = $true; configuration = $Configuration; rid = $Rid; provider = $Provider; sdkVersion = $SdkVersion; sourceRevision = $SourceRevision; efRevision = $EfRevision; httpRevision = $HttpRevision; assetsSha256 = (Get-FileHash -LiteralPath $AssetsFile).Hash.ToLowerInvariant(); generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant(); files = @($entries.ToArray() | Sort-Object -Property path -CaseSensitive) }
[IO.File]::WriteAllText((Join-Path $destinationRoot 'delivery.manifest.json'), ($manifest | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "Assembled $Provider/$Rid : $($entries.Count) files at $destinationRoot"
