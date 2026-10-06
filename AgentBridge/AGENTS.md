# Ядро AgentBridge

- `agent-bridge.csproj` — SDK-style библиотека `net10.0`, сборка `AgentBridge.dll`, пространство имён `AgentBridge`.
- `Application/`, `Domain/`, `Configuration/`, `Diagnostics/` и `Tokenization/` сохраняют свои локальные границы в ближайших `AGENTS.md`.
- Адаптеры и тесты находятся в соседних папках `../adapters/` и `../tests/`; ядро не ссылается на них. Compile glob охватывает только эту папку, исключая вложенные `bin`, `obj` и `artifacts`.
- Compile-check выполняется для `AgentBridge/agent-bridge.csproj` от корня решения. Изолированные проверки ядра находятся в `../tests/AgentBridge.Tests/`.
