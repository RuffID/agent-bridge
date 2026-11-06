# Baseline и воспроизводимая проверка совместимости

[Контракт API](legacy-api-baseline.md) · [Машинная выжимка](baseline-evidence.json) · [Этап01](main.md)

## Что сохранено

До любых transport/DI изменений создан отдельный каталог:

`C:\Users\Spike\AppData\Local\AgentBridge\CompatibilityBaselines\sub2api-2026-11-06`

Он находится вне всех рабочих репозиториев и не заменяется подготовкой `artifacts/delivery-tests/current`. Это локальные baseline материалы; они не добавлены в исходники/Git. Для переноса на другую машину нужно переносить весь каталог с проверкой preservation.json и baseline-evidence.json. Потеря локального каталога означает потерю бинарного baseline, а не разрешение пересобрать «старую DLL» из новых исходников.

| Путь внутри baseline | Происхождение / состав |
| --- | --- |
| kit/ | Точная копия SDK SqlServer/win-x64/Release/net10.0 комплекта: DLL/XML, выбранный native runtime asset, satellite resources, deps evidence, props и manifest. 112 manifest entries проверены до копирования по size/SHA256; дополнительные companion files также включены в preservation.json. |
| source-ready.json | Копия ready.json действующей поставки: SDK10.0.403 и fingerprint 1ad73d56ceb4db7abaf2bd63c19280c8b8cf3ec9c7267ce4c3655288385e080b. |
| preservation.json | Опись первоначально сохранённых127 файлов с relative path/size/SHA256, originalKit и source provenance. Сам manifest не выдаётся за независимую цифровую подпись. |
| pe-metadata.json | Read-only PE extraction собственных AgentBridge assemblies: identities, AssemblyReferences, top-level public types, public/protected method signature blobs, параметры, свойства/поля. Для полной семантики signatures необходимо читать сохранённую DLL: raw blobs содержат локальные metadata tokens. |
| original-consumer/ | Все13 исходных .cs/.csproj файлов tests/Delivery/Consumer, скопированные **до** добавления LegacyTransportSurfaceProbe. Compile-only library, без ProjectReference и без выполнения методов. Generated obj/artifacts добавились только при разрешённой сборке. |
| surface-consumer/ | Тот же consumer плюс новый LegacyTransportSurfaceProbe.cs: прямые AddCodexLb*, ctor, Generate/Compact/settings и auxiliary signatures. Полнота ABI не выводится из одного probe. |
| frozen-binaries/original/AgentBridge.BinaryConsumer.dll | Исходный consumer, скомпилированный против baseline kit до рефакторинга; не перекомпилировать при ABI проверке. |
| frozen-binaries/surface/AgentBridge.BinaryConsumer.dll | Расширенный consumer против того же baseline kit; покрывает дополнительные direct references. |

Исходные fingerprints рассчитаны отдельными read-only командами SHA256 по алгоритму прочитанного Prepare-DeliveryTests.ps1 (сам файл не выполнялся): путь + hash содержимого, сортировка CaseSensitive, исключения generated/docs/tests соседних библиотек, root build inputs. Совпали agentBridge=92817551086a4491990344c09e0d366c5a62d2b556fb29999eb47ccbc498b46f, EF=f99e9dea1504f7103c8f8d15dd2f2ab0f6d24682b32d37da21e748e66a0295d4, HTTP=19cee37265352c2c41a2f5003202c964e953aa4f7eda0f8a69a575bf6b04a9f0 и составной ready fingerprint. Это совпадение working-tree inputs, не commit hash. Новый compile-only probe входит в последующие inputs; прежний fingerprint не надо выдавать за текущий fingerprint после его добавления.

## Выполненные команды

Перед запуском прочитаны .csproj, импортируемые SDK delivery props/variant props, root global.json и отсутствие Directory.Build.props/targets/NuGet.config/locks в соответствующей цепочке внешнего проекта. Импортируемый custom target содержит только Error-проверку RID, без Exec. App/metadata test project с BeforeCompile подготовкой поставки не собирался.

Команды выполнялись из `D:\Media\User\source\repos\agent-bridge`. Для каждого из двух значений `<consumer>`: `original-consumer` и `surface-consumer` выполнены:

```powershell
dotnet restore 'C:\Users\Spike\AppData\Local\AgentBridge\CompatibilityBaselines\sub2api-2026-11-06\<consumer>\AgentBridge.BinaryConsumer.csproj' --source 'C:\Users\Spike\.nuget\packages' -p:NuGetAudit=false -p:GeneratePackageOnBuild=false -p:AgentBridgeDeliveryRoot='C:\Users\Spike\AppData\Local\AgentBridge\CompatibilityBaselines\sub2api-2026-11-06\kit' -p:RuntimeIdentifier=win-x64
dotnet build 'C:\Users\Spike\AppData\Local\AgentBridge\CompatibilityBaselines\sub2api-2026-11-06\<consumer>\AgentBridge.BinaryConsumer.csproj' -c Debug --no-restore -p:BaseOutputPath='.\artifacts\compile-check\' -p:GeneratePackageOnBuild=false -p:AgentBridgeDeliveryRoot='C:\Users\Spike\AppData\Local\AgentBridge\CompatibilityBaselines\sub2api-2026-11-06\kit' -p:RuntimeIdentifier=win-x64
```

`<consumer>` — обозначение двух конкретных путей, не буквальная команда для копирования. Обе restore и обе build завершились Exit0; обе сборки дали 0 warnings / 0 errors. DLL не исполнялись. SDK kit импортирует готовые бинарные References, источники библиотек/ProjectReference в этой компиляции не используются.

Read-only команды: rg/Get-Content/Get-ChildItem, SHA256 fingerprints и проверка manifest entries; PEReader; XML/JSON/link/encoding validation документов. Copy-Item сохранил baseline, целевые библиотеки не загружались. Тесты, live HTTP, OAuth, native loading, DI resolution, БД/Docker, приложения, Git и проектные scripts/CLI OpenSpec не запускались.

## Как проверять после этапов02–05

1. Сначала проверить preservation.json и hashes baseline-evidence.json; сохранить frozen binaries без перекомпиляции. Новый kit должен иметь собственные fresh source provenance/manifests и оставаться отдельно от baseline. Не копировать новые DLL поверх старых.
2. **Source compatibility:** скопировать неизменённые original-consumer source/.csproj в новый внешний каталог, compile против нового kit теми же restore/build командами с заменёнными путями и отдельным output. Так же проверить surface-consumer. Нельзя менять исходник ради green, добавлять ProjectReference либо считать linked source тест доказательством DLL-поставки. Расширить проверку на ранее заявленную матрицу пяти provider/RID в05.02.
3. **ABI:** сравнить старые и новые PE semantically: assembly name/version/culture/public-key identity, AssemblyRefs, fully qualified declaring types, base types/interfaces, generic arity/constraints, method static/instance/virtual shape, ctor/return/parameter/ref/nullable/source defaults и property accessors/enum values. Использовать сохранённые DLL/XML как input metadata, не только число public types или raw blob equality: tokens могут перенумероваться. Type moves требуют разрешения прежнего assembly-qualified имени (wrapper/проверенный forwarder), а не просто доступности одноимённого нового типа.
4. Для двух **неперекомпилированных** frozen consumer DLL разрешить все их TypeRef/MemberRef/MethodSpec через metadata нового kit и framework references. Проверить сигнатуры, generic instantiations и доступность. Это воспроизводимый статический ABI критерий выбранных consumers; полный API comparison покрывает публичные члены, которыми они не пользуются. Existing Metadata tests уже читают PE/Roslyn, но такой проверки old-binary→new-kit сейчас в них нет. Её нужно реализовать/выполнить в05.02; сборка consumer заново не заменяет её.
5. **Runtime compatibility:** отдельно проверить разрешённым изолированным Windows consumer загрузку/DI/lifetimes/options/overrides на новых DLL; Linux compile не подтверждает Linux runtime. Existing Compatibility/Runtime проверки не запускались и не считаются green только по наличию исходников.
6. **Server compatibility:** отдельно разрешённая05.03 с ключом sub2api и OAuth upstream, подтверждёнными budgets, несколькими turns/tools/encrypted reasoning/compact/restart/error/cancel. Ни source compile, ни PE comparison не заменяет этот пункт.

Обратная совместимость с новой поставкой пока не проверялась: новой поставки нет. На этапе01 создан воспроизводимый исходный baseline, а не заявление об успешной будущей ABI/runtime приёмке. Strict OpenSpec validation нового change также остаётся на05.01/отдельное разрешение; main spec не синхронизируется.
