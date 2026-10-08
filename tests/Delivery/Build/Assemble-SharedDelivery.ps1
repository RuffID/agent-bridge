# Объединяет проверенные комплекты: одинаковые managed/XML/resources хранит один раз, native остаются по RID.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SourceRoot,
    [Parameter(Mandatory)][string] $Destination
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$sourceRootPath = (Resolve-Path -LiteralPath $SourceRoot).Path
$destinationRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationRoot) { throw 'Destination must be new; never reuse a stale bundle.' }
if ($destinationRoot.StartsWith($sourceRootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination cannot be inside the input kits.'
}
$rids = @('win-x64', 'linux-x64', 'linux-arm64')
$kits = @{}
$assets = [Collections.Generic.List[object]]::new()

function Assert-RelativePath([string] $path) {
    if ($path.Contains('\') -or $path.Contains(':') -or $path.StartsWith('/') -or @($path.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count) {
        throw "Nonportable manifest path: $path"
    }
}

# Проверка полного исходного комплекта до любых изменений destination.
foreach ($rid in $rids) {
    $kitRoot = Join-Path $sourceRootPath $rid
    $manifestPath = Join-Path $kitRoot 'delivery.manifest.json'
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
    if ($manifest.schemaVersion -ne 1 -or $manifest.rid -cne $rid -or $manifest.framework -cne 'net10.0') { throw "Unsupported source kit: $rid" }
    foreach ($field in @('provider', 'configuration', 'framework', 'sdkVersion', 'sourceRevision', 'efRevision', 'httpRevision', 'generatorSha256')) {
        if ($kits.Count -gt 0 -and $manifest[$field] -cne $kits[$rids[0]].manifest[$field]) { throw "Mixed $field in source kits." }
    }
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $manifest.files) {
        Assert-RelativePath $entry.path
        if (-not $paths.Add($entry.path)) { throw "Duplicate source path: $rid/$($entry.path)" }
        $file = Join-Path $kitRoot $entry.path
        $current = $kitRoot
        foreach ($segment in $entry.path.Split('/')) {
            $matches = @(Get-ChildItem -LiteralPath $current | Where-Object { $_.Name -ceq $segment })
            if ($matches.Count -ne 1) { throw "Missing exact-case source: $rid/$($entry.path)" }
            $current = $matches[0].FullName
        }
        if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -ne $entry.size -or
            (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -cne $entry.sha256) { throw "Source hash mismatch: $rid/$($entry.path)" }
        if ($entry.kind -notin @('props', 'deps')) {
            if ($entry.kind -notin @('managed', 'xml', 'resource', 'native')) { throw "Unknown asset kind: $($entry.kind)" }
            $assets.Add(@{ rid = $rid; entry = $entry; file = $file })
        }
    }
    $actual = @(Get-ChildItem -LiteralPath $kitRoot -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($kitRoot, $_.FullName).Replace('\', '/') } | Where-Object { $_ -cne 'delivery.manifest.json' })
    if ($actual.Count -ne $paths.Count -or @($actual | Where-Object { -not $paths.Contains($_) }).Count) { throw "Unlisted source files: $rid" }
    $kits[$rid] = @{ root = $kitRoot; manifest = $manifest; manifestPath = $manifestPath; entries = [Collections.Generic.List[object]]::new() }
}

$null = New-Item -ItemType Directory -Path $destinationRoot
$groups = @($assets | Group-Object { $_.entry.path + '|' + $_.entry.kind + '|' + $_.entry.sha256 })
foreach ($group in $groups) {
    $first = $group.Group[0]
    $entry = $first.entry
    if ($entry.kind -eq 'native') {
        # Нативный код всегда остаётся явно привязанным к платформе даже при совпадении hash.
        foreach ($asset in $group.Group) {
            $relative = "platform/$($asset.rid)/native/" + [IO.Path]::GetFileName($entry.path)
            $target = Join-Path $destinationRoot $relative
            $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force
            Copy-Item -LiteralPath $asset.file -Destination $target
            $mapped = [ordered]@{}
            foreach ($key in $asset.entry.Keys) { $mapped[$key] = $asset.entry[$key] }
            $mapped.sourcePath = $asset.entry.path
            $mapped.path = $relative
            $kits[$asset.rid].entries.Add($mapped)
        }
        continue
    }
    $relative = if ($group.Count -eq $rids.Count) { 'common/' + $entry.path }
        elseif ($group.Count -gt 1) { 'common/variants/' + $entry.sha256 + '/' + $entry.path }
        else { "platform/$($first.rid)/" + $entry.path }
    $target = Join-Path $destinationRoot $relative
    $null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force
    Copy-Item -LiteralPath $first.file -Destination $target
    foreach ($asset in $group.Group) {
        if ($asset.entry.size -ne $entry.size -or $asset.entry.kind -cne $entry.kind) { throw 'Shared metadata mismatch.' }
        $mapped = [ordered]@{}
        foreach ($key in $asset.entry.Keys) { $mapped[$key] = $asset.entry[$key] }
        $mapped.sourcePath = $asset.entry.path
        $mapped.path = $relative
        $kits[$asset.rid].entries.Add($mapped)
    }
}

$propsSource = Join-Path $PSScriptRoot '../AgentBridge.SharedDelivery.props'
Copy-Item -LiteralPath $propsSource -Destination (Join-Path $destinationRoot 'AgentBridge.Delivery.props')
foreach ($rid in $rids) {
    $kit = $kits[$rid]
    $platformRoot = Join-Path $destinationRoot "platform/$rid"
    $null = New-Item -ItemType Directory -Path (Join-Path $platformRoot 'evidence') -Force
    $builder = [Text.StringBuilder]::new()
    $null = $builder.AppendLine('<Project>')
    $null = $builder.AppendLine("  <PropertyGroup><AgentBridgeDeliveryRid>$rid</AgentBridgeDeliveryRid><AgentBridgeDeliveryProvider>$($kit.manifest.provider)</AgentBridgeDeliveryProvider></PropertyGroup>")
    $null = $builder.AppendLine('  <ItemGroup>')
    $outputPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in @($kit.entries | Sort-Object -Property sourcePath -CaseSensitive)) {
        $output = if ($entry.kind -eq 'resource') { $entry.sourcePath.Substring('resources/'.Length) } else { [IO.Path]::GetFileName($entry.sourcePath) }
        if (-not $outputPaths.Add($output)) { throw "Duplicate output asset: $rid/$output" }
        $include = '$(MSBuildThisFileDirectory)../../' + $entry.path
        if ($entry.kind -eq 'managed') {
            $null = $builder.AppendLine("    <Reference Include=`"$($entry.assemblyName)`"><HintPath>$include</HintPath><Private>true</Private></Reference>")
        } else {
            $null = $builder.AppendLine("    <None Include=`"$include`" Link=`"$output`" CopyToOutputDirectory=`"PreserveNewest`" CopyToPublishDirectory=`"PreserveNewest`" />")
        }
    }
    $null = $builder.AppendLine('  </ItemGroup>')
    $null = $builder.AppendLine('</Project>')
    $variant = Join-Path $platformRoot 'AgentBridge.Delivery.variant.props'
    [IO.File]::WriteAllText($variant, $builder.ToString(), [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $kit.root 'evidence/AgentBridge.Delivery.deps.json') -Destination (Join-Path $platformRoot 'evidence/AgentBridge.Delivery.deps.json')
    Copy-Item -LiteralPath $kit.manifestPath -Destination (Join-Path $platformRoot 'evidence/source.manifest.json')
    foreach ($relative in @('AgentBridge.Delivery.props', "platform/$rid/AgentBridge.Delivery.variant.props", "platform/$rid/evidence/AgentBridge.Delivery.deps.json", "platform/$rid/evidence/source.manifest.json")) {
        $file = Join-Path $destinationRoot $relative
        $kit.entries.Add([ordered]@{ path = $relative; kind = 'metadata'; size = (Get-Item -LiteralPath $file).Length; sha256 = (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() })
    }
    $manifest = [ordered]@{ schemaVersion = 2; layout = 'shared'; rid = $rid; provider = $kit.manifest.provider; framework = 'net10.0'; frameworkDependent = $true;
        configuration = $kit.manifest.configuration; sdkVersion = $kit.manifest.sdkVersion; sourceRevision = $kit.manifest.sourceRevision;
        efRevision = $kit.manifest.efRevision; httpRevision = $kit.manifest.httpRevision; sourceGeneratorSha256 = $kit.manifest.generatorSha256;
        generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant(); files = @($kit.entries.ToArray() | Sort-Object -Property path -CaseSensitive) }
    [IO.File]::WriteAllText((Join-Path $platformRoot 'delivery.manifest.json'), ($manifest | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
}
Write-Output "Assembled shared $($kits[$rids[0]].manifest.provider): $((Get-ChildItem -LiteralPath $destinationRoot -File -Recurse).Count) files at $destinationRoot"
