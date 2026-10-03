# 04 — Развитие контракта логирования HttpClientLibrary

Статус: **Завершён и принят; запрещённые проверки пропущены** (2026-10-03). Зависимости: **00, 03**. Координатор принял исходники, документацию, diff, тесты и результаты проверок. Пользователь разрешил правки HttpClientLibrary; координатор поручил отдельные локальные коммиты по явным спискам файлов.

## Цель

Добавить в общую HTTP-библиотеку уже согласованное безопасное логирование по умолчанию.

## Задачи

- [x] Согласовать точный контракт конфигурации и получить разрешение на правки соседней библиотеки.
- [x] Реализовать безопасные метаданные по умолчанию и opt-in JsonStructure без исходных имён и значений; текст содержимого и sanitizer не входят в согласованный объём.
- [x] Сохранить статус ошибки и ограниченные данные, отдельно согласованную копию error headers/body с явной полнотой, без raw данных в логе.
- [x] Проверить сохранение JSON, потоковой выдачи, отмены и владения содержимым.
- [x] Сверить исходный контракт и подготовленные DLL библиотеки для будущего использования AgentBridge; фактического подключения адаптера ещё нет.

## Фактический результат

- До согласования статически подтверждены raw URL/reason/snippet в Warning, 2000-char preview без redaction, отсутствие error headers и URL/reason в Exception.Message. После предложения пользователь отдельно разрешил правки и точный объём ниже; исходные отчёты 00–03 не изменены.
- HttpClientLibrary: FileVersion 0.0.0.5, прежние net8.0/net10.0 и Logging Abstractions 10.0.2. Старые конструкторы сохранены, новые четвёртый/пятый параметры обязательны; вызов с третьим null однозначен.
- HttpClientLoggingOptions: None по умолчанию, opt-in JsonStructure. HttpResponseDiagnostics пишет 5100/HttpResponseReceived только с закрытым методом, числовым статусом, request/correlation GUID, видом ответа и длительностью до заголовков. Статус HTTP не выдаётся за завершение JSON/SSE. URL/reason/headers/body/Exception не логируются.
- JsonStructure пишет лишь фиксированное состояние, RootKind и счётчики узлов полного валидного JSON. Исходные имена/значения отсутствуют. Неполный/невалидный JSON не выдаётся за полный. Успешный SSE не читается ради диагностики. SanitizedText и sanitizer отсутствуют.
- HttpRequestFailedException.Message только HTTP <status>.; прежние raw свойства сохранены. ErrorResponse содержит независимую неизменяемую копию обеих областей headers, BodyText и состояние Empty/Complete/Truncated/UnsupportedContent/InvalidEncoding. MaxBodyBytes по умолчанию 65536; читается максимум лимит + 1 байт с cancellation. Truncated сохраняет декодируемый prefix без незавершённого UTF-8 символа, реальные неверные байты явно InvalidEncoding.
- Поддерживается строгий UTF-8 (BOM допустим), text/*, application/json, *+json и отсутствующий Content-Type. Иные объявленные кодировки/типы дают UnsupportedContent. Raw details не являются безопасными для журналов. Полнота не обещает валидность JSON; type/code/param, server correlation и Retry-After остаются будущему адаптеру.
- Добавлены локальные AGENTS библиотеки, pipeline и тестов; обновлены README библиотеки, актуальные документы/навигаторы AgentBridge и spec/context. Production-код и project-файлы AgentBridge не менялись; EFCoreLibrary и codex-lb не изменены. Этапы 05–25 не начаты.

## Фактические проверки

Команды выполнялись из `D:\Media\User\source\repos\work\HttpClientLibrary`, SDK 10.0.401. Перед restore/build/test прочитаны csproj, Directory.Build.props, родительские build/config пути и пакетные imports. Проектных Exec/внешних hooks нет; xUnit targets генерируют entrypoint и копируют runner dependencies. Тесты используют stub handler, память и cancellation, без приложения, сети, БД или процессов из тестов. Restore — только локальный кэш, NuGetAudit=false.

```powershell
dotnet restore .\HttpClientLibrary.Tests\HttpClientLibrary.Tests.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet build .\HttpClientLibrary.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\HttpClientLibrary.Tests\HttpClientLibrary.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test .\HttpClientLibrary.Tests\HttpClientLibrary.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:TestTfmsInParallel=false
```

| Проверка | Результат |
| --- | --- |
| Restore библиотеки и тестов обоих TFM | Успех, локальный кэш |
| Compile-check библиотеки net8/net10 | 0 ошибок, 0 предупреждений |
| Первые compile-check/test тестового проекта | 0 ошибок/предупреждений; по 40 passed на каждом TFM |
| Compile-check после дополнения регрессий | CS9343 в новом FailingStream test double; исправлен отсутствующий список параметров primary constructor, тесты до исправления не запускались |
| Итоговый compile-check тестов и зависимой библиотеки | 0 ошибок, 0 предупреждений на обоих TFM |
| Итоговые изолированные тесты | net8: 44 passed; net10: 44 passed; failed/skipped 0; 5 прежних + 39 новых случаев на каждом TFM |

Проверены default/JsonStructure, sentinels в URL/userinfo/query/reason/headers/body/имени поля и отсутствие Exception/state утечек, byte limits/EOF/UTF-8/BOM/invalid encoding, headers включая одинаковые имена двух областей, JSON/string/no-body/204/205/stream, отсутствие pre-read SSE, caller cancellation, I/O failure, disposal, старые конструкторы и wrappers. Runtime-тесты проверяют FileVersion/TFM/MVID и наличие прежних сигнатур загруженной DLL. API клиента и исключения не требует прикладного host.

Статически проверены 20 изменённых/новых файлов HttpClientLibrary и 12 файлов AgentBridge: строгая UTF-8-декодировка, отсутствие U+FFFD, четырёх вопросительных знаков, проверенных маркеров mojibake и trailing whitespace. В документах AgentBridge сохранён LF; существующие строки библиотеки не подвергались общей нормализации окончаний. Локальные Markdown-ссылки AgentBridge ведут на существующие файлы. git diff --check обоих репозиториев прошёл; предупреждения autocrlf не являются ошибками diff. Подтверждены неизменность HEAD/ветки master AgentBridge, отсутствие правок отчётов 00–03 и статусы «Не начат» всех 21 этапов 05–25. Для review подготовлены полные diff, включая новые файлы библиотеки, в игнорируемом artifacts AgentBridge.

### Происхождение DLL

PE metadata прочитаны без загрузки или исполнения production DLL. Build-артефакты из `artifacts/compile-check/Debug/<TFM>/HttpClientLibrary.dll` и копии тестового output `HttpClientLibrary.Tests/artifacts/compile-check/Debug/<TFM>/HttpClientLibrary.dll` имеют одинаковый SHA-256 внутри каждого TFM. Assembly identity обоих: HttpClientLibrary, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null; FileVersion 0.0.0.5. Эти DLL получены адресной сборкой текущих исходников, без публикации.

| TFM | MVID | SHA-256 |
| --- | --- | --- |
| net8.0 (.NETCoreApp,Version=v8.0) | 8b5b9d33-87ff-408e-a934-13f25b42ee45 | D3F6F3C01BBB666F39535741892D5E154B9816A6A8CCCB0C05D0AA46A0720EE7 |
| net10.0 (.NETCoreApp,Version=v10.0) | 165cc2e7-964b-421f-a563-87dd85ace328 | 43788278E7F4CAA773DC891A98CE24CC2E2E75B504B30063AC80819305DC808F |

## Непроведённые проверки и ограничения

- Адаптер AgentBridge.CodexLb пока не ссылается на HttpClientLibrary. Provenance относится к библиотеке и тестовым копиям, не к запущенному приложению/адаптеру. Проверки AgentBridge не повторялись: изменена только его документация.
- Чтение ошибок теперь ограничено байтами, может ожидать до 65536 + 1 байта вместо прежних 2000 символов; caller отвечает за cancellation/deadline. Старая универсальная замена неверных байтов больше не маскирует InvalidEncoding, иная объявленная кодировка не декодируется как UTF-8. Message и формат/частота логов изменены намеренно.
- Приложение, hosting, произвольные проектные скрипты, OpenSpec CLI, реальные HTTP/codex-lb/OpenAI, БД/SQL, migrations и backup/restore: **Пропущено по указанию пользователя**. Автоматическая OpenSpec-валидация не заявляется.
- HttpClientLibrary зафиксирована локальным коммитом `6d0528d940d1d8494c722c22464051dd961d6bf7` (`feat: add safe HTTP diagnostics and bounded error details`), ровно 20 согласованных файлов. Перед коммитом проверены status/diff/staged diff и точный список; после коммита рабочее дерево библиотеки чистое. Координатор разрешил коммит ровно 12 файлов документации AgentBridge; его hash возвращается в итоговом отчёте, не записывается заранее в собственный коммит. Diff artifacts/manifest/build outputs исключены; author identity не менялась, remote/push/PR отсутствуют. Следующий этап не начат.

## Проверка и завершение

Выполнить разрешённые изолированные тесты с подставными HTTP-обработчиками и сохранением логов. Этап завершён, когда успешные, ошибочные и потоковые ответы соблюдают контракт без замены HTTP-слоя внутри AgentBridge.

Источник: [HTTP-контракт](<../../Technical documentation/03-http-and-codex-lb.md>).
