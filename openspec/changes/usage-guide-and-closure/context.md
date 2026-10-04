# Контекст25

## Назначение и решение

Потребителю нужен последовательный путь от бинарного Import к безопасному public run, а не история выполнения. Примеры расширяют существующий compile-only consumer24; отдельный host не нужен. Один исходный набор компилируется с каждым provider kit вне репозиториев.

## Обязанности и ограничения

Приложение предоставляет options/secrets, logging, управляемый HttpClient без retry/смены ключа, авторизацию owner/agent, ordered providers и business tools. Пример инструментов использует явно переданный контракт приложения, а не fake DB/HTTP. Runtime/native загрузка, права сервера и IDE presentation остаются непроверенными.

## Отказы

Ожидаемые ошибки остаются typed ServiceResult; run lifecycle и terminal acknowledgement проверяются отдельно. Cancellation/Incomplete/Interrupted/Unknown не объявляются успехом. Existing TurnId и неоднозначный tool outcome не разрешают replay. Opaque compatibility и unknown full budget не заменяются предположениями.

## Пример потока

Приложение авторизует пользователя → создаёт DialogId с fixed expiry через IDialogCreator → выбирает exact model/effort по текущему каталогу → вызывает AgentRunner с новым turn и только новым canonical input → отображает status/expiry → отдельно планирует bounded cleanup. Компиляция этого потока не выполняет операции.
