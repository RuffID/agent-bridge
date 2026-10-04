## ADDED Requirements

### Requirement: Автономный комплект DLL

Поставка AgentBridge MUST содержать ядро, CodexLb-адаптер, общее EF-хранилище, выбранную SQLite/PostgreSQL migrations assembly и полную runtime closure для явно указанного RID, включая обязательные EFCoreLibrary/HttpClientLibrary, tokenizer data и native assets. Состав MUST фиксироваться manifest с версиями и SHA256. Поставка MUST NOT требовать путей к дереву исходников или NuGet-публикации AgentBridge.

#### Scenario: Компиляция бинарного потребителя

- **WHEN** .NET10-потребитель и комплект скопированы за пределы дерева исходников
- **THEN** потребитель компилируется только с бинарными ссылками на комплект, без ProjectReference и PackageReference
- **AND** runtime/native файлы доставляются в output стандартной сборкой; compile-check не объявляется доказательством их загрузки.

### Requirement: Документация бинарных контрактов

Поставка MUST включать сгенерированные XML-файлы AgentBridge рядом с соответствующими DLL и доступные XML зависимостей. Проверка MUST подтверждать наличие описаний публичных контрактов и metadata-связь inheritdoc реализации с документированным контрактом. Неразвёрнутый inheritdoc MUST NOT объявляться готовым текстом для любой IDE. Generated XML MUST NOT редактироваться вручную.

#### Scenario: Реализация интерфейса в отдельной сборке

- **WHEN** потребитель читает metadata CodexLb-реализации и XML из комплекта
- **THEN** доступны inheritdoc реализации и русский summary соответствующего интерфейса ядра без исходников.

### Requirement: Явные внешние требования поставки

Документация MUST различать SQLite/PostgreSQL, выбранную migrations assembly, RID/native runtime, .NET10 runtime и внешние PostgreSQL dump-утилиты. Она MUST сохранять явную конфигурацию provider, SingleInitializer и отсутствие автоматического обслуживания при подключении DLL.

#### Scenario: PostgreSQL backup

- **WHEN** приложение выбирает PostgreSQL maintenance
- **THEN** оно отдельно предоставляет pg_dump с согласованным major, абсолютными путями и конечным cleanup timeout; наличие DLL не заменяет утилиту или права сервера.
