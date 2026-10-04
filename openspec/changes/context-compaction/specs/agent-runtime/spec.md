## ADDED Requirements

### Requirement: Отдельный JSON compact transport

Compact gateway MUST отправлять canonical base-prefix POST /v1/responses/compact через HttpClientLibrary с per-call ModelAccess и конечным CompactTimeout. MUST сохранять exact model/effort, instructions, ordered input и unknown/opaque поля. MUST NOT подменять compact генерацией, повторять запрос или менять доступ. Continuation и неподдержанные параметры MUST отклоняться до HTTP. Compact MUST NOT создавать generation continuation из id ответа.

#### Scenario: Каноническое окно без status

- **WHEN** JSON object discriminator после trim начинается с response.compact, status отсутствует/null, output является массивом объектов и error отсутствует/null
- **THEN** gateway возвращает Completed с полными output/envelope и null continuation.

#### Scenario: Явный отказ или неизвестный lifecycle

- **WHEN** ответ содержит error либо status=failed
- **THEN** gateway сохраняет output/envelope как Failed с безопасной ошибкой
- **AND** иной неподтверждённый status или отсутствующий output даёт Incomplete; некорректная JSON-форма отклоняется.

### Requirement: Ошибки и отмена compact

Compact MUST сохранять правила безопасных HTTP ошибок и caller/deadline JSON transport. Explicit failure MUST иметь приоритет над поздней отменой; caller cancellation после полного отчёта MUST сохранять output/envelope в Canceled. Неожиданный I/O MUST распространяться. HTTP request/response MUST освобождаться на всех путях. Raw headers/body/reason/exception message MUST NOT публиковаться или логироваться как ошибка.

#### Scenario: Поздняя отмена

- **WHEN** caller отменён после полного compact JSON
- **THEN** известный результат сохраняется как Canceled, если ранее не установлен explicit failure.

### Requirement: Сохраняемый префикс и фиксированный полный запрос

Прикладной compact MUST использовать только активное окно и следующий непрерывный terminal prefix, включая0. InProgress и последующий хвост, provider items и новый несохранённый input MUST оставаться вне сохраняемого окна. Полный request MUST учитывать их при threshold/budget и после замены окна; providers MUST вызываться один раз на сценарий. Instructions/tools/controls MUST сохраняться в полном generation request. Compact MUST использовать отдельную проекцию поддержанных controls без tools. Известные неполные function pairs MUST отклоняться до отправки, без удаления данных.

#### Scenario: Завершённая история с текущим хвостом

- **WHEN** первый turn terminal, второй InProgress, а приложение добавило provider и новый input
- **THEN** compact получает только активное окно и ещё не покрытый первый turn
- **AND** следующий полный request содержит provider, новый compact output, второй turn и новый input ровно по одному разу.

### Requirement: Ограниченное принятие compact

Сценарий MUST проверять exact settings, считать полный request и запускать compact при estimate >= threshold. Unknown estimate MUST NOT заменяться KnownTokens либо прошлым usage. Число проходов MUST ограничиваться MaxPasses; known non-reduction MUST останавливать проходы без принятия увеличенного окна. Валидный Completed кандидат с unknown full estimate MUST сохраняться и возвращать UnknownBudget без дальнейших проходов или разрешения генерации. Пустой output при непустой compact history MUST отклоняться без сохранения. Успех SaveAsync MUST предшествовать активации. Save MUST получать исходный token либо результат предыдущего save и свежий UTC; now >= expiry, stale token, incomplete/failed/canceled и ошибки MUST сохранять последнее успешно принятое окно без retry. Внешний I/O MUST завершаться вне write UoW. Исходная история и fixed expiry MUST сохраняться. Compact payload MUST проходить отдельную проверку input budget перед HTTP. Статус отчёта MUST NOT объявляться разрешением generation без отдельного full-request guard.

#### Scenario: Неизвестный opaque бюджет

- **WHEN** Completed output имеет opaque состояние и full counter возвращает null estimate
- **THEN** version-aware save принимает окно и сценарий возвращает UnknownBudget
- **AND** guard17 по-прежнему отклоняет генерацию без полной оценки.

#### Scenario: Сбой второго прохода

- **WHEN** первый проход сохранён, а второй завершился ошибкой либо потерял актуальность
- **THEN** активным остаётся окно первого прохода, ошибка сохраняется без автоматического повторения.
