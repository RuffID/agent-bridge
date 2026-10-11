# Точные команды упаковки DLL

Дата: 11.10.2026. Четыре команды отдельно разрешены пользователем и выполнены. Три SDK kits созданы (114/112/112 файлов); Assemble-NuGetDelivery.ps1 отказал до создания destination: RID-specific local library HttpClientLibrary.dll. Эти inputs не являются общей согласованной DLL-поставкой.

Причина: отдельные ArtifactsPath записаны в CodeView PDB paths собственных DLL. Коррекция штатными ArtifactsPivots=release и PathMap, отдельно разрешённые новые команды и успешная поставка — [Packaging-coherent-commands.md](Packaging-coherent-commands.md). Старые комплекты не подменялись; этот файл сохраняет evidence первого запуска, его команды повторять не требуется.

Inputs: fresh Release SDK Build, SDK 10.0.401, EF 10.0.12 / SqlClient 7.0.2; restore только C:/Users/Spike/.nuget/packages. Каждый RID имеет собственный ArtifactsPath. Команды читают deps/assets/PE/XML/package originals и генерируют новые комплекты; не запускают DLL/native/БД/restore/host. Старый kit не изменяется.

Provenance — SHA256 production source/build inputs (.cs/.csproj/.props/.targets/global.json, без tests/*.Tests/bin/obj/artifacts). Абсолютные пути, возвращённые Windows rg, сортируются StringComparer.Ordinal до нормализации разделителей; затем строки relativePath с `/`|size|lowercase fileSha256, LF + final LF, UTF-8. Это working-tree hash, не Git revision. Повторная проверка тем же алгоритмом 11.10 подтвердила все три hash; сортировка уже нормализованных relative paths является другим алгоритмом.

- D:/Media/User/source/repos/agent-bridge: 306 files, SHA256 `bf1e862704860907070a27c48022c086a104619b5a0084e9a31eed7152113364`.
- D:/Media/User/source/repos/work/EFCoreLibrary: 85 files, SHA256 `5506e0b3e26b244c3e2cfed1fae67e3a09a7b334032ad20432d0b41f06824f72`.
- D:/Media/User/source/repos/work/HttpClientLibrary: 21 files, SHA256 `0b05b22203da9ba841e9598b4a08a88cd2f1fe3d2f202a1b33c084f19ec55e74`.

Рабочая директория: `D:/Media/User/source/repos/agent-bridge`.

## win-x64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/win-x64/bin/AgentBridge.Delivery/release_win-x64" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/win-x64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011/sdk/SqlServer/win-x64" -Provider SqlServer -Rid win-x64 -Configuration Release -SourceRevision "working-tree-sha256:bf1e862704860907070a27c48022c086a104619b5a0084e9a31eed7152113364" -EfRevision "working-tree-sha256:5506e0b3e26b244c3e2cfed1fae67e3a09a7b334032ad20432d0b41f06824f72" -HttpRevision "working-tree-sha256:0b05b22203da9ba841e9598b4a08a88cd2f1fe3d2f202a1b33c084f19ec55e74" -SdkVersion 10.0.401
```

## linux-x64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/linux-x64/bin/AgentBridge.Delivery/release_linux-x64" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/linux-x64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011/sdk/SqlServer/linux-x64" -Provider SqlServer -Rid linux-x64 -Configuration Release -SourceRevision "working-tree-sha256:bf1e862704860907070a27c48022c086a104619b5a0084e9a31eed7152113364" -EfRevision "working-tree-sha256:5506e0b3e26b244c3e2cfed1fae67e3a09a7b334032ad20432d0b41f06824f72" -HttpRevision "working-tree-sha256:0b05b22203da9ba841e9598b4a08a88cd2f1fe3d2f202a1b33c084f19ec55e74" -SdkVersion 10.0.401
```

## linux-arm64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/linux-arm64/bin/AgentBridge.Delivery/release_linux-arm64" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/linux-arm64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011/sdk/SqlServer/linux-arm64" -Provider SqlServer -Rid linux-arm64 -Configuration Release -SourceRevision "working-tree-sha256:bf1e862704860907070a27c48022c086a104619b5a0084e9a31eed7152113364" -EfRevision "working-tree-sha256:5506e0b3e26b244c3e2cfed1fae67e3a09a7b334032ad20432d0b41f06824f72" -HttpRevision "working-tree-sha256:0b05b22203da9ba841e9598b4a08a88cd2f1fe3d2f202a1b33c084f19ec55e74" -SdkVersion 10.0.401
```

## Полный DLL/XML/props/manifest bundle

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-NuGetDelivery.ps1" -SourceRoot "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011/sdk/SqlServer" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011/SqlServer"
```

