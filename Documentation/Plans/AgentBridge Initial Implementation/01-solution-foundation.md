# 01 — Основа решения

Статус: **Завершён и принят** (2026-10-03). Зависимости: **00 — завершён и принят**.

## Дополнительная проверка 2026-10-04

Адресно восстановлены и собраны текущие три test projects со всеми production ProjectReference, включая обе migrations assemblies и фактические библиотеки. Итоговые builds: 0 warnings/errors; nullable/XML-docs/root compile excludes сохранены. Root csproj/slnx не перемещались и не изменялись; приложения и hosting отсутствуют в запуске. Результаты ядра 121, CodexLb 61, isolated persistence 164 и integration 39: [команды и окружение](README.md#дополнительный-интеграционный-запуск-2026-10-04). История первоначальной подготовки ниже сохранена; пауза после 13 не снята.

## Цель

Создать границы SDK-style проектов .NET 10 для согласованной архитектуры библиотеки.

## Задачи

- [x] Сохранить существующее ядро/прикладной слой в корне; добавить отдельные проекты адаптера codex-lb и хранения с явными ссылками на ядро.
- [x] Добавить три проекта изолированных тестов в `tests`, вне production-каталогов.
- [x] Проверить отсутствие зависимостей ASP.NET Core, WPF и Telegram по вычисленным references ядра.
- [x] Включить генерацию XML-документации и nullable во всех проектах; исключить вложенные результаты сборки, адаптеры и тесты из glob ядра.
- [x] Добавить локальные `AGENTS.md` обоих адаптеров и тестов; обновить корневую карту проектов.
- [x] Закрепить XML summary на русском и `<inheritdoc/>` для реализаций интерфейсов в инструкциях областей.

## Проверка и завершение

Перед разрешённой проверкой компиляции проверить ссылки проектов и хуки сборки. Этап завершён, когда сборки конкретных проектов соблюдают архитектуру без запуска приложения.

## Фактический результат

`agent-bridge.csproj` не переносился и не создавался заново. В `agent-bridge.slnx` зарегистрированы ядро, `AgentBridge.CodexLb`, `AgentBridge.Persistence.EfCore` и три соответствующих тестовых проекта. Production-проекты пустые, без типов этапов 02+ и без внешних пакетов. Тестовая инфраструктура: Microsoft.NET.Test.Sdk 18.0.1, xUnit 2.9.3, runner Visual Studio 3.1.5; тестовых сценариев пока нет.

До restore/build проверены все `.csproj`, родительские каталоги и доступные NuGet-настройки. Применимых `Directory.Build.props/targets`, `Directory.Packages.props`, project NuGet.config, lock-файлов и пользовательских build-hooks не найдено. Проверены импорты выбранных тестовых пакетов и generated NuGet imports; произвольных Exec нет. Внешние источники не использовались: restore выполнен из локального NuGet-кэша, сетевой audit отключён только параметром команды.

Рабочий каталог всех команд: `D:\Media\User\source\repos\agent-bridge`. SDK: `10.0.401`. Для каждого из шести путей ниже последовательно выполнены **обе** команды (подстановка `<project>` — точный путь из таблицы):

```powershell
dotnet restore <project> --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet build <project> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
```

| `<project>` | Restore | Compile-check Debug |
| --- | --- | --- |
| `.\agent-bridge.csproj` | Успешно | 0 ошибок, 0 предупреждений |
| `.\adapters\AgentBridge.CodexLb\AgentBridge.CodexLb.csproj` | Успешно | 0 ошибок, 0 предупреждений |
| `.\adapters\AgentBridge.Persistence.EfCore\AgentBridge.Persistence.EfCore.csproj` | Успешно | 0 ошибок, 0 предупреждений |
| `.\tests\AgentBridge.Tests\AgentBridge.Tests.csproj` | Успешно | 0 ошибок, 0 предупреждений |
| `.\tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj` | Успешно | 0 ошибок, 0 предупреждений |
| `.\tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj` | Успешно | 0 ошибок, 0 предупреждений |

Дополнительно для каждого проекта выполнено вычисление MSBuild items без запуска targets:

```powershell
dotnet msbuild <project> -getProperty:TargetFramework,OutputType,Nullable,GenerateDocumentationFile,_Xunit_ImportPropsFile,_Xunit_ImportTargetsFile -getItem:ProjectReference,PackageReference,FrameworkReference,Reference,Compile
```

У ядра нет ProjectReference, PackageReference и явных Reference; единственный FrameworkReference — неявный `Microsoft.NETCore.App`. У адаптеров по одной ссылке на ядро, у каждого тестового проекта — на свою проверяемую сборку. Nullable и XML-docs включены во всех шести проектах. Дополнительные xUnit imports не заданы. Тестовый SDK стандартно формирует entry point для runner; собственных приложений и host-проектов нет, entry point не запускался.

После сборок повторное вычисление `Compile` ядра не включает ни одного исходника из вложенных проектов или результатов сборки. Проверены шесть путей проектов в solution, 80 локальных Markdown-ссылок и UTF-8/LF всех 18 изменённых/добавленных файлов; U+FFFD и четырёх вопросительных знаков нет. `git diff --check` прошёл без ошибок; Git сообщает лишь о возможном будущем преобразовании LF в CRLF при следующей записи согласно локальной настройке. Файлы оставлены с LF.

## Ограничения

- Приложение/hosting, TestServer/WebApplicationFactory, реальные HTTP-запросы, БД/SQL, migrations и backup/restore: **Пропущено по указанию пользователя**.
- Обычный runner `dotnet test`/xUnit для изолированных unit-тестов разрешён. На этапе 01 тестовых сценариев ещё нет, поэтому runner не запускался; пройденные тесты не заявляются, проверена компиляция тестовой инфраструктуры.
- HttpClientLibrary, EFCoreLibrary и провайдеры не подключены; известные gaps этапов 04/05 здесь не реализуются. Соседние репозитории не изменены.
- Поведение и нормативный OpenSpec не изменены. CLI OpenSpec и проектные скрипты не запускались; проверка требований к архитектуре выполнена статически.
- Этапы 00–01 завершены и приняты; этапы 02–25 не начаты. Ветка сохранена.

Источник: [архитектура](<../../Technical documentation/01-architecture.md>).
