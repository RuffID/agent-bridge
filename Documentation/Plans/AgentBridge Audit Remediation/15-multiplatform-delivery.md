# 15 — Поставка win-x64, linux-x64 и linux-arm64

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **не начат**. Зависимости: 12–14. Область: delivery composition/metadata/consumer, без автоматического запуска.

## Цель и исходное состояние

Подключение AgentBridge на .NET10 к трём платформам приложения, основной provider MSSQL. Сейчас delivery Build проект и native/PE checks рассчитаны на win-x64, а PostgreSQL-kit также содержит статически связанные SQLite assets. Такие native DLL нельзя переносить на Linux.

## Работы

1. Сделать RID явным параметром delivery assembly resolution и manifests: поддержать win-x64/linux-x64/linux-arm64 без fallback на Windows assets. Сохранить framework-dependent DLL model; .NET runtime устанавливает приложение.
2. Выделить SQL Server kit в каждой из трёх RID комбинаций, с actual integration facade14/migrations/dependency closure. Existing SQLite/PostgreSQL paths сохранять и проверять адресно; не выдавать их новую cross-platform матрицу за автоматически проверенную.
3. Сверить actual package/runtime assets всего графа, включая транзитивные SqlClient/SQLite зависимости. Не удалять statically referenced DLL/native assets вручную ради меньшего комплекта; разделение provider modules — отдельный согласованный шаг, если оно действительно требуется.
4. Генерировать managed/XML/native props и manifests для каждого RID. Пути и filename case должны работать на Linux; не встраивать absolute Windows paths. Корректно классифицировать PE managed metadata и PE/ELF native assets, сохраняя architecture verification.
5. Подготовить compile-only binary consumer для каждого kits/RID с общей registration14 и IConfiguration13. Не исполнять примеры при этой проверке; ARM64 artifact resolution на Windows не считать ARM64 runtime acceptance.
6. Обновить техническое руководство DLL, usage guide и README после фактической реализации. Указывать separate app RID/dependency graph и незапущенный runtime18. AOT, trimming и single-file не входят в автоматически согласованный объём.

## Проверки

B в16: актуальные assets, hashes, dependency closure, XML и binary consumer для трёх MSSQL kits. Restore может использовать сеть и требует правил/разрешения; production packaging scripts не запускать автоматически.

Runtime в18: actual Windows x64, Ubuntu x64 и Linux ARM64 среды; подключение к разрешённому remote SQL Server отдельно17/19. Отсутствующая ARM64 машина остаётся runtime gap, а не successful cross-platform выводом.

## Критерии завершения

Три корректных комплекта с совпадающими managed APIs и платформенными dependencies; compile-only consumers подтверждены. Нет смешения PE/ELF и архитектур, stale manifests или ложного обещания SQL Server Engine ARM64.

## Результаты

Мультиплатформенная поставка и её проверки ещё не выполнялись.
