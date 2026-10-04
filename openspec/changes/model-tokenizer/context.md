# Решение и границы

Microsoft.ML.Tokenizers2.0.0 и data packages O200kBase/Cl100kBase2.0.0 предоставляют BPE tokenizer и embedded словари, без runtime download. Mapping берётся из OpenAI tiktoken model.py; библиотечный эвристический resolver не применяется. Exact ID из конечного проверенного списка не расширяются произвольными prefixes.

KnownTokens — сумма токенизированных известных payload: инструкции, текст сообщений/результатов, arguments, names/descriptions и JSON схем. JSON framing всего известного input/tool/input controls используется только как локальная оценка (не server/billing count), не меньше известной суммы. Model/effort, continuation IDs и транспортные controls не объявляются текстом input. Reserve применяется guard отдельно, не добавляется в KnownTokens.

Opaque reasoning/compaction, multimodal/files, неизвестные items/fields/controls и continuation со скрытым серверным состоянием делают полную оценку неизвестной. Известный текст остаётся доступным, но guard не объявляет неизвестный бюджет допустимым. Новых opaque estimates или server-count API нет.

Это поведение встроенного offline counter. Независимый IContextTokenCounter/ContextTokenCount сохраняет существующую возможность обоснованной полной оценки приложения при HasOpaqueContent=true; DI custom counter сохраняется, guard проверяет наличие estimate, не требует HasOpaqueContent=false. Новый источник такой оценки на17 не реализуется.

Например, короткое новое сообщение с длинными инструкциями и schemas считается вместе с ними; изображение рядом с текстом сохраняет KnownTokens текста, EstimatedInputTokens=null и требует явного отказа guard. Композиция/история не меняются и не обрезаются. Threshold достигается при estimate >= threshold; reserve+estimate == input_context_window допустимо по локальной оценке. Это не гарантия принятия сервером.
