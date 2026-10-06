# 18 — Runtime проверка DLL-комплектов

[Навигатор](README.md). Статус: **не начат**. Зависимости: 12–16, ресурсы **Q-002**, отдельное разрешение запуска consumer/native операций. Реальные MSSQL provider операции дополнительно зависят от17 и его разрешённых ресурсов.

## Цель и область

Проверить загрузку актуальных комплектов в целевом app dependency graph. Источник — [аудит13](<../AgentBridge Quality Audit/13-dll-delivery.md>) и Q-002. Старые framework-dependent net10.0/win-x64/Debug kits и PE/XML checks не доказывают runtime/native/DI совместимость.

## Работы

1. Проверить .NET10/configuration/app graph и hashes kits16 на **win-x64, linux-x64, linux-arm64**. Три RID входят в согласованный объём; каждую runtime среду подтвердить отдельно. Release и конкретные Ubuntu versions указать по actual deployment; AOT/trimming/single-file не становятся поддержанными автоматически.
2. Подготовить минимального выделенного потребителя с actual app dependency graph, без запуска production приложения. Изменение потребителя и его исполнение — разные области разрешения.
3. Проверить каждый MSSQL RID kit, общую registration14, strict configuration13, ILogger приложения и offline BPE resource loading. Не выполнять database initialization как незаметную часть DI smoke. Existing providers получают адресный smoke при изменении общего closure.
4. Для необходимых native операций актуального dependency graph получить отдельное разрешение на конкретный ресурс/команду. Проверить architecture/PE/ELF соответствие и missing/mixed dependency failure без fallback; Windows native DLL не копировать в Linux kit.
5. Если требуется IDE/XML acceptance, проверить конкретную IDE/version и доступность public API comments. Это отдельное evidence от XML file presence.
6. Зафиксировать successful и failure outcomes, versions/hash/dependency graph; не публиковать или устанавливать комплект в приложение пользователя автоматически.

## Проверки

Runtime/native — только после явного разрешения запуска. Сверить hashes перед загрузкой; отличать actual resource/DI/native проверки от compiler/PE/ELF observations16. Проверки с БД используют только окружение17; live HTTP здесь не требуется. Windows cross-build не является Linux ARM64 runtime evidence.

## Критерии завершения

Проверены три согласованных MSSQL RID kits и relevant native boundaries на соответствующих ОС/архитектурах. Недоступная Linux ARM64 среда остаётся незакрытой частью поддержки. Прочие RID/modes не объявляются проверенными. Shared app files и чужие installations не изменены.

## Результаты

Подготовка runtime потребителя и его запуск ещё не выполнялись.
