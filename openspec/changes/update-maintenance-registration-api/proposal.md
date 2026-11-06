# Совместимость с обновлёнными библиотеками

## Why

EFCoreLibrary 0.0.9 удаляет MaintenanceExecutionMode и аргумент режима из AddRelationalMaintenance. AgentBridge после обновления зависимостей не компилируется.

## What Changes

- Удалить аргумент режима из двух перегрузок AddAgentBridgeDatabaseMaintenance и всех текущих вызовов.
- Сохранить библиотечный singleton gate, scoped coordinator, валидацию backup и отсутствие I/O при регистрации.
- Обновить изолированные проверки и примеры. Ответственность приложения за единственного исполнителя обслуживания сохраняется.

## Impact

Несовместимое изменение исходного API: потребители удаляют аргумент MaintenanceExecutionMode.SingleInitializer и пересобираются с текущими DLL. Схема БД и сценарии обслуживания не меняются.
