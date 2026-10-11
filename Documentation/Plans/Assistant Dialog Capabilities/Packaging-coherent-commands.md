# Точные команды согласованной DLL-поставки

Дата: 11.10.2026. Все четыре команды отдельно разрешены пользователем и успешно выполнены. Созданы три SDK kits (114/112/112 файлов) и полный schema3 bundle с 10 local DLL, 9 штатными XML, 3 props, 12 required package roots. Manifest SHA256: `5fd8ddabe234857a68a79d49a74079e384920e7a122af7536dc54b7b97d9206a`. Повторный запуск не требуется; destination уже существует.

Причина: прежние отдельные ArtifactsPath попали в CodeView PDB paths собственных DLL. Штатный Prepare-DeliveryTests.ps1 использует общий ArtifactsPivots=release и PathMap=<variant buildRoot>=/_/delivery-build. Эти свойства применены к fresh local-cache restore/Release Build всех трёх RID без запуска Prepare-DeliveryTests.ps1. Все десять local DLL теперь имеют одинаковый SHA256 между RID, XML/graph проверяются упаковщиком. Production sources/provenance не изменились; hashes и алгоритм — [Packaging-commands.md](Packaging-commands.md).

Новые inputs: artifacts/assistant01a-release-coherent/<rid>/bin/AgentBridge.Delivery/release и obj/AgentBridge.Delivery/project.assets.json. Destination новый: artifacts/assistant01a-delivery-20261011-coherent. Упаковщики не изменены. Команды не запускают DLL/native/БД/сеть/restore/host.

Рабочая директория: D:/Media/User/source/repos/agent-bridge.

## win-x64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release-coherent/win-x64/bin/AgentBridge.Delivery/release" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release-coherent/win-x64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011-coherent/sdk/SqlServer/win-x64" -Provider SqlServer -Rid win-x64 -Configuration Release -SourceRevision "working-tree-sha256:bf1e862704860907070a27c48022c086a104619b5a0084e9a31eed7152113364" -EfRevision "working-tree-sha256:5506e0b3e26b244c3e2cfed1fae67e3a09a7b334032ad20432d0b41f06824f72" -HttpRevision "working-tree-sha256:0b05b22203da9ba841e9598b4a08a88cd2f1fe3d2f202a1b33c084f19ec55e74" -SdkVersion 10.0.401
```

## linux-x64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release-coherent/linux-x64/bin/AgentBridge.Delivery/release" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release-coherent/linux-x64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011-coherent/sdk/SqlServer/linux-x64" -Provider SqlServer -Rid linux-x64 -Configuration Release -SourceRevision "working-tree-sha256:bf1e862704860907070a27c48022c086a104619b5a0084e9a31eed7152113364" -EfRevision "working-tree-sha256:5506e0b3e26b244c3e2cfed1fae67e3a09a7b334032ad20432d0b41f06824f72" -HttpRevision "working-tree-sha256:0b05b22203da9ba841e9598b4a08a88cd2f1fe3d2f202a1b33c084f19ec55e74" -SdkVersion 10.0.401
```

## linux-arm64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release-coherent/linux-arm64/bin/AgentBridge.Delivery/release" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release-coherent/linux-arm64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011-coherent/sdk/SqlServer/linux-arm64" -Provider SqlServer -Rid linux-arm64 -Configuration Release -SourceRevision "working-tree-sha256:bf1e862704860907070a27c48022c086a104619b5a0084e9a31eed7152113364" -EfRevision "working-tree-sha256:5506e0b3e26b244c3e2cfed1fae67e3a09a7b334032ad20432d0b41f06824f72" -HttpRevision "working-tree-sha256:0b05b22203da9ba841e9598b4a08a88cd2f1fe3d2f202a1b33c084f19ec55e74" -SdkVersion 10.0.401
```

## Полный bundle

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-NuGetDelivery.ps1" -SourceRoot "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011-coherent/sdk/SqlServer" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011-coherent/SqlServer"
```

