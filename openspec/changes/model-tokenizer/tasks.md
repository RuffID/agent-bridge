# Задачи этапа17

- [x] Зафиксировать первичные источники model→encoding, стабильную NuGet-зависимость и offline словари.
- [x] Реализовать IContextTokenCounter с exact mapping, whole prepared request, KnownTokens/nullable estimate/opaque и caller cancellation.
- [x] Добавить input_context_window guard с configured reserve/threshold и запретом неизвестного полного бюджета.
- [x] Проверить полноценный tokenizer на известных vectors, всей композиции, schema/results/parameters, opaque/unknown, равенствах/переполнении и отмене.
- [x] Выполнить адресные restore/build/tests и записать исходные failures и финальные результаты.
- [x] Синхронизировать main spec/context, бизнес/техничку, README, ближайшие AGENTS и существующий план.
- [ ] Выполнить strict OpenSpec CLI validation при доступном CLI; недоступность не считать успехом и change не архивировать.
- [x] Передать полный manifest/checks/limitations координатору; получить приёмку и разрешение локального коммита ровно31 утверждённого файла. CLI limitation остаётся невыполненной, change не архивировать.
