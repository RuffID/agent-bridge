# B03: точные команды упаковки

Дата: 11.10.2026. **Все четыре точные команды прямо разрешены пользователем и успешно выполнены.** Destinations уже существуют; повторять запуск не требуется. Source SDK kits:114/112/112 файлов; полный schema3 kit:10 DLL/9XML/3props/12 roots. SDK outputs собраны local-cache-only в Release; PathMap использует абсолютный Windows path с обратными слешами, чтобы CodeView paths всех RID совпадали. Prepare-DeliveryTests.ps1 не запускался.

Рабочая директория: `D:/Media/User/source/repos/agent-bridge`. Скрипты проверены статически: читают готовые SDK outputs/assets, создают новый kit и hashes/props/manifest; не запускают DLL/native/restore/HTTP/БД/hosting. Снимки inputs и алгоритм SHA-256 находятся в `artifacts/b03-bounded-history-20261011/{agentbridge,ef,http}-source-inputs.sha256`, fingerprints — в `source-fingerprints.json`. Это hashes текущих файлов, а не Git revision.

## win-x64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/sdkbuild/SqlServer-win-x64/bin/AgentBridge.Delivery/release" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/sdkbuild/SqlServer-win-x64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/sdk/SqlServer/win-x64" -Provider SqlServer -Rid win-x64 -Configuration Release -SourceRevision "working-tree-sha256:e43e8d956c1904a33c68f48148429451780a0daa152ac43dd0fad9f014398b1c" -EfRevision "working-tree-sha256:1739badc30f5db15982b85fbaae61a5ef5789b9f36095b11aa7c2053f642f252" -HttpRevision "working-tree-sha256:c58b6aca2c3454ca48f0763c3fa2819b1a74df86abde52411e791474822049de" -SdkVersion 10.0.401
```

## linux-x64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/sdkbuild/SqlServer-linux-x64/bin/AgentBridge.Delivery/release" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/sdkbuild/SqlServer-linux-x64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/sdk/SqlServer/linux-x64" -Provider SqlServer -Rid linux-x64 -Configuration Release -SourceRevision "working-tree-sha256:e43e8d956c1904a33c68f48148429451780a0daa152ac43dd0fad9f014398b1c" -EfRevision "working-tree-sha256:1739badc30f5db15982b85fbaae61a5ef5789b9f36095b11aa7c2053f642f252" -HttpRevision "working-tree-sha256:c58b6aca2c3454ca48f0763c3fa2819b1a74df86abde52411e791474822049de" -SdkVersion 10.0.401
```

## linux-arm64

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-Delivery.ps1" -BuildOutput "D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/sdkbuild/SqlServer-linux-arm64/bin/AgentBridge.Delivery/release" -AssetsFile "D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/sdkbuild/SqlServer-linux-arm64/obj/AgentBridge.Delivery/project.assets.json" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/sdk/SqlServer/linux-arm64" -Provider SqlServer -Rid linux-arm64 -Configuration Release -SourceRevision "working-tree-sha256:e43e8d956c1904a33c68f48148429451780a0daa152ac43dd0fad9f014398b1c" -EfRevision "working-tree-sha256:1739badc30f5db15982b85fbaae61a5ef5789b9f36095b11aa7c2053f642f252" -HttpRevision "working-tree-sha256:c58b6aca2c3454ca48f0763c3fa2819b1a74df86abde52411e791474822049de" -SdkVersion 10.0.401
```

## DLL/NuGet bundle

```powershell
pwsh -NoProfile -File "D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/Assemble-NuGetDelivery.ps1" -SourceRoot "D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/sdk/SqlServer" -Destination "D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/SqlServer"
```

После упаковки выполнены external compile-only consumer с новым BoundedHistoryRegistration.cs и **полным kit/props**, locked restore/build трёх RID из cache;29 isolated binary cases и семь negative exact/framework/RID/lock guards. Generic Metadata csproj/Prepare-DeliveryTests hooks не запускались: отдельный external binary project проверяет именно новый SQL Server kit, actual bounded query paths и metadata без packaging scripts. Fresh результаты — Evidence.md.

Полный kit `D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/SqlServer`; manifest SHA-256 `fd66709eb625acc138b90ed5da1834bf0a988bb10258630ef43ecb4a84dac633`.
