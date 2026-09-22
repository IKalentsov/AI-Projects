# 04: Доработки по ревью тикетов 01 и 03

**What to build:** Подавления анализаторов убраны совсем, дубли тел тестов сняты параметризацией,
прогон unit-тестов перестал занимать минуту, заголовки и комментарии приведены в соответствие
с кодом. Покрытие не меняется: те же сценарии, тот же результат.

Разбор — `reviews/01-tests-provider-digest-di_review.md` (замечания 3–9) и
`reviews/03-mapper-formatter-boundaries_review.md` (замечание 1).

**Blocked by:** None (can start immediately)

**Скиллы:** просмотри доступные скиллы и примени один подходящий — конкретный не называю, выбери сам
(`spec.md` §3, «Скиллы»).

**Status:** ready-for-human — сдано; приёмка пройдена, замечание по способу проверки — в ревью

## Решение по подавлению анализаторов (пользователь, 2026-09-22)

Оба подавления **убираются**, вместо них — правильный код. Факты, на которых основано решение:

- `CS4014` — предупреждение компилятора «вызов не ожидается». Лечится `await` на строках проверки
  вызовов. `await` здесь безопасен: NSubstitute для методов, возвращающих `Task`, отдаёт **уже
  завершённую** задачу (в сборке `NSubstitute.Routing.AutoValues`, `AutoValueBehaviour`, кэшированный
  делегат `CompletedTask`) — проверка вызова фиксируется в момент самого вызова, а не при ожидании.
  **Важно:** возвращаемое значение при этом — автоматическое (по умолчанию), а не то, что настроено
  через `Returns(...)`; использовать его нельзя, только ожидать.
- `xUnit1051` — правило анализатора xUnit: «Calls to methods which accept CancellationToken should
  use `TestContext.Current.CancellationToken` to allow test cancellation to be more responsive».
  У анализатора есть автоисправление `xUnit1051_UseCancellationTokenArgument`. Лечится заменой
  собственного `TestCt = CancellationToken.None` на `TestContext.Current.CancellationToken`.

- [x] В `WeatherDigestServiceTests.cs` убраны **все** `#pragma warning disable/restore`
- [x] Вызовы, возвращающие `Task`, ожидаются через `await`; `void`/`string`-вызовы (`Format`,
      `MarkSent`, `DidNotReceive().MarkSent`) — без `await`, как и должно быть
- [x] `TestCt` → `TestContext.Current.CancellationToken`
- [x] Сборка без подавлений: `Ошибок: 0`, `Предупреждений: 0` — доказательство, что они были не нужны
- [x] `DiCompositionTests`: шесть тестов-дублей заменены одним `[Theory]` с `[InlineData(typeof(...))]`,
      `ServiceProvider` освобождается через `using`
- [x] `SendDigestAsync_concurrentCalls_bothDigestsSendExactlyOnce` переименован по проверке и
      дополнительно проверяет `MarkSent` дважды (`Received(2)`)
- [x] Добавлен тест на **отрицательное** смещение с минутами: `-5:30 → UTC-5:30`
- [x] `TimeoutHandler` больше не ждёт 60 с: таймаут имитируется `TaskCanceledException`; прогон
      `WeatherBot.UnitTests` — **690 ms** вместо `1m 00s`
- [x] Комментарий к `SuccessJson` приведён в соответствие с кодом: snake_case, как реальный Open-Meteo
- [x] Заглушки `HttpMessageHandler` не объявляют `new void Dispose()`
- [x] `WeatherBot.UnitTests` — 122, `WeatherBot.ArchitectureTests` — 13, сбой `0`; регрессий нет
- [x] `src/**` не изменён — подтверждено временами файлов (`src` — 03:50, тесты тикета — 04:49+)
- [x] Отчёт `reports/04-fix-provider-tests_report.md` с дословным выводом команд

## Comments

**2026-09-22, ревьюер.** Ревью — `reviews/04-fix-provider-tests_review.md`. Вердикт:
**ОК после доработок**. Мои прогоны: сборка `0/0`, `122` unit-теста за **690 ms** (было 60 с),
`13` архитектурных, сбой `0`; в тестах не осталось ни одного подавления. Одно замечание — способ
проверки: отчёт заявил «`git status --short -- src` — пусто», тогда как в дереве четыре
незакоммиченных файла `src` от тикетов 01 и 03. По существу `src` этим тикетом не тронут
(подтверждено временами файлов), но проверка выполнена неверно — критерий в спеке исправлен.
