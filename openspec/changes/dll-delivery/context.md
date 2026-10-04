# Контекст поставки24

Baseline: master `7e9d533d80393bd85b08e1b17567038e12ec8a16`, parent22 `89c28e839bf12584027b81494c31557c91279935`; tree/index чистые. EFCoreLibrary0.0.5 HEAD `3a8a53187af3c5df049770dfd6727b5065159d1f`, HttpClientLibrary FileVersion0.0.0.5 HEAD `6d0528d940d1d8494c722c22464051dd961d6bf7`; read-only.

## Решение и причина

Общий EF-адаптер имеет статические ссылки на оба EF provider и maintenance-модуля. Исключение невыбранного provider из комплекта требует отдельного изменения архитектуры; в24 сохраняется вся runtime closure, но поставляется только выбранная AgentBridge migrations DLL. Например, SQLite-комплект также содержит Npgsql, но не PostgreSQL migrations.

Подготавливается win-x64-комплект для текущей Windows-среды. Другие RID требуют отдельной сборки и проверки; win-x64 native DLL не объявляется переносимой. Стандартный SDK-проект агрегирует зависимости без собственных targets/Exec. Его служебная DLL не является частью поставки.

## Проверка и ограничения

Потребитель — библиотека без entry point и PackageReference, с импортом локального файла бинарных ссылок. Копия исходников потребителя размещается рядом с копией комплекта в отдельном временном каталоге вне репозиториев. Сборка не исполняет DI, HTTP, EF, tokenizer или native code.

XML копируется без ручного изменения. Metadata/XML-проверка отличает непосредственные summary, inheritdoc и существующий контракт интерфейса; она не является проверкой отображения IntelliSense конкретной IDE. Если внешняя библиотека не имеет исходных summary, генерация пустого XML не создаёт описаний.

## Передача

Точные команды, manifest и фактические результаты фиксируются в [плане24](<../../../Documentation/Plans/AgentBridge Initial Implementation/24-dll-delivery.md>). Руководство полного сценария использования и закрытие плана относятся к25.

Подготовлены два комплекта по39 managed DLL/35 XML/1 native asset; оба внешних compile-only потребителя собраны без warnings/errors.8 metadata/XML tests passed,0 failed/skipped. EFCoreLibrary CRUD XML исходный проект не генерирует; SQLitePCLRaw также не предоставляет XML. Изменять read-only библиотеки для выдуманного заполнения описаний не потребовалось. OpenSpec CLI отсутствует; change не архивирован. Этап24 принят координатором после независимой проверки code/docs, manifest24, обоих payload manifests, TRX8/0/0 и receipts206=39kit+167framework. Разрешён локальный English Conventional Commit ровно24 файлов manifest с parent7e9d533d80393bd85b08e1b17567038e12ec8a16. Full hash — в git log и итоговом ответе;25 продолжит отдельный исполнитель координатора.
