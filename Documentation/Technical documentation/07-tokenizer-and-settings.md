# Tokenizer, настройки и Serilog

## Tokenizer

Tokenizer разбивает текст на элементы словаря модели — токены — и позволяет посчитать их число. Токен может быть словом, частью слова, знаком пунктуации или частью числа. Количество символов и слов не определяет точное количество токенов.

Этап17 реализует `AgentBridge.Tokenization.ContextTokenCounter : IContextTokenCounter` через стабильную `Microsoft.ML.Tokenizers 2.0.0` с embedded `Microsoft.ML.Tokenizers.Data.O200kBase/Cl100kBase 2.0.0`. Native .NET BPE работает без сети. Прямой `Microsoft.Bcl.Memory 10.0.4` исправляет уязвимую транзитивную9.0.4 data packages ([advisory](https://github.com/advisories/GHSA-73j8-2gch-69rq)); первоначальный restore warning и результаты записаны в [этапе17](<../Plans/AgentBridge Initial Implementation/17-model-tokenizer.md>).

AgentBridge использует tokenizer, соответствующий известной кодировке выбранной модели, через `IContextTokenCounter`. Подсчёт учитывает сериализуемые инструкции, сообщения, описания инструментов и текстовые результаты. Для модели без проверенного соответствия кодировки нельзя выдавать произвольный tokenizer за точный счётчик.

Видимое число токенов tokenizer не раскрывает содержимое непрозрачного reasoning/compaction и не определяет точную стоимость изображений. При расчёте полного контекста учитываются доступные серверные сведения и запас бюджета. Подсчёт видимого текста и итоговый входной бюджет различаются в возвращаемых данных.

После каждого успешного compact пересчитывается новое рабочее окно. В одном обращении разрешено не более настроенного числа проходов; повторение останавливается при достижении целевого размера или отсутствии уменьшения. Последующие обращения могут снова выполнить compact. Бесконечного цикла «сжимать, пока получится» нет.

Если рабочее окно не укладывается в допустимый входной бюджет, библиотека сообщает явную невозможность отправки. Она не скрывает превышение и не обрезает произвольно обязательные инструкции или связку инструмента с результатом.

## Проектируемые контракты

Этап13 реализует независимые IIndividualModelKeySource/IModelAccessResolver/IModelCatalog/IModelSettingsReader, ModelCapabilities/ModelCatalogSnapshot/ModelSettingsSnapshot и ModelSelectionValidator. Это чтение/проверка модельных настроек, без их сохранения или статуса диалога. [Точные API, бюджет и безопасные ошибки](13-model-catalog-and-keys.md). Offline tokenizer и guard реализованы17; управление настройками21 ещё отсутствует.

`IContextTokenCounter.CountAsync(ModelRequest, CancellationToken)` получает весь prepared ModelRequest. `ContextTokenCount` разделяет KnownTokens, nullable EstimatedInputTokens и HasOpaqueContent. Реализация17 доступна через `AddAgentBridgeTokenization`; [фактические порты](09-application-ports.md). AgentSettingsSnapshot/AgentSettingsService/DialogStatus ниже остаются будущими типами.

### Проверенный exact mapping и источники

Все перечисленные источники прочитаны **2026-10-04**; mapping не является каталогом доступности. Actual catalog/effort/InputContextWindow всё равно проверяются отдельно по13. Настройка обязательной default model у приложения не заменяется.

| Exact ID | Encoding | Подтверждение |
| --- | --- | --- |
| `gpt-5`, `gpt-4.1`, `gpt-4o`, `o1`, `o3`, `o4-mini` | `o200k_base` | Прямые MODEL_TO_ENCODING entries OpenAI tiktoken0.12.0 |
| `gpt-4`, `gpt-3.5-turbo` | `cl100k_base` | Прямые MODEL_TO_ENCODING entries того же source |

[OpenAI model.py, release0.12.0](https://github.com/openai/tiktoken/blob/0.12.0/tiktoken/model.py), SHA256 UTF-8 source `779ee48b1b24b08bfa5444a77558ef9860518fc2eb485180c14095e7166dc519`. Прочитан также actual main: прямые entries совпадают. Prefix resolver намеренно не используется: source прямо отмечает, что prefix способен принять несуществующий ID. Любые suffix/date/version, другой регистр, `gpt-5.4`, `gpt-6`/`gpt-6.1`, произвольные codex-lb aliases и fine-tuned IDs пока явно Unsupported. Пользователь не задал дополнительных обязательных ID; координатор подтвердил конечный прямой список до dependent mapping work. Это ограничение локального tokenizer, а не утверждение о недоступности модели сервером.

[OpenAI openai_public.py0.12.0](https://github.com/openai/tiktoken/blob/0.12.0/tiktoken_ext/openai_public.py), SHA256 source `954392738e60d0fb6dca1dad80872efc47c8e2733babecbbf0a23970ed66c2cb`, фиксирует canonical словари:

- cl100k SHA256 `223921b76ee99bde995b7ff738513eef100fb51d18c93597a113bcffe865b2a7`;
- o200k SHA256 `446a9538cb6c348e3516120d7c08b09f57c36495e2acfffe59a5bf8b0cfb1a2d`.

NuGet package nuspec связывает2.0.0 с machinelearning commit `efefa92f4486a43047c5b47618885a71bf7f0967` (release v5.0.0). [Pinned TiktokenTokenizer.cs](https://github.com/dotnet/machinelearning/blob/efefa92f4486a43047c5b47618885a71bf7f0967/src/Microsoft.ML.Tokenizers/Model/TiktokenTokenizer.cs), SHA256 `61c369f8d2d3198f33134865b0a1c2c5b021d888b134003d2e066188926a815f`, подтверждает regex и resource names. Factory использует именно эти regex с `RegexPreTokenizer(..., specialTokens:null)` и `TiktokenTokenizer.Create` без normalization/special tokens. Например literal `<|endoftext|>` — 7 ordinary токенов в обеих кодировках, не служебный токен протокола.

Factory зависит от pinned layout `cl100k_base.tiktoken.deflate`/`o200k_base.tiktoken.deflate` в соответствующих Data assemblies. [Pinned TokenizerData.targets](https://github.com/dotnet/machinelearning/blob/efefa92f4486a43047c5b47618885a71bf7f0967/eng/TokenizerData.targets) добавляет `Capacity:` header, удаляет ranks (rank = номер строки), затем deflate. Изолированный тест пропускает header, восстанавливает canonical token/rank строки с LF и проверяет оба OpenAI hashes. Прямой hash compressed или stripped bytes не равен hash canonical словаря. Missing resource/package, regex timeout и прочие неожиданные ошибки не скрываются fallback. При обновлении package необходимо повторить source/layout/regex/vocabulary checks.

[Pinned LruCache.cs](https://github.com/dotnet/machinelearning/blob/efefa92f4486a43047c5b47618885a71bf7f0967/src/Microsoft.ML.Tokenizers/Utils/LruCache.cs), SHA256 `e04fbd92732ceafb36f0d63d78018d303b0c42a62a98074943b0f3e9661c8fc4`: TryGetValue/Add используют `lock(SyncObj)`. CountTokens хранит рабочие данные в locals; vocab/decoder после построения читаются. Factory публикует два экземпляра через thread-safe Lazy, PayloadCount создаётся на вызов. Singleton reuse дополнительно проверен8 bounded concurrent workers; это не throughput benchmark.

### Подсчёт и оценка

KnownTokens — сумма BPE counts известных payload: Instructions, строковый message content либо input_text/output_text/refusal, function name/arguments, строковые/текстовые function results, tool name/description и вся JSON schema. Tool schema включает названия/описания/числа/default/enum, даже неизвестные schema keywords как обычные JSON данные. `parameters.text` (формат/JSON schema) и `tool_choice` также учитываются; input controls сохраняются для framing. Model ID/effort и transport controls parallel_tool_calls/include/service_tier/truncation/prompt_cache_key/reasoning.summary не объявляются input текстом. Guard не заменяет shape/support validation адаптера.

Если весь input известен, EstimatedInputTokens — max(KnownTokens, BPE(serialized instructions/input/tools/input parameters)) с compact JSON и неэкранированным Unicode. JSON framing включает structural/role/идентификационные поля только в оценке. Сумма отдельных payload BPE counts не является точным числом единого серверного input. Это локальная оценка без доказанного верхнего предела; сервер может считать иначе. [Официальная OpenAI Counting tokens](https://developers.openai.com/api/docs/guides/token-counting) прямо описывает ограничения local tiktoken для images/files/tools/schemas. Endpoint server count здесь не вызывается и не реализуется.

Encrypted reasoning/compaction, multimodal/file parts, unknown item/field/control/role, непустые annotations/logprobs и continuation дают HasOpaqueContent=true/EstimatedInputTokens=null. Известный текст остаётся доступным в KnownTokens. Base64, image/file IDs и encrypted data не токенизируются как скрытая модельная информация. Unknown data сохраняются в исходном request, не удаляются/не переписываются. Server usage прошлой генерации не выдается за оценку нового prepared request. Для opaque estimates новый контракт не вводится.

`ModelContinuation` — metadata, а не input item: например bound previous_response_id может дополнить новый input скрытой server history. Даже если local request уже содержит историю, counter не доказывает отсутствие дополнительного upstream state; поэтому continuation ID не прибавляется к KnownTokens, estimate=null. Пустой/неподдержанный metadata object также не является доказательством известного полного бюджета. Без continuation полностью текстовый request допускает framing estimate.

Caller cancellation проверяется до работы, между items/parts/controls, до/после каждого библиотечного BPE и перед возвратом. Синхронный BPE/JSON serialization одного большого payload нельзя прервать посередине; отмена наблюдается после него. Известные vectors regression: `Привет` =3 cl100k/2 o200k, `я` =1, `2 + 2 = 4` =7, `{"a":1}` =5; tokenizer не заменён character heuristic.

### Public guard и подключение

`AgentBridge.Application.ContextBudgetGuard(IContextTokenCounter)` предоставляет `CheckAsync(ModelRequest request, ModelSettingsSnapshot settings, CancellationToken cancellationToken = default)` → `ServiceResult<ContextBudgetAssessment>`. Exact request model/effort должен совпасть с settings; public snapshot повторно проходит ModelSelectionValidator. Guard использует только положительный InputContextWindow, настроенные TokenThreshold/InputTokenReserve. При estimate > window-reserve возвращает safe Rejected (без overflow), при estimate=null — Unsupported. Counter failure передаётся тем же ServiceError; поздняя отмена не заменяет failure, после success отмена проверяется.

Успех содержит count до резерва, окно/резерв и `ThresholdReached = estimate >= threshold`. Равенство estimate+reserve==window допустимо **по локальной оценке**, не гарантирует приём сервером. Assessment constructor защищает положительный window, nonnegative reserve<=window и известную estimate в пределах бюджета. Достижение threshold не запускает compact18. При overflow guard не обрезает instructions/history/tools.

```csharp
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using Microsoft.Extensions.DependencyInjection;

services.AddAgentBridgeTokenization(); // after configuration/catalog registrations приложения
// preparedRequest — результат ContextBuilder, settings — успешный snapshot IModelSettingsReader.
IContextTokenCounter counter = serviceProvider.GetRequiredService<IContextTokenCounter>();
ServiceResult<ContextTokenCount> counted = await counter.CountAsync(preparedRequest, cancellationToken);
ContextBudgetGuard guard = serviceProvider.GetRequiredService<ContextBudgetGuard>();
ServiceResult<ContextBudgetAssessment> checkedBudget = await guard.CheckAsync(preparedRequest, settings, cancellationToken);
```

TryAdd singleton counter/transient guard сохраняет явные регистрации приложения и повторный вызов не дублирует services. Регистрация не вызывает tokenizer/HTTP/DB и не запускает host. Guard вызывается явно приложением: gateway этапов14–15 пока не требует его, AgentRunner20 не реализован; автоматический запрет отправки в transport не добавлен. Например request с новым коротким текстом и длинной schema может выйти за лимит; изображение или continuation с тем же текстом у встроенного offline counter возвращает известную часть, но guard отказывает из-за null estimate. Независимый IContextTokenCounter/ContextTokenCount сохраняет существующую возможность обоснованной полной оценки приложения при HasOpaqueContent=true: guard проверяет estimate, а не требует false opaque. Новый источник такой оценки на17 не реализуется. [Нормативные требования](../../openspec/specs/agent-runtime/spec.md), [контекст решения](../../openspec/specs/agent-runtime/context.md).

| Тип | Обязанность |
| --- | --- |
| `AgentSettingsSnapshot` | Безопасное представление модели, effort и лимитов |
| `AgentSettingsService` | Чтение и изменение выбора модели/effort с проверкой каталога |
| `DialogStatus` | `CreatedAtUtc`, `ExpiresAtUtc`, объём, размер контекста, состояние и предупреждения |
| `IModelAccessResolver` (реализован этапом 13) | Индивидуальный ключ приложения или общий только при null, без повторов после ошибки |
| `IContextTokenCounter` | Подсчёт по tokenizer и раздельное представление известного/непрозрачного бюджета |

Приоритет effort: override запроса, затем настройка выбранного агента, затем настроенное значение по умолчанию. Итог проверяется по доступной модели и политике ключа. Для одного выполняющегося обращения используется фиксированный снимок настроек.

Авторизация на чтение настроек и смену модели определяется приложением. Контракт чтения не возвращает секреты. `IIndividualModelKeySource` принадлежит приложению; `IModelAccessResolver` не сохраняет ключ внутри диалога.

## Serilog

Логирование выполняется через Serilog, подключённый приложением к `Microsoft.Extensions.Logging`. На этапе 03 фактически доступна основа `AgentBridge.Diagnostics` в ядре: `AgentBridgeDiagnostics` получает `ILogger<AgentBridgeDiagnostics>` и создаёт наблюдения операций. Подключение будущих AgentRunner, HTTP и EF-инфраструктуры остаётся соответствующим этапам. Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md); назначение и ограничения: [контекст](../../openspec/specs/agent-runtime/context.md#реализованная-основа-диагностики).

Библиотека не заменяет global logger приложения и не выбирает самостоятельно файлы логов. Sinks, уровни, обогащение и срок хранения логов задаёт приложение.

### Регистрация приложения

`AddAgentBridgeDiagnostics` регистрирует стандартную инфраструктуру Microsoft.Extensions.Logging и singleton-службу диагностики через TryAdd. Повторный вызов не дублирует службу, существующие ILoggerFactory, провайдеры и фильтры сохраняются. Без провайдера диагностика разрешается из DI, но вывод не создаётся. В production подключён Microsoft.Extensions.Logging `10.0.3`; пакет Serilog принадлежит приложению.

Фрагмент composition root, в котором `services` и уже настроенный `applicationLogger` принадлежат приложению. Для `AddSerilog` приложение подключает Serilog.Extensions.Logging; изолированная проверка выполнена с версией `10.0.0` и Serilog `4.3.0`:

```csharp
using AgentBridge.Configuration;
using AgentBridge.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

services.AddLogging(logging => logging.AddSerilog(applicationLogger, dispose: false));
services.AddAgentBridgeConfiguration(agent => agent.MaxToolSteps = 5);
services.AddAgentBridgeDiagnostics();
```

`dispose: false` сохраняет владение Serilog logger у приложения: оно освобождает logger после своих потребителей. Выбор sinks и создание logger здесь не показаны, поскольку их политика зависит от приложения. Присваивание `Serilog.Log.Logger` для AgentBridge не требуется.

### Доступная диагностика

Публичная граница — `AgentBridgeDiagnostics.BeginOperation(AgentBridgeOperation, Guid, CancellationToken, CancellationToken)` и возвращаемый `AgentBridgeDiagnosticOperation`. Наблюдение не исполняет делегаты, не создаёт таймер, не управляет отменой и не перехватывает исключения. Нет автоматического успешного Dispose. Владелец явно вызывает `Complete()` или `Fail(Exception)` ровно один раз; повторное завершение даёт InvalidOperationException без второй записи.

Пример наблюдения уже реализованной проверки options после построения обычного DI-контейнера приложением. Это явный вызов приложения, а не автоматически подключённая диагностика options:

```csharp
AgentBridgeDiagnostics diagnostics = serviceProvider.GetRequiredService<AgentBridgeDiagnostics>();
AgentBridgeDiagnosticOperation observation = diagnostics.BeginOperation(
    AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid());
try
{
    serviceProvider.GetRequiredService<IStartupValidator>().Validate();
}
catch (Exception error)
{
    observation.Fail(error);
    throw;
}

observation.Complete();
```

Закрытый `AgentBridgeOperation` содержит ConfigurationValidation, ModelRequest, ContextCompaction, ToolExecution, DialogStorage и DatabaseMaintenance. Наличие имени не реализует соответствующий сценарий. Произвольные operation names, URL, configuration objects, status text и содержимое сообщений API не принимает. Пустой GUID и неизвестный enum отклоняются до создания наблюдения.

Итоговое событие `EventId = 4100`, `Name = AgentBridgeOperationCompleted`, категория `AgentBridge.Diagnostics.AgentBridgeDiagnostics`:

| Поле | Содержимое |
| --- | --- |
| `Operation` | Имя из закрытого enum |
| `OperationId` | Новый GUID для каждого наблюдения |
| `CorrelationId` | Переданный непустой GUID, общий для связанных операций |
| `Status` | Succeeded, Failed, Canceled или DeadlineExceeded |
| `ErrorCode` | None, UnexpectedFailure, UnattributedCancellation, CallerCanceled или DeadlineExceeded |
| `DurationMs` | Число миллисекунд, измеренное монотонным Stopwatch |

Стандартный `{OriginalFormat}` хранит шаблон Microsoft.Extensions.Logging; Serilog provider также добавляет SourceContext/EventId. Объект Exception всегда отсутствует в событии, даже при ошибке. `Fail` проверяет только принадлежность к OperationCanceledException: Message, ToString, Data, InnerException, имена пользовательских типов, URL и значения настроек не читаются и не форматируются. Ошибка возвращается или повторно выбрасывается самим владельцем; диагностика не заменяет исходное исключение.

### Caller cancellation и локальный deadline

`BeginOperation` получает исходный caller-токен и отдельный токен локального deadline. Для выполнения будущего HTTP допустим linked token, но для диагностической классификации передаются именно два исходных источника. Один cancelable token для обеих причин отклоняется. Диагностика не может распознать происхождение произвольно переданного linked token: соблюдение этого контракта лежит на владельце операции.

| Наблюдаемый результат | Статус / код | Уровень |
| --- | --- | --- |
| Подтверждённый успех (`Complete`) | Succeeded / None | Information |
| OperationCanceledException, caller отменён, включая одновременный deadline | Canceled / CallerCanceled | Information |
| OperationCanceledException, только локальный deadline отменён | DeadlineExceeded / DeadlineExceeded | Warning |
| OperationCanceledException без подтверждённого исходного источника | Failed / UnattributedCancellation | Error |
| Любая другая ошибка, включая TimeoutException | Failed / UnexpectedFailure | Error |

Поздняя отмена не переименовывает подтверждённый успешный результат. Обычная ошибка не становится отменой из-за сработавшего токена. Если будущий адаптер имеет отдельный явный контракт timeout, его сопоставление проектируется вместе с адаптером; одно имя исключения сейчас не выдаётся за доказанную причину.

### Проверенная граница и ограничения

18 новых изолированных проверок через DI/diagnostics проверяют структуру, длительность, корреляцию, отсутствие секретов в state/тексте/Exception, фильтры и фабрику приложения, раздельную отмену, повторное завершение и реальные события Serilog provider с sink в памяти. Вместе с прежними проверками настроек прошли 36 тестов ядра. Команды и результаты: [этап 03](<../Plans/AgentBridge Initial Implementation/03-serilog-integration.md>).

За свои ambient scopes, enrichers и журналы других компонентов отвечает приложение: события AgentBridge не очищают сторонний pipeline. Здесь нет режима вывода содержимого и фиктивных интеграций агента. AgentRunner, HTTP, хранилище и тестирование их реальных операций не реализованы на этапе 03.

По умолчанию журнал содержит безопасные идентификаторы корреляции, operation, status/code и длительность. На этапе 04 в HttpClientLibrary реализован согласованный None/opt-in JsonStructure: только структура HTTP-ошибки без исходных имён и значений. Текст содержимого и sanitizer не поддерживаются. Raw error details не логируются. Библиотека проверена отдельно; интеграция транспорта AgentBridge ещё не выполнена. Фактические события и transport error contract: [HTTP-контракт](03-http-and-codex-lb.md).

Связанные документы: [настройки приложения](<../Business logic/05-application-configuration.md>), [модели и состояние](<../Business logic/06-models-and-status.md>).
