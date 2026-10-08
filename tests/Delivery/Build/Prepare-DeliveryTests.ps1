# Подготавливает проектную поставку перед сборкой metadata-тестов; DLL, приложение и native code не исполняются.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$artifactsRoot = Join-Path $repositoryRoot 'artifacts/delivery-tests'
$currentRoot = Join-Path $artifactsRoot 'current'
$readyPath = Join-Path $currentRoot 'ready.json'
$deliveryProject = Join-Path $PSScriptRoot 'AgentBridge.Delivery.csproj'
$efRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '../work/EFCoreLibrary'))
$httpRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '../work/HttpClientLibrary'))
$null = New-Item -ItemType Directory -Path $artifactsRoot -Force
$preparationLock = [IO.FileStream]::new((Join-Path $artifactsRoot 'preparation.lock'),
    [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)

# Generated/output, тесты соседних библиотек и документация не являются входами runtime-поставки.
function Get-SourceFiles([string] $directory) {
    foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
        if ($entry.Name -in @('bin', 'obj', 'artifacts', '.git', '.vs', 'tests', 'Documentation') -or
            $entry.Name.EndsWith('.Tests', [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source reparse point is unsupported: $($entry.FullName)" }
        if ($entry.PSIsContainer) {
            Get-SourceFiles $entry.FullName
        } elseif ($entry.Extension -notin @('.md', '.suo', '.user')) {
            $entry
        }
    }
}

# Hash учитывает содержимое и relative paths, включая tokenizer resources и build/packaging configuration.
function Get-SourceFingerprint {
    $groups = [ordered]@{
        agentBridge = @((Join-Path $repositoryRoot 'AgentBridge'), (Join-Path $repositoryRoot 'adapters'),
            (Join-Path $repositoryRoot 'tests/Delivery'))
        efCoreLibrary = @($efRoot)
        httpClientLibrary = @($httpRoot)
    }
    $result = [ordered]@{}
    foreach ($group in $groups.Keys) {
        $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
        try {
            foreach ($directory in $groups[$group]) {
                foreach ($file in @(Get-SourceFiles $directory | Sort-Object FullName -CaseSensitive)) {
                    $relative = [IO.Path]::GetRelativePath($repositoryRoot, $file.FullName).Replace('\', '/')
                    $fileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                    $hash.AppendData([Text.Encoding]::UTF8.GetBytes($relative + "`n" + $fileHash + "`n"))
                }
            }
            foreach ($file in @(Get-ChildItem -LiteralPath $repositoryRoot -File | Where-Object {
                $_.Name -in @('global.json', 'NuGet.config') -or $_.Extension -in @('.props', '.targets')
            } | Sort-Object Name -CaseSensitive)) {
                $hash.AppendData([Text.Encoding]::UTF8.GetBytes($file.Name + "`n" + (Get-FileHash -LiteralPath $file.FullName).Hash + "`n"))
            }
            $result[$group] = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
        } finally {
            $hash.Dispose()
        }
    }
    $result['fingerprint'] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes(($result.Values -join '|') + '|' + $sdkVersion))).ToLowerInvariant()
    return $result
}

function Invoke-DotNet([string[]] $arguments) {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $($arguments -join ' ')" }
}

# Удаляются только собственные временные/previous каталоги внутри фиксированного корня этой проверки.
function Remove-OwnedDirectory([string] $directory) {
    $absolute = [IO.Path]::GetFullPath($directory)
    $prefix = [IO.Path]::GetFullPath($artifactsRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup path escapes delivery-test artifacts: $absolute"
    }
    if (Test-Path -LiteralPath $absolute) {
        if ((Get-Item -LiteralPath $absolute).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Cleanup reparse point is unsupported: $absolute"
        }
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}

Push-Location -LiteralPath $repositoryRoot
try {
    $sdkVersion = (& dotnet --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $sdkVersion.StartsWith('10.', [StringComparison]::Ordinal)) {
        throw 'Delivery tests require a working .NET 10 SDK.'
    }
    $initialFingerprint = Get-SourceFingerprint
    $requiredManifests = @('sdk/SqlServer/win-x64', 'sdk/SqlServer/linux-x64', 'sdk/SqlServer/linux-arm64',
        'sdk/Sqlite/win-x64', 'sdk/PostgreSql/win-x64', 'nuget/SqlServer', 'nuget/Sqlite', 'nuget/PostgreSql',
        'shared/SqlServer/platform/win-x64', 'shared/SqlServer/platform/linux-x64', 'shared/SqlServer/platform/linux-arm64')
    if (Test-Path -LiteralPath $readyPath -PathType Leaf) {
        $ready = Get-Content -Raw -Encoding UTF8 -LiteralPath $readyPath | ConvertFrom-Json -AsHashtable
        $missing = @($requiredManifests | Where-Object {
            -not (Test-Path -LiteralPath (Join-Path $currentRoot "$_/delivery.manifest.json") -PathType Leaf)
        })
        if ($ready.schemaVersion -eq 1 -and $ready.fingerprint -ceq $initialFingerprint.fingerprint -and -not $missing.Count) {
            Write-Output "Delivery-test inputs unchanged; using $currentRoot"
            return
        }
        # После неудачной новой подготовки предыдущая поставка не считается готовой.
        Remove-Item -LiteralPath $readyPath
    }

    $runId = [Guid]::NewGuid().ToString('N')
    $workspace = Join-Path $artifactsRoot "staging/$runId"
    $preparedRoot = Join-Path $workspace 'current'
    $null = New-Item -ItemType Directory -Path $preparedRoot
    $variants = @(
        @{ provider = 'SqlServer'; rid = 'win-x64' },
        @{ provider = 'SqlServer'; rid = 'linux-x64' },
        @{ provider = 'SqlServer'; rid = 'linux-arm64' },
        @{ provider = 'Sqlite'; rid = 'win-x64' },
        @{ provider = 'PostgreSql'; rid = 'win-x64' }
    )
    foreach ($variant in $variants) {
        $provider = $variant.provider
        $rid = $variant.rid
        $buildRoot = Join-Path $workspace "build/$provider-$rid"
        $properties = @('-p:Configuration=Release', "-p:RuntimeIdentifier=$rid", "-p:DeliveryProvider=$provider",
            '-p:UseArtifactsOutput=true', "-p:ArtifactsPath=$buildRoot", '-p:ArtifactsPivots=release',
            "-p:PathMap=$buildRoot=/_/delivery-build", '-p:GeneratePackageOnBuild=false', '-p:NuGetAudit=false')
        Write-Output "Preparing delivery-test kit $provider/$rid"
        Invoke-DotNet (@('restore', $deliveryProject, '--verbosity', 'minimal') + $properties)
        Invoke-DotNet (@('build', $deliveryProject, '--no-restore', '--verbosity', 'minimal') + $properties)
        $pathsJson = (& dotnet msbuild $deliveryProject @properties '-getProperty:TargetDir,ProjectAssetsFile' '-verbosity:quiet' | Out-String)
        if ($LASTEXITCODE -ne 0) { throw "Cannot read SDK output paths for $provider/$rid." }
        $paths = ($pathsJson | ConvertFrom-Json -AsHashtable).Properties
        & (Join-Path $PSScriptRoot 'Assemble-Delivery.ps1') -BuildOutput $paths.TargetDir -AssetsFile $paths.ProjectAssetsFile `
            -Destination (Join-Path $preparedRoot "sdk/$provider/$rid") -Provider $provider -Rid $rid -Configuration Release `
            -SourceRevision "working-tree-$($initialFingerprint.agentBridge)" -EfRevision "working-tree-$($initialFingerprint.efCoreLibrary)" `
            -HttpRevision "working-tree-$($initialFingerprint.httpClientLibrary)" -SdkVersion $sdkVersion
    }
    foreach ($provider in @('SqlServer', 'Sqlite', 'PostgreSql')) {
        & (Join-Path $PSScriptRoot 'Assemble-NuGetDelivery.ps1') -SourceRoot (Join-Path $preparedRoot "sdk/$provider") `
            -Destination (Join-Path $preparedRoot "nuget/$provider")
    }
    & (Join-Path $PSScriptRoot 'Assemble-SharedDelivery.ps1') -SourceRoot (Join-Path $preparedRoot 'sdk/SqlServer') `
        -Destination (Join-Path $preparedRoot 'shared/SqlServer')

    $finalInput = Get-SourceFingerprint
    if ($finalInput.fingerprint -cne $initialFingerprint.fingerprint) {
        throw 'Source files changed during delivery preparation; build the tests again.'
    }
    $ready = [ordered]@{ schemaVersion = 1; fingerprint = $initialFingerprint.fingerprint; sdkVersion = $sdkVersion; configuration = 'Release' }
    [IO.File]::WriteAllText((Join-Path $preparedRoot 'ready.json'), ($ready | ConvertTo-Json) + "`n", [Text.UTF8Encoding]::new($false))
    $previousRoot = Join-Path $artifactsRoot "previous-$runId"
    if (Test-Path -LiteralPath $currentRoot) {
        if ((Get-Item -LiteralPath $currentRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Current delivery-test directory cannot be a reparse point.'
        }
        Move-Item -LiteralPath $currentRoot -Destination $previousRoot
    }
    Move-Item -LiteralPath $preparedRoot -Destination $currentRoot
    Remove-OwnedDirectory $previousRoot
    Remove-OwnedDirectory $workspace
    Write-Output "Delivery tests ready: $currentRoot"
} finally {
    Pop-Location
    $preparationLock.Dispose()
}
