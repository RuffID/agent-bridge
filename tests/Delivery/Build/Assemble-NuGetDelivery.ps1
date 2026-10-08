# Создаёт поставку наших DLL с обязательными NuGet-зависимостями из проверенных SDK-комплектов одного провайдера.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SourceRoot,
    [Parameter(Mandatory)][string] $Destination
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$sourceRootPath = (Resolve-Path -LiteralPath $SourceRoot).Path
$destinationRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationRoot) { throw 'Destination must be new.' }
if ($destinationRoot.StartsWith($sourceRootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination cannot be inside source kits.'
}
$rids = @(Get-ChildItem -LiteralPath $sourceRootPath -Directory | Select-Object -ExpandProperty Name | Sort-Object)
if (-not $rids.Count -or @($rids | Where-Object { $_ -notin @('win-x64', 'linux-x64', 'linux-arm64') }).Count) {
    throw 'SourceRoot must contain only supported RID kits for one provider.'
}
$localAssets = @{}
$requiredPackages = @{}
$sourceKits = [Collections.Generic.List[object]]::new()
$baseline = $null

function Assert-RelativePath([string] $path) {
    if ($path.Contains('\') -or $path.Contains(':') -or $path.StartsWith('/') -or @($path.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count) {
        throw "Nonportable manifest path: $path"
    }
}

# Проверяем полную исходную поставку и граф до создания destination; никаких DLL не загружаем.
foreach ($rid in $rids) {
    $kitRoot = Join-Path $sourceRootPath $rid
    $manifestPath = Join-Path $kitRoot 'delivery.manifest.json'
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
    if ($manifest.schemaVersion -ne 1 -or $manifest.rid -cne $rid -or $manifest.framework -cne 'net10.0' -or
        $manifest.provider -notin @('SqlServer', 'Sqlite', 'PostgreSql')) { throw "Unsupported source kit: $rid" }
    foreach ($field in @('provider', 'configuration', 'framework', 'sdkVersion', 'sourceRevision', 'efRevision', 'httpRevision', 'generatorSha256')) {
        if ($null -ne $baseline -and $manifest[$field] -cne $baseline[$field]) { throw "Mixed $field in source kits." }
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
        if ($entry.kind -notin @('managed', 'xml', 'native', 'resource', 'props', 'deps')) { throw "Unknown asset kind: $($entry.kind)" }
    }
    $actual = @(Get-ChildItem -LiteralPath $kitRoot -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($kitRoot, $_.FullName).Replace('\', '/') } | Where-Object { $_ -cne 'delivery.manifest.json' })
    if ($actual.Count -ne $paths.Count -or @($actual | Where-Object { -not $paths.Contains($_) }).Count) { throw "Unlisted source files: $rid" }
    if (-not $paths.Contains('evidence/AgentBridge.Delivery.deps.json')) { throw 'Missing verified SDK deps.' }
    $deps = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $kitRoot 'evidence/AgentBridge.Delivery.deps.json') | ConvertFrom-Json -AsHashtable
    $target = $deps.targets[$deps.runtimeTarget.name]
    if ($deps.runtimeTarget.name -cne ".NETCoreApp,Version=v10.0/$rid") { throw 'SDK deps RID mismatch.' }
    $packages = @{}
    foreach ($key in $deps.libraries.Keys) {
        if ($deps.libraries[$key].type -eq 'package') {
            $parts = $key.Split('/')
            if ($parts.Count -ne 2 -or $parts[0] -notmatch '^[A-Za-z0-9_.-]+$' -or $parts[1] -notmatch '^[0-9][A-Za-z0-9.+-]*$') { throw 'Invalid package identity.' }
            if ($packages.ContainsKey($parts[0])) { throw 'Conflicting package versions.' }
            $packages[$parts[0]] = @{ id = $parts[0]; version = $parts[1]; sha512 = $deps.libraries[$key].sha512 }
        }
    }
    $kitRequired = @{}
    foreach ($key in $target.Keys) {
        if ($deps.libraries[$key].type -ne 'project' -or -not $target[$key].ContainsKey('dependencies')) { continue }
        foreach ($dependency in $target[$key].dependencies.Keys) {
            if ($packages.ContainsKey($dependency)) { $kitRequired[$dependency] = $packages[$dependency].version }
        }
    }
    if (-not $kitRequired.Count) { throw 'Missing direct NuGet dependencies of local libraries.' }
    $kitLocal = @{}
    foreach ($entry in $manifest.files) {
        if ($entry.kind -notin @('managed', 'xml')) { continue }
        if (-not $deps.libraries.ContainsKey($entry.origin)) { throw "Unknown asset origin: $($entry.origin)" }
        if ($deps.libraries[$entry.origin].type -eq 'package') { continue }
        if ($deps.libraries[$entry.origin].type -notin @('project', 'reference')) { throw 'Unknown local library type.' }
        if ($entry.path -notmatch '^lib/(AgentBridge[^/]*|EFCoreLibrary[^/]*|HttpClientLibrary)\.(dll|xml)$') { throw "Unexpected local asset: $($entry.path)" }
        $kitLocal[$entry.path] = @{ entry = $entry; file = (Join-Path $kitRoot $entry.path) }
    }
    $moduleName = "AgentBridge.Persistence.$($manifest.provider)"
    foreach ($assembly in @('AgentBridge', 'AgentBridge.Integration', 'AgentBridge.CodexLb', 'AgentBridge.Persistence.EfCore', $moduleName,
            "AgentBridge.Persistence.Migrations.$($manifest.provider)", 'EFCoreLibrary', 'EFCoreLibrary.Maintenance',
            "EFCoreLibrary.Maintenance.$($manifest.provider)", 'HttpClientLibrary')) {
        if (-not $kitLocal.ContainsKey("lib/$assembly.dll")) { throw "Missing local DLL: $assembly" }
        if ($assembly -ne 'EFCoreLibrary' -and -not $kitLocal.ContainsKey("lib/$assembly.xml")) { throw "Missing local XML: $assembly" }
    }
    if (@($kitLocal.Values | Where-Object { $_.entry.kind -eq 'managed' }).Count -ne 10) { throw 'Unexpected local library closure.' }
    if ($null -eq $baseline) {
        $baseline = $manifest
        $localAssets = $kitLocal
        $requiredPackages = $kitRequired
    } else {
        if ($kitLocal.Count -ne $localAssets.Count -or $kitRequired.Count -ne $requiredPackages.Count) { throw 'Mixed local/package closures.' }
        foreach ($path in $localAssets.Keys) {
            if (-not $kitLocal.ContainsKey($path) -or $kitLocal[$path].entry.sha256 -cne $localAssets[$path].entry.sha256) { throw "RID-specific local library requires a separate delivery: $path" }
        }
        foreach ($id in $requiredPackages.Keys) {
            if (-not $kitRequired.ContainsKey($id) -or $kitRequired[$id] -cne $requiredPackages[$id]) { throw "Mixed direct dependency: $id" }
        }
    }
    $sourceKits.Add([ordered]@{ rid = $rid; manifestSha256 = (Get-FileHash -LiteralPath $manifestPath).Hash.ToLowerInvariant();
        generatorSha256 = $manifest.generatorSha256; packages = @($packages.Values | Sort-Object id) })
}

$null = New-Item -ItemType Directory -Path (Join-Path $destinationRoot 'lib')
$files = [Collections.Generic.List[object]]::new()
foreach ($path in @($localAssets.Keys | Sort-Object)) {
    Copy-Item -LiteralPath $localAssets[$path].file -Destination (Join-Path $destinationRoot $path)
    $files.Add($localAssets[$path].entry)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../AgentBridge.NuGetDelivery.props') -Destination (Join-Path $destinationRoot 'AgentBridge.Delivery.props')
$references = [Text.StringBuilder]::new()
$null = $references.AppendLine('<Project>')
$null = $references.AppendLine("  <PropertyGroup><AgentBridgeDeliveryProvider>$($baseline.provider)</AgentBridgeDeliveryProvider><AgentBridgeDeliveryRids>$($rids -join ';')</AgentBridgeDeliveryRids></PropertyGroup>")
$null = $references.AppendLine('  <ItemGroup>')
foreach ($path in @($localAssets.Keys | Sort-Object)) {
    $entry = $localAssets[$path].entry
    if ($entry.kind -eq 'managed') {
        $null = $references.AppendLine("    <Reference Include=`"$($entry.assemblyName)`"><HintPath>`$(MSBuildThisFileDirectory)$path</HintPath><Private>true</Private></Reference>")
    } else {
        $output = [IO.Path]::GetFileName($path)
        $null = $references.AppendLine("    <None Include=`"`$(MSBuildThisFileDirectory)$path`" Link=`"$output`" CopyToOutputDirectory=`"PreserveNewest`" CopyToPublishDirectory=`"PreserveNewest`" />")
    }
}
$null = $references.AppendLine('  </ItemGroup>')
$null = $references.AppendLine('</Project>')
[IO.File]::WriteAllText((Join-Path $destinationRoot 'AgentBridge.References.props'), $references.ToString(), [Text.UTF8Encoding]::new($false))

$dependencies = [Text.StringBuilder]::new()
$null = $dependencies.AppendLine('<Project>')
$null = $dependencies.AppendLine('  <ItemGroup>')
foreach ($id in @($requiredPackages.Keys | Sort-Object)) {
    $version = $requiredPackages[$id]
    $selection = "@(PackageReference->WithMetadataValue('Identity', '$id'))"
    $versions = "@(PackageReference->WithMetadataValue('Identity', '$id')->Metadata('Version'))"
    $null = $dependencies.AppendLine("    <PackageReference Include=`"$id`" Version=`"[$version]`" Condition=`"'$selection' == ''`" />")
    # Превращаем одинаковую app minimum version в exact; отличающийся выбор не перезаписываем, validation его отклоняет.
    $null = $dependencies.AppendLine("    <PackageReference Update=`"$id`" Version=`"[$version]`" Condition=`"'$versions' == '$version'`" />")
}
$null = $dependencies.AppendLine('  </ItemGroup>')
$null = $dependencies.AppendLine('  <Target Name="ValidateAgentBridgeNuGetRequirements" BeforeTargets="CollectPackageReferences;PrepareForBuild">')
foreach ($id in @($requiredPackages.Keys | Sort-Object)) {
    $version = $requiredPackages[$id]
    $versions = "@(PackageReference->WithMetadataValue('Identity', '$id')->Metadata('Version'))"
    $null = $dependencies.AppendLine("    <Error Condition=`"'$versions' != '[$version]'`" Text=`"AgentBridge requires one PackageReference $id with exact Version=[$version]. Import AgentBridge after application PackageReference items; conflicting versions or duplicates are unsupported.`" />")
}
$null = $dependencies.AppendLine('  </Target>')
$null = $dependencies.AppendLine('</Project>')
[IO.File]::WriteAllText((Join-Path $destinationRoot 'AgentBridge.Dependencies.props'), $dependencies.ToString(), [Text.UTF8Encoding]::new($false))
foreach ($path in @('AgentBridge.Delivery.props', 'AgentBridge.References.props', 'AgentBridge.Dependencies.props')) {
    $file = Join-Path $destinationRoot $path
    $files.Add([ordered]@{ path = $path; kind = 'metadata'; size = (Get-Item -LiteralPath $file).Length; sha256 = (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() })
}
$manifest = [ordered]@{ schemaVersion = 3; layout = 'dll-nuget'; provider = $baseline.provider; framework = 'net10.0'; rids = $rids;
    configuration = $baseline.configuration; sdkVersion = $baseline.sdkVersion; sourceRevision = $baseline.sourceRevision;
    efRevision = $baseline.efRevision; httpRevision = $baseline.httpRevision; generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant();
    requiredPackages = @($requiredPackages.Keys | Sort-Object | ForEach-Object { [ordered]@{ id = $_; version = $requiredPackages[$_] } });
    sourceKits = $sourceKits.ToArray(); files = @($files.ToArray() | Sort-Object path) }
[IO.File]::WriteAllText((Join-Path $destinationRoot 'delivery.manifest.json'), ($manifest | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "Assembled DLL/NuGet $($baseline.provider): 10 local libraries, $($requiredPackages.Count) direct packages at $destinationRoot"
