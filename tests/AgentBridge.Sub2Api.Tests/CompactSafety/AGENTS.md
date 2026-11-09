# Безопасный отказ compact

- Actual Sub2Api gateway/ContextCompactor/AgentRunner с existing fake HTTP и in-memory storage; без БД, live HTTP, host и процессов.
- Проверять блокировку native trigger и compact-окна до factory/callbacks, включая родное provenance и неоднозначные type. Текст сообщения с такими словами не является протокольным сигналом.
- Полная история, tool pairs, fixed expiry и отсутствие compact save/retry обязательны. Disabled/Unsupported не означают успешный compact или пригодность окна после server recovery.
- Serialized canonical roundtrip не является проверкой persistence на БД. Успешная fixture не закрывает T05 и не подтверждает deployed pool.
