# Изолированные проверки ядра

- Только core/Application/Tokenization и локальные doubles; без приложения, host, сети, БД и внешних artifacts.
- Бюджетные тесты различают strict, ServerValidation с известным окном и явный ServerValidationWithUnknownWindow. Порог и резерв не заменяют unknown window; custom snapshot не обходит guard policy.
- Содержательные сценарные группы остаются отдельными test classes; не добавлять transport/provider fixtures в этот проект.
