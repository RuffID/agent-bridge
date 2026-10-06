# 06 — Проверка и условное исправление HTTP disposal

[Навигатор](README.md). Статус: **не начат**. Зависимость: 00; 04 для итоговой SSE-регрессии. Находка: **ABQA-002, подозрение, S2 предварительно**.

## Цель и область

Проверить освобождение response при throwing Body.Dispose/DisposeAsync и сохранение первичной ошибки обращения. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), streaming lifecycle в [OpenSpec](../../../openspec/specs/agent-runtime/spec.md).

Владелец wrapper — [HttpStreamResponseResult](../../../../work/HttpClientLibrary/Models/HttpStreamResponseResult.cs), проект `HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj` соседней библиотеки. В AgentBridge проверить gateway ownership. Изменения HttpClientLibrary требуют отдельного поручения; не добавлять параллельный HTTP-клиент.

## Работы

1. Создать изолированный наблюдаемый response/content и stream, выдающий контролируемое исключение при sync/async disposal. Проверить оба wrapper paths, повторное освобождение и ownership Content/Body по реальным .NET компонентам fixture.
2. Проверить, достигается ли response cleanup после ошибки Body, что реально завершено и какое исключение получает вызывающий. Отличать вызов Dispose от гарантии успешного освобождения при новом secondary failure.
3. При подтверждении нарушения точечно исправить библиотечную ownership границу, обеспечив попытку освобождения response и честный результат ошибок. Сохранить agreed public API и успешные sync/async пути.
4. Отдельно проверить gateway: read/JSON/callback primary exception + throwing cleanup. Место сохранения primary определить по actual ownership; правка wrapper сама по себе не доказывает сохранение callback exception через await using.
5. Если нужен новый публичный error/lifecycle contract, согласовать его до реализации. При опровержении сохранить контрпример, границу вывода и отказ от необоснованной правки.

## Проверки

B без live HTTP: wrapper sync/async; успешный и throwing Body; наблюдаемый response/content cleanup; primary-only/cleanup-only/двойной отказ; canceled caller. В gateway использовать actual HttpClientLibrary с fake handler/local streams и проверить no retries, Failed/caller/deadline/callback priority после04.

Compile-check конкретных library и adapter/test проектов. Реальная утечка соединений либо её отсутствие в deployment не выводятся из fixture; при необходимости отдельное evidence19.

## Критерии завершения

ABQA-002 получает «подтверждено и исправлено в указанной границе», «опровергнуто в указанной границе» либо остаётся подозрением с точным недостающим evidence. Условная серьёзность не превращается в доказанный ущерб. Primary и cleanup результаты не теряются молча.

## Результаты

Воспроизведение, решение о правке и проверки ещё не выполнялись.
