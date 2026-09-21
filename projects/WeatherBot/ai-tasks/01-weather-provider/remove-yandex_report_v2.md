# Отчёт: 01-01-remove-yandex (доработки)

**Постановка:** `ai-tasks/01-weather-provider/01-01-remove-yandex.md`
**Ревью-фиксы:** `ai-tasks/01-weather-provider/remove-yandex.fixes.md`
**Версия отчёта:** v2
**Дата:** 2026-09-21
**Исполнитель:** Qwen3.6-MTP

---

## Что исправлено по fixes

### Правка 1. Потеряна регистрация `IHttpClientFactory`

**Файл:** `backend/WeatherBot/src/WeatherBot.Infrastructure/DependencyInjectionExtension.cs`
**Что сделано:** добавлена строка `services.AddHttpClient();` перед регистрацией провайдера. Это регистрирует `IHttpClientFactory` в DI-контейнере. Провайдер остаётся синглтоном (`AddSingleton<IWeatherProvider, OpenMeteoWeatherProvider>()`), как требовала постановка §5.

### Правка 2. Смещение UTC захардкожено

**Файл 1:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoContracts.cs`
**Что сделано:** в класс `OpenMeteoResponse` добавлено поле `int UtcOffsetSeconds { get; set; }`. `JsonSerializerDefaults.Web` автоматически мапит `utc_offset_seconds` (snake_case) → `UtcOffsetSeconds` (camelCase).

**Файл 2:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`
**Что сделано:** вызов `OpenMeteoWeatherMapper.ToObservedAt(c.Time, 10800)` заменён на `TryParseObservedAt(c.Time, parsed.UtcOffsetSeconds, out var observedAt)`. Литерал `10800` из кода удалён.

### Правка 3. Не реализовано правило «нулевых осадков»

**Файл:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherMapper.cs`
**Что сделано:** метод `ToPrecipitationType` получил четвёртый параметр `double precipitation`. Проверка нулевых осадков изменена с `weatherCode == 0 && rain == 0 && snowfall == 0` на `precipitation == 0 && rain == 0 && snowfall == 0` — теперь она не зависит от кода погоды, как требует контракт §5 постановки. Приоритет `Mixed` (`rain > 0 && snowfall > 0`) сохранён выше правила нулевых осадков.

**Файл:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`
**Что сделано:** вызов обновлён: `ToPrecipitationType(c.WeatherCode, c.Precipitation, c.Rain, c.Snowfall)`.

### Правка 4. Разбор времени может выбросить исключение наружу

**Файл:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherMapper.cs`
**Что сделано:** метод `ToObservedAt(string, int)` заменён на `TryParseObservedAt(string, int, out DateTimeOffset)`. Вместо `DateTime.Parse(localTime!)` используется `DateTime.TryParse`, которая возвращает `false` при пустом или неразбираемом значении. Исключение наружу не выбрасывается.

**Файл:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`
**Что сделано:** вызов маппера заменён на:

```csharp
if (!OpenMeteoWeatherMapper.TryParseObservedAt(c.Time, parsed.UtcOffsetSeconds, out var observedAt))
{
    logger.LogError("Не удалось разобрать время из Open-Meteo (time='{Time}', offset={Offset})", c.Time, parsed.UtcOffsetSeconds);
    return Result.Failure<WeatherInfo>("Не удалось разобрать время наблюдения из ответа Open-Meteo");
}
```

Ожидаемая ошибка — значение `Result.Failure`, а не исключение. Соответствует паттерну проекта (§3 AGENTS.md).

---

## Изменённые файлы

| Файл | Действие | Комментарий |
|------|----------|-------------|
| `backend/WeatherBot/src/WeatherBot.Infrastructure/DependencyInjectionExtension.cs` | изменён | добавлена строка `services.AddHttpClient();` |
| `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoContracts.cs` | изменён | добавлено поле `UtcOffsetSeconds` в `OpenMeteoResponse` |
| `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherMapper.cs` | изменён | `ToPrecipitationType` — добавлен параметр `precipitation`; `ToObservedAt` → `TryParseObservedAt` (bool + out) |
| `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs` | изменён | передача `parsed.UtcOffsetSeconds` вместо литерала; вызов `TryParseObservedAt` с возвратом `Result.Failure`; передача `c.Precipitation` в `ToPrecipitationType` |

---

## Команды и дословный вывод

**Сборка:**

```
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false
```

```
WeatherBot.Domain -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Domain\bin\Debug\net10.0\WeatherBot.Domain.dll
  WeatherBot.Application -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Application\bin\Debug\net10.0\WeatherBot.Application.dll
  WeatherBot.Contracts -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Contracts\bin\Debug\net10.0\WeatherBot.Contracts.dll
  WeatherBot.Infrastructure -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Infrastructure\bin\Debug\net10.0\WeatherBot.Infrastructure.dll
  WeatherBot.Web -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Web\bin\Debug\net10.0\WeatherBot.Web.dll

Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

**Поиск yandex/Яндекс (приёмка §7 постановки):**

```powershell
Get-ChildItem -Path "H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend" -Recurse -File -Include *.cs,*.json,*.csproj,*.props,*.slnx,*.config | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Select-String -Pattern 'yandex|яндекс' -CaseSensitive:$false
```

```
(no output)
```

Пустой результат — ни одного совпадения не найдено.

**Поиск AddHttpClient по src/** (дополнительная проверка правки 1):

```bash
grep -r "AddHttpClient" backend/WeatherBot/src/
```

Найдено 1 совпадение:
- `backend/WeatherBot/src/WeatherBot.Infrastructure/DependencyInjectionExtension.cs` — строка `services.AddHttpClient();`

---

## Что не проверено и почему

- **Живой вызов Open-Meteo** — невозможен из песочницы (нет сети). Указано в постановке как ожидаемое.
- **Тестовые проекты** — ещё не созданы (задача `01-02`). Резолв графа зависимостей будет проверен там.

---

## Отклонения от fixes и замечено «заодно»

Нет. Все 4 правки из `remove-yandex.fixes.md` внесены в полном объёме, без дополнительных изменений.

---

## Проблемы и вопросы

Не было. Сборка прошла с первого раза после внесения всех 4 правок.

---

## Использованные скиллы

Не применялись — задача решена чтением постановки, ревью-фиксов и существующего кода без обращения к внешним скиллам.

---

## Самопроверка по Definition of Done

- [x] Сборка: `Ошибок: 0`, `Предупреждений: 0`.
- [ ] Тесты на новый функционал добавлены (если функционал есть); все зелёные. — **Не применимо**: тестовые проекты ещё не созданы (задача `01-02`).
- [x] Секретов в коде и отчёте нет.
- [x] Файлы `ARCHITECTURE.md`, `AGENTS.md`, `WORKFLOW.md`, `memory-bank/**`, `ai-tasks/**` не изменялись (кроме этого отчёта).
- [x] Условие остановки из постановки достигнуто: сборка без ошибок и предупреждений, поиск по `yandex|яндекс` в дереве решения — пустой результат. Объём не расширен.
