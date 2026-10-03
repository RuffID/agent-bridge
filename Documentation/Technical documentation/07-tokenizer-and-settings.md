# Tokenizer, настройки и Serilog

## Tokenizer

Tokenizer разбивает текст на элементы словаря модели — токены — и позволяет посчитать их число. Токен может быть словом, частью слова, знаком пунктуации или частью числа. Количество символов и слов не определяет точное количество токенов.

Tokenizer — тип компонента, а не имя отдельного framework. Для AgentBridge подключается подходящая .NET-библиотека токенизации за `IContextTokenCounter`. Выбор конкретной библиотеки и проверка кодировок моделей входят в [этап tokenizer](<../Plans/AgentBridge Initial Implementation/17-model-tokenizer.md>); ни один package пока не объявлен выбранным или установленным.

AgentBridge использует tokenizer, соответствующий известной кодировке выбранной модели, через `IContextTokenCounter`. Подсчёт учитывает сериализуемые инструкции, сообщения, описания инструментов и текстовые результаты. Для модели без проверенного соответствия кодировки нельзя выдавать произвольный tokenizer за точный счётчик.

Видимое число токенов tokenizer не раскрывает содержимое непрозрачного reasoning/compaction и не определяет точную стоимость изображений. При расчёте полного контекста учитываются доступные серверные сведения и запас бюджета. Подсчёт видимого текста и итоговый входной бюджет различаются в возвращаемых данных.

После каждого успешного compact пересчитывается новое рабочее окно. В одном обращении разрешено не более настроенного числа проходов; повторение останавливается при достижении целевого размера или отсутствии уменьшения. Последующие обращения могут снова выполнить compact. Бесконечного цикла «сжимать, пока получится» нет.

Если рабочее окно не укладывается в допустимый входной бюджет, библиотека сообщает явную невозможность отправки. Она не скрывает превышение и не обрезает произвольно обязательные инструкции или связку инструмента с результатом.

## Проектируемые контракты

Ниже рабочие названия будущих типов; исходный код ещё не создан.

| Тип | Обязанность |
| --- | --- |
| `AgentSettingsSnapshot` | Безопасное представление модели, effort и лимитов |
| `AgentSettingsService` | Чтение и изменение выбора модели/effort с проверкой каталога |
| `DialogStatus` | `CreatedAtUtc`, `ExpiresAtUtc`, объём, размер контекста, состояние и предупреждения |
| `IApiKeyProvider` | Выбор индивидуального ключа или общего ключа при его отсутствии |
| `IContextTokenCounter` | Подсчёт по tokenizer и раздельное представление известного/непрозрачного бюджета |

Приоритет effort: override запроса, затем настройка выбранного агента, затем настроенное значение по умолчанию. Итог проверяется по доступной модели и политике ключа. Для одного выполняющегося обращения используется фиксированный снимок настроек.

Авторизация на чтение настроек и смену модели определяется приложением. Контракт чтения не возвращает секреты. `IApiKeyProvider` не является сохранением ключа внутри диалога: приложение владеет источником секретов.

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
