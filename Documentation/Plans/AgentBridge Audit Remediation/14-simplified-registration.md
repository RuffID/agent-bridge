# 14 — Единая регистрация и короткое подключение

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **не начат**. Зависимости: 12,13; принятые исправления relevant runner/transport. Область: отдельный integration adapter и consumer docs.

## Цель и исходное состояние

Свести стандартное подключение к одной групповой регистрации с IConfiguration приложения. Сейчас AddServerAgentBridge в README — собственная обёртка приложения, собирающая много public регистраций вручную; одноимённого универсального API библиотеки нет.

## Работы

1. Создать отдельный SDK library проект `adapters/AgentBridge.Integration/` с зависимостями на ядро, CodexLb и persistence adapters, nearest AGENTS и записью в `.slnx`. Core не ссылается на этот facade или ASP.NET Core.
2. Спроектировать и реализовать одну публичную IServiceCollection extension для привязки выбранного IConfiguration и стандартной DI composition. Название/сигнатуру документировать как actual API только после реализации; предполагаемое AddAgentBridge пока не выдавать за существующий метод.
3. Собирать options, persistence, transport/library pipeline, diagnostics, registry, counter/guard, builder/compactor, runner/settings/cleanup. Использовать app-owned HttpClient factory; согласовать и проверить management/scopes, сохранить explicit custom implementations через соответствующий TryAdd контракт.
4. Явно определить базовый сценарий без business tools/context providers и с общим ключом: он должен подключаться без обязательных пустых app классов. Если индивидуальные keys включены, отсутствие app key source отклонять; не скрывать ошибку регистрацией source=null.
5. Оставить advanced extension points для ordered context providers, scoped tools/validators, HTTP и settings/compatibility ports. Проверить повторную регистрацию, lifetimes/captive dependencies и порядок выбора зависимостей.
6. Не включать создание собственного logger, файлов, auth policies, пользовательских owner/permissions, app endpoints, migrations, maintenance commands или scheduler. Требуемые app integrations документировать коротко и отдельно от стандартной настройки библиотеки.
7. Переписать основной вход README вокруг actual короткого MSSQL сценария после реализации. Развёрнутые HTTP endpoints/admin examples оставить в отдельном/раскрываемом руководстве; одновременно обновить compile-only consumers и guide25.

## Проверки

B: public facade через in-memory configuration/DI, required settings13, common/individual key modes, отсутствие I/O при регистрации, scopes/overrides, default empty tools/providers без удаления обычной dialog history.

Compile-check самого facade и external binary consumer без ProjectReference/PackageReference во всех kits16. Не запускать ASP.NET Core host/HTTP маршруты ради DI проверки.

## Критерии завершения

Одна actual стандартная registration подключает базовый сценарий; приложение передаёт config/логирование и отдельно определяет свои business boundaries. Сложность advanced integrations не спрятана в ложные defaults. README и compile-only examples соответствуют реализованной сигнатуре.

## Результаты

Facade, упрощённое API и проверки ещё не реализованы.
