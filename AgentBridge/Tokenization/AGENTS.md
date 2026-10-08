# Offline tokenization

- ContextTokenCounter также реализует IContextContentInspector через тот же canonical shape parser, без BPE/model mapping. Это только opaque/unknown classification, не input estimate. Текстовая совместимость не зависит от поддержки exact model tokenizer; CountAsync неизвестной модели по-прежнему Unsupported. DI registration сохраняет отдельные custom counter/inspector.

- Здесь реализация IContextTokenCounter через Microsoft.ML.Tokenizers2.0.0 и embedded O200kBase/Cl100kBase словари. Runtime network/download отсутствует. Mapping конечный, exact/ordinal, первичные источники и дата в техничке07; библиотечный model resolver/prefix fallback не применять.
- Точный gpt-5.5 добавлен для existing Telegram default: существование подтверждено official model docs и local codex-lb registry; encoding — official tiktoken main gpt-5 family rule (2026-10-08). Runtime prefix matching по-прежнему запрещён, arbitrary suffix и gpt-5.4 не добавлены. Каталог/effort/input window проверяются отдельно.
- KnownTokens считает известные payload, не billing. JSON framing — только локальная оценка полного известного input. Opaque/multimodal/unknown fields или continuation state => estimate null, известный текст сохраняется. Не токенизировать IDs/base64/encrypted content как скрытый input.
- Reserve/threshold принадлежат Application.ContextBudgetGuard, не BPE tokenizer. Counter/guard не меняют историю, не вызывают compact/HTTP/DB. Не вводить opaque estimates без согласования.
- Literal special-token markers являются обычным пользовательским текстом, не protocol special tokens. Изолированные проверки public counter/guard/DI — tests/AgentBridge.Tests/ContextTokenCounterTests.cs и ContextBudgetGuardTests.cs.
