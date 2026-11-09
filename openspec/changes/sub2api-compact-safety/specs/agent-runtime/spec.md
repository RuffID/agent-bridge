## ADDED Requirements

### Requirement: Безопасная граница compact sub2api

Коннектор sub2api MUST сохранять режим полного canonical контекста без создания или активации compact-окна. Automatic compact MUST оставаться отключённым; manual/force CompactAsync MUST возвращать Unsupported до HTTP factory и записи окна. GenerateAsync MUST отклонять input items с type compaction_trigger, compaction или compaction_summary до HTTP factory и callbacks, даже с родным connector provenance. Проверка MUST учитывать каждое поле type при повторяющихся именах, не меняя canonical JSON. Клиент MUST NOT удалять эти items, восстанавливать prefix автоматически, подменять compact генерацией/текстовым summary, повторять запросы/business tools или менять доступ/модель/коннектор.

#### Scenario: Native v2 не является обычной генерацией

- **WHEN** input содержит compaction_trigger в любой позиции, с callback либо без него
- **THEN** возвращается Unsupported без HTTP и callbacks; canonical input остаётся неизменным
- **AND** отсутствие callback MUST NOT запускать legacy body-signal compact, а наличие callback MUST NOT запускать native v2.

#### Scenario: Родное окно не доказывает сохранность covered context

- **WHEN** input содержит compaction либо compaction_summary, в том числе после сериализованного восстановления с родным provenance
- **THEN** generation возвращает Unsupported до HTTP; окно и полная исходная история не удаляются и не заменяются хвостом
- **AND** Completed, HTTP 200, requested model echo, provenance и save-before-activation MUST NOT считаться доказательством устранения T05.

#### Scenario: Повторный force и границы хранения

- **WHEN** compact запрашивается повторно либо диалог истёк
- **THEN** новое окно не отправляется, не сохраняется и не активируется; существующие token, covered prefix, полная история и fixed expiry не изменяются.

#### Scenario: Обычная генерация с полным контекстом

- **WHEN** threshold достигнут, compact items отсутствуют и отдельный generation budget guard допускает запрос
- **THEN** передаются все canonical items в исходном порядке, включая function_call/function_call_output, без compact dispatch/save
- **AND** ServerValidationWithUnknownWindow MUST сохранять null окна без guessed budget; server rejection, HTTP 200 со stream failure, malformed и EOF MUST NOT запускать retry или обрезание истории.
