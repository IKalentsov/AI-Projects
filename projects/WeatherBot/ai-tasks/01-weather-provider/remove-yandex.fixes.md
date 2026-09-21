# Доработки: 01-01-remove-yandex

**Ревью:** `remove-yandex.review.md`
**Дата:** 2026-09-21
**Кому:** исполнитель микро-задачи

> Формат правок — максимально конкретный: **файл → место → что заменить**.
> Исполнитель не додумывает: он вносит ровно то, что написано.

---

## Правка 1. Потеряна регистрация `IHttpClientFactory`

**Файл:** `backend/WeatherBot/src/WeatherBot.Infrastructure/DependencyInjectionExtension.cs`
**Место:** метод `AddInfrastructure`, строка с `services.AddSingleton<IWeatherProvider, OpenMeteoWeatherProvider>();` (стр. 37)
**Сейчас:** прежний вызов `AddHttpClient<IWeatherProvider, YandexWeatherProvider>(…)` удалён и заменён
на `AddSingleton`, но ни одного вызова `AddHttpClient` во всём `src/**` не осталось. При этом
`OpenMeteoWeatherProvider` объявляет в конструкторе параметр `IHttpClientFactory`.

**Должно быть:** в `AddInfrastructure` присутствует регистрация `IHttpClientFactory` (через
`AddHttpClient` в той или иной форме). Провайдер при этом **остаётся синглтоном** — это требование
постановки §5, typed client, как было у яндексового провайдера, не подходит: он даст transient
и вернёт захват синглтоном.

**Почему:** `IHttpClientFactory` не регистрируется ничем, кроме `AddHttpClient`. Сейчас резолв
`IWeatherProvider` падает с `InvalidOperationException`. При `ValidateOnBuild`, который включается
в окружении Development (`launchSettings.json` задаёт именно Development), хост упадёт **на старте**;
в остальных окружениях — на первом обращении к погоде. Сборка при этом зелёная: дефект виден
только при запуске.

**Как проверить:** после правки `dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false`
должен остаться `Ошибок: 0`, `Предупреждений: 0`, и поиск `AddHttpClient` по `src/**` — находить
хотя бы одно совпадение.

---

## Правка 2. Смещение UTC захардкожено

**Файл 1:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoContracts.cs`
**Место:** класс `OpenMeteoResponse` (стр. 4–8)
**Сейчас:** в корневом контракте есть только `Current`; поля под `utc_offset_seconds` нет.

**Файл 2:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`
**Место:** `ParseResponse`, строки 103–104
**Сейчас:** `ObservedAt: OpenMeteoWeatherMapper.ToObservedAt(c.Time, 10800)` — смещение передано
литералом.

**Должно быть:** в `OpenMeteoResponse` появляется поле, соответствующее `utc_offset_seconds` ответа
(серый snake_case → camelCase даёт `JsonSerializerDefaults.Web`, дополнительных атрибутов не нужно).
В `ObservedAt` передаётся значение **из ответа**. Литерала `10800` в коде быть не должно.

**Почему:** §5 постановки, раздел «Момент наблюдения»: `DateTimeOffset` собирается из `current.time`
и `utc_offset_seconds`. Сейчас город настраивается (`OpenMeteo:City`), а время жёстко привязано
к UTC+3 — при смене координат сообщение соврёт в дате и часе. Это ровно находка `F-07`
в новой форме, и задача `01-01` обязана её закрыть, а не перенести.

---

## Правка 3. Не реализовано правило «нулевых осадков»

**Файл:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherMapper.cs`
**Место:** `ToPrecipitationType` (стр. 52–77); вызов — `OpenMeteoWeatherProvider.cs:101–102`
**Сейчас:** `if (weatherCode == 0 && rain == 0 && snowfall == 0) return PrecipitationType.None;` —
проверяется код погоды, а `precipitation` в метод **не передаётся вообще**.

**Должно быть:** правило из §5, таблица «Тип осадков»: если `precipitation == 0` **и** `rain == 0`
**и** `snowfall == 0` → `None`, независимо от `weather_code` («в текущий момент осадков нет»).
Параметр `precipitation` должен доходить до метода. Приоритет `Mixed` при `rain > 0 && snowfall > 0`
сохраняется и остаётся выше правила нулевых осадков. Таблица кодов WMO не меняется.

**Почему:** прямое отклонение от контракта §5. Сейчас при `weather_code = 61` (дождь) и нулевых
`precipitation`/`rain`/`snowfall` вернётся `Rain`, тогда как контракт требует `None`.

---

## Правка 4. Разбор времени может выбросить исключение наружу

**Файл 1:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherMapper.cs`
**Место:** `ToObservedAt`, стр. 91–95
**Сейчас:** `DateTime.Parse(localTime!, CultureInfo.InvariantCulture)` — при пустом или неразбираемом
`current.time` выбрасывается `FormatException`/`ArgumentNullException`.

**Файл 2:** `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`
**Место:** `GetCurrentAsync`, блоки `catch` (стр. 46–66)
**Сейчас:** ловятся `HttpRequestException`, `TaskCanceledException`, `JsonException`.
`FormatException` не ловится нигде, поэтому покидает `GetCurrentAsync` и уходит наверх.

**Должно быть:** отсутствующее или неразбираемое `current.time` даёт `Result.Failure` с понятным
русским текстом — как и остальные ошибки разбора. Конкретная форма (разбор с проверкой результата
в провайдере, возврат признака отсутствия времени из маппера или иное) — на выбор исполнителя;
важно, что наружу из `GetCurrentAsync` по этой причине исключение не выходит.

**Почему:** `.dsh/AGENTS.md` §3 и паттерн проекта: ожидаемые ошибки — значениями `Result`, а не
исключениями; ошибки не покидают инфраструктуру. Прежний провайдер этим свойством обладал
(брал `DateTimeOffset.Now`), новый его потерял.

---

## После внесения правок

1. Прогнать приёмку из §7 постановки: сборка (`Ошибок: 0`, `Предупреждений: 0`) и поиск
   `yandex|яндекс` по дереву решения — пусто.
2. Дополнительно подтвердить в отчёте, что поиск `AddHttpClient` по `src/**` находит совпадение
   (проверка правки 1).
3. Тестов пока нет — они появятся в задаче `01-02`; там же обязателен тест на резолв графа
   зависимостей и тесты на границы маппинга.
4. Обновить отчёт: **новый** файл `remove-yandex_report_v2.md` (предыдущий не перезаписывать),
   в нём — раздел «Что исправлено по fixes» с указанием каждой правки.

## Чего делать НЕ нужно

- Не трогать `ai-tasks/**` и прочие `.md`-артефакты: правки приёмки и постановки вносит архитектор.
- Не «улучшать» соседний код: замечания 6–8 ревью отложены в `01-02` и в этой задаче не правятся.
