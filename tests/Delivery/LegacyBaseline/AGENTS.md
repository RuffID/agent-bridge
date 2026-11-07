# Явная проверка исторического baseline

- Проект намеренно не включён как Project в solution: обычный набор не требует внешних исторических файлов.
- Путь задаётся только AgentBridgeLegacyBaselineRoot при сборке и сохраняется в AssemblyMetadata. Нет default пути профиля, поиска, восстановления или Skip при отсутствии файлов; ошибка входов должна быть явной.
- Только read-only SHA256/size и PE metadata. Сначала проверяются pinned hashes evidence этапа01, затем описи127/112 и frozen consumers; target DLL не загружаются и не исполняются.
- Embedded текстовый API-эталон общий с обычными Sub2Api tests. Он получен только из проверенных старых DLL; новые исходники не являются источником expected. При расхождении тест показывает различие, но не меняет fixture или старый kit.
- Это не old→new CLR ABI resolution и не runtime приёмка. Нет HTTP, БД, Docker, host или процессов из тестов.
- Restore/build/test конкретного проекта по csharp-project-rules, GeneratePackageOnBuild=false. Команды и границы — в ../README.md.
