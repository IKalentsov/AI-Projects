# Отчёт: тикет 04 — Доработки по ревью тикетов 01 и 03

**Дата:** 2026-09-22
**Исполнитель:** Qwen3.6-MTP

## Что сделано

### 1. WeatherDigestServiceTests.cs — сняты все `#pragma warning disable`

- Удалён блок `#pragma warning disable CS4014, xUnit1051` в начале файла (строка 5 оригинала)
- Поле `TestCt` заменено на свойство: `private static CancellationToken TestCt => TestContext.Current.CancellationToken;`
- Проверки вызовов для Task-возвращающих методов ожидаются через `await`:
  - `await provider.Received(1).GetCurrentAsync(...)` (3 места)
  - `await sender.Received(1).SendAsync(...)` (2 места)
  - `await sender.DidNotReceive().SendAsync(...)` (1 место)
- Проверки для void/string методов (`Format`, `MarkSent`) — без `await` (NSubstitute возвращает `void`/`string`, не `Task`)

### 2. DiCompositionTests.cs — один `[Theory]` вместо шести дублей

- Шесть тестов с одинаковыми телами заменены одним `[Theory]` с `[InlineData(typeof(...))]` для каждого типа абстракции
- `ServiceProvider` освобождается через `using`

### 3. OpenMeteoWeatherProviderTests.cs

- **TimeoutHandler**: вместо `Task.Delay(TimeSpan.FromSeconds(60))` — `Task.FromException<TaskCanceledException>(new TaskCanceledException())`. Тесты таймаута выполняются мгновенно, а не по 30 с каждый
- Параметризованный тест `allPaths_returnResultNotException`: убран дублирующий сценарий `"Timeout"` (остался единственный выделенный тест на таймаут)
- Комментарий к `SuccessJson`: исправлен «camelCase» → «snake_case»
- Все три заглушки (`SuccessHandler`, `ErrorHandler`, `TimeoutHandler`) избавлены от `IDisposable` и `new void Dispose()`

### 4. WeatherMessageFormatterTests.cs — тест на отрицательное смещение с минутами

Добавлен `Format_negativeOffsetWithMinutes_displaysUTCMinusWithMinutes`: `-5:30 → (UTC-5:30)`

## Команды и вывод

### Сборка

```
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false

WeatherBot.Domain -> .../WeatherBot.Domain.dll
  WeatherBot.Application -> .../WeatherBot.Application.dll
  WeatherBot.Contracts -> .../WeatherBot.Contracts.dll
  WeatherBot.Infrastructure -> .../WeatherBot.Infrastructure.dll
  WeatherBot.Web -> .../WeatherBot.Web.dll
  WeatherBot.ArchitectureTests -> .../WeatherBot.ArchitectureTests.dll
  WeatherBot.UnitTests -> .../WeatherBot.UnitTests.dll

Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

### Unit-тесты

```
dotnet tests/WeatherBot.UnitTests/bin/Debug/net10.0/WeatherBot.UnitTests.dll

Сводка тестового запуска: Пройден! - .../WeatherBot.UnitTests.dll (net10.0|x64)
  всего: 122
  сбой: 0
  успешно выполнено: 122
  пропущено: 0
  длительность: 687ms
```

### Архитектурные тесты

```
dotnet tests/WeatherBot.ArchitectureTests/bin/Debug/net10.0/WeatherBot.ArchitectureTests.dll

Сводка тестового запуска: Пройден! - .../WeatherBot.ArchitectureTests.dll (net10.0|x64)
  всего: 11
  сбой: 0
  успешно выполнено: 11
  пропущено: 0
  длительность: 265ms
```

## Проверка критериев приёмки

| Критерий | Статус |
|----------|--------|
| Все `#pragma warning disable` убраны из WeatherDigestServiceTests.cs | ✅ (поиск по `pragma warning` — 0 совпадений) |
| Received()/DidNotReceive() для Task-методов ожидаются через `await` | ✅ |
| `TestCt` → `TestContext.Current.CancellationToken` | ✅ |
| Сборка: Ошибок: 0, Предупреждений: 0 | ✅ |
| DiCompositionTests: один `[Theory]`, `ServiceProvider` освобождается | ✅ |
| `SendDigestAsync_concurrentCalls_bothDigestsSendExactlyOnce` — имя + `MarkSent` дважды | ✅ |
| Тест на отрицательное смещение `-5:30 → UTC-5:30` добавлен | ✅ |
| `TimeoutHandler` не ждёт 60 с | ✅ |
| Комментарий `SuccessJson`: snake_case | ✅ |
| Заглушки без `new void Dispose()` | ✅ |
| UnitTests ≥ 89, ArchitectureTests ≥ 6 | ✅ (122 и 11) |
| `src/**` не изменён | ✅ (`git status --short -- src` — пусто) |

## Отклонения

Нет. Все замечания ревью закрыты без отклонений.

## Что не проверялось

- Прогон на чистой сборке (без кэша) — среда агента работает офлайн
- Время прогона всех тестов как метрика (< 5 с) — unit-тесты прошли за 687 мс, архитектурные за 265 мс, но это не формальный критерий
