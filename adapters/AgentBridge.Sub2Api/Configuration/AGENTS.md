# Композиция sub2api

- Bind/validation читает только Sub2Api, без I/O и без выдачи значений в ошибках. Existing bundle и app-owned HTTP factory сохраняются.
- Gateway получает отложенный доступ к scoped HttpApiClient и IContextContentInspector ядра: не вызывать factory до проверки key/endpoint/controls/continuation и connector provenance opaque input. Facade регистрирует inspector через AddAgentBridgeRunner; регистрация не создаёт HTTP-клиент. После разрешённого dispatch использовать только HttpClientLibrary.
- Endpoints принимают root/prefix либо один конечный /v1; неоднозначные маршруты не угадываются. Регистрация не выполняет запросы и не создаёт ServiceProvider.
