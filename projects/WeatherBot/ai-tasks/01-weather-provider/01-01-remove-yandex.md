# 01-01-remove-yandex

**Требует изменения архитектуры:** да — уже внесено архитектором в `ARCHITECTURE.md`
(разделы «Overview», «Интеграция с Open-Meteo», «Конфигурация», «Паттерны», «Формат сообщения»)
и в `WORKFLOW.md`.
**Статус:** согласована
**Задача:** 01-weather-provider
**Зависит от:** нет
**Слой / проект:** Domain, Infrastructure, Web

---

## 1. Цель

**В проекте не остаётся ничего, связанного с Яндекс.Погодой.** Источник погоды —
**Open-Meteo**: ключ API не нужен, вместо него источники данных берутся по общедоступному
HTTP-запросу. Город выносится в настройки (`OpenMeteo:City`, по умолчанию «Москва»).

Главный критерий готовности — **поиск по `yandex`/`Яндекс` в `backend/**` не находит ничего**:
ни файлов, ни типов, ни секций конфигурации, ни комментариев.

## 2. Контекст

**Обязательно прочитать до начала работы** (в этом порядке, не по памяти):

| # | Файл | Зачем |
|---|------|-------|
| 1 | `memory-bank/snapshot.md` | инвентарь проекта и реестр находок |
| 2 | `memory-bank/activeContext.md` | где мы сейчас, что решено |
| 3 | `ARCHITECTURE.md` → «Интеграция с Open-Meteo», «Конфигурация», «Формат сообщения» | целевой контракт: поля API, единицы, ловушки |
| 4 | `WORKFLOW.md` §2 | команды и ограничения среды |
| 5 | `.dsh/AGENTS.md` | правила проекта |

- Корень проекта: `projects/WeatherBot/`; решение — `backend/WeatherBot/WeatherBot.slnx`.
- Сейчас провайдер — `YandexWeatherProvider` (GraphQL, заголовок `X-Yandex-Weather-Key`).
  Он удаляется целиком вместе с маппером и контрактами.
- Абстракция `IWeatherProvider` (Application) **не меняется**, поэтому `WeatherDigestService`,
  контроллеры и фоновая задача не затрагиваются.
- `Domain/WeatherInfo.cs` и enum-ы **не меняются**: набор полей тот же.

### Что даёт Open-Meteo (проверено живым запросом 2026-09-21)

Ответ без ключа по Москве:

```json
{"latitude":55.75,"longitude":37.625,"utc_offset_seconds":10800,"timezone":"Europe/Moscow",
 "current":{"time":"2026-09-21T05:45","interval":900,"temperature_2m":13.7,
 "relative_humidity_2m":85,"apparent_temperature":13.0,"precipitation":0.00,"rain":0.00,
 "snowfall":0.00,"weather_code":3,"cloud_cover":98,"pressure_msl":1012.8,
 "wind_speed_10m":7.6,"wind_direction_10m":183,"wind_gusts_10m":21.6}}
```

**Три ловушки, из-за которых нужен маппер:**

1. давление приходит в **гектопаскалях** (1012.8 гПа), а не в мм рт.ст.;
2. направление ветра — в **градусах** (183°), а не румбами;
3. тип осадков — **кодом WMO** (`3`), а не строковым enum-ом.

Время в `current.time` — локальное время точки **без смещения**; смещение лежит отдельно
в `utc_offset_seconds`.

### Ограничения среды исполнителя

Коротко: `-m:1` обязателен и для `restore`, и для `build` (без него команда падает молча
с кодом 1); `dotnet test` на MTP падает на именованном канале — тесты запускаются прямым
вызовом собранной сборки. Подробности — скилл `dotnet-verify-in-sandbox`.

**Новые пакеты не нужны**: `Microsoft.Extensions.Http` уже подключён в Infrastructure,
restore для этой задачи не требуется. Живой вызов Open-Meteo из песочницы невозможен —
указать в отчёте как «не проверено».

## 3. Файлы

**Создать:**

- `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoContracts.cs`
- `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`
- `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherMapper.cs`

**Удалить:**

- `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/YandexWeatherProvider.cs`
- `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/YandexWeatherMapper.cs`
- `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/YandexWeatherContracts.cs`

**Изменить:**

- `src/WeatherBot.Domain/WeatherSettings.cs` — секция `OpenMeteo`, убрать `ApiKey`,
  добавить `City`, поправить XML-комментарии;
- `src/WeatherBot.Domain/WeatherBot.Domain.csproj` — комментарий перечисляет секции
  `Telegram, YandexWeather, WeatherBot`: привести в соответствие;
- `src/WeatherBot.Infrastructure/DependencyInjectionExtension.cs` — регистрация нового провайдера;
- `src/WeatherBot.Infrastructure/Formatting/WeatherMessageFormatter.cs` — город из настроек,
  время из `ObservedAt`, убрать зашитые «Москва» и «МСК»;
- `src/WeatherBot.Web/Controllers/BotController.cs` — XML-комментарий у `send-test` сейчас
  упоминает ключ; переформулировать без вендора (поведение не менять);
- `src/WeatherBot.Web/appsettings.json` — секция `YandexWeather` → `OpenMeteo` без `ApiKey`,
  с `City`.

**Не трогать:**

- `src/WeatherBot.Application/**` — абстракции не меняются;
- `src/WeatherBot.Domain/WeatherInfo.cs` и enum-ы;
- `src/WeatherBot.Web/appsettings.Development.json` — **содержит реальный токен**;
  разберётся архитектор, не исполнитель;
- `ARCHITECTURE.md`, `WORKFLOW.md`, `.dsh/AGENTS.md`, `README.md`, `memory-bank/**`,
  `ai-tasks/**` (кроме своего отчёта);
- `backend/Directory.Packages.props`, `backend/Directory.Build.props`, `backend/.globalconfig`;
- `TelegramSender`, `BotStateManager`, `InMemoryLogBuffer`, `InMemoryLoggerProvider`,
  `WeatherBotBackgroundService`, `WeatherDigestService`.

## 4. Шаги

1. Изменить `WeatherSettings` (имя секции, состав полей, комментарии).
2. Создать контракты ответа Open-Meteo.
3. Создать маппер «API → домен» по таблицам §5.
4. Создать провайдер `OpenMeteoWeatherProvider`.
5. Переработать `WeatherMessageFormatter`.
6. Обновить регистрацию в DI и `appsettings.json`.
7. Удалить три яндексовых файла.
8. Пройтись поиском по `yandex`/`Яндекс` по `backend/**` и вычистить найденное —
   включая комментарии в `.csproj` и XML-документацию.
9. Прогнать приёмку из §7.

## 5. Контракты и форматы данных

### Запрос

`GET https://api.open-meteo.com/v1/forecast`, без заголовков авторизации:

```
latitude={Latitude}
longitude={Longitude}
current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,rain,snowfall,weather_code,cloud_cover,pressure_msl,wind_speed_10m,wind_direction_10m,wind_gusts_10m
wind_speed_unit=ms
timezone=auto
```

Параметр `wind_speed_unit=ms` обязателен — иначе ветер придёт в км/ч.

### Разбор ответа

- успех — HTTP 200 и наличие блока `current`; иначе `Result.Failure` с понятным русским текстом;
- ответ не-200, таймаут, `HttpRequestException`, `JsonException`, отсутствие `current` —
  **не выбрасывать наружу**, а возвращать `Result.Failure(...)` (ошибки не покидают инфраструктуру);
- таймаут запроса — 30 секунд.

### Маппинг «API → домен» — обязательные правила

**Давление:** `pressure_msl` в гПа → мм рт.ст.: `hPa × 0.750062`, округление до целого
(половина — от нуля). Пример: `1012.8` → `760`.

**Ветер:**
- скорость — как пришла (`wind_speed_unit=ms`), в домен `double`;
- направление `wind_direction_10m` (градусы) → 8 румбов, сектор 45°, полуинтервал
  `[начало, конец)`:

| Градусы | Домен |
|---|---|
| `[337.5, 360)` и `[0, 22.5)` | `North` |
| `[22.5, 67.5)` | `NorthEast` |
| `[67.5, 112.5)` | `East` |
| `[112.5, 157.5)` | `SouthEast` |
| `[157.5, 202.5)` | `South` |
| `[202.5, 247.5)` | `SouthWest` |
| `[247.5, 292.5)` | `West` |
| `[292.5, 337.5)` | `NorthWest` |

- вне диапазона `[0, 360)` → `Unknown`.

**Облачность:** по `cloud_cover`, %:

| `cloud_cover` | Домен |
|---|---|
| `< 20` | `Clear` |
| `[20, 60)` | `PartlyCloudy` |
| `[60, 85)` | `Cloudy` |
| `>= 85` | `Overcast` |

**Тип осадков:** по `weather_code` (код WMO), с уточнением по `rain`/`snowfall`:

| `weather_code` | Домен |
|---|---|
| `0, 1, 2, 3, 45, 48` | `None` |
| `51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95` | `Rain` |
| `71, 73, 75, 77, 85, 86` | `Snow` |
| `96, 99` | `Hail` |
| прочие | `Unknown` |

- **приоритет уточнения:** если одновременно `rain > 0` и `snowfall > 0` → `Mixed`
  (независимо от кода);
- если `precipitation == 0` и `rain == 0` и `snowfall == 0` → `None`
  (даже если код относится к осадкам: в текущий момент осадков нет).

**Интенсивность осадков:** по `precipitation`, мм/ч:

| `precipitation` | Домен |
|---|---|
| `0` | `None` |
| `(0, 0.5]` | `Weak` |
| `(0.5, 2.5]` | `Moderate` |
| `(2.5, 7.5]` | `Heavy` |
| `> 7.5` | `VeryHeavy` |

**Момент наблюдения:** `current.time` — локальное время точки без смещения; собрать
`DateTimeOffset` из него и `utc_offset_seconds`. В `WeatherInfo.ObservedAt` попадает момент
наблюдения из API, а не время получения ответа.

### Настройки

| Секция | Поле | Тип | По умолчанию |
|--------|------|-----|--------------|
| `OpenMeteo` | `Latitude` | double | `55.7558` |
| `OpenMeteo` | `Longitude` | double | `37.6173` |
| `OpenMeteo` | `City` | string | `Москва` |

- имя секции задаётся константой `SectionName` в классе настроек;
- поля `ApiKey` в настройках погоды **больше нет**;
- биндинг — `IOptionsMonitor<T>`, как для остальных секций.

### Формат сообщения

```
🌤 *Погода в {City}*

🌡 *Температура:* 5°C (ощущается как 2°C)
☁️ *Облачность:* Облачно
💧 *Влажность:* 78%
📊 *Давление:* 745 мм рт.ст.
💨 *Ветер:* 3 м/с, СЗ
🌧 *Осадки:* Дождь (слабые)

_Обновлено: 21.09.2026 14:30 (UTC+3)_
```

- заголовок берёт город из настроек, «Москва» в форматтере **не хардкодится**;
- нижняя строка — время из `ObservedAt` и его смещение от UTC, «МСК» **не хардкодится**;
- значения `Unknown` → «Нет данных» / «—»; отправка при этом не срывается;
- прочие правила (осадки без интенсивности, штиль) сохраняются как сейчас.

### DI

`WeatherDigestService` — синглтон, а провайдер регистрировался как transient через
`AddHttpClient`: синглтон навсегда захватывал клиент, и ротация HTTP-хендлера не работала.
Провайдер пишется заново — исправьте сразу:

- провайдер получает **`IHttpClientFactory`**, а не готовый `HttpClient`;
- регистрируется как **синглтон**;
- таймаут задаётся при создании клиента из фабрики.

## 6. Тесты

**Тестовых проектов в решении пока нет, и эта задача их не создаёт.** Тесты на новый
маппер, провайдер и форматтер пишутся следующей задачей — `01-02-tests-infrastructure`.
Это осознанное отступление от общего правила «тесты вместе с кодом», зафиксированное
в `WORKFLOW.md` §6 по решению пользователя.

Сценарии, которые обязана покрыть следующая задача (приведены здесь как контракт,
чтобы маппинг из §5 не «поплыл»): все границы секторов ветра, все пороги облачности,
все коды WMO из таблицы, все пять диапазонов интенсивности, неизвестный код → `Unknown`,
не-200 / таймаут / битый JSON / ответ без `current` → `Result.Failure`.

## 7. Приёмка

Каталог — `backend/WeatherBot`. Вывод команд приводить **дословно**.

```bash
# 1) сборка
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore

# 2) ни одного следа Яндекса в backend (bin/obj исключены)
Get-ChildItem -Recurse -File -Include *.cs,*.json,*.csproj,*.props,*.slnx,*.config |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'yandex|яндекс' -CaseSensitive:$false
```

**Критерии приёмки:**

- [ ] Сборка: `Ошибок: 0`, `Предупреждений: 0`.
- [ ] Команда 2 **не выводит ничего** — ни файлов, ни строк. Привести её вывод дословно
      (пустой результат — тоже результат, так и написать).
- [ ] Три файла `YandexWeather*.cs` удалены.
- [ ] В `appsettings.json` есть секция `OpenMeteo` с `City` и **нет** `ApiKey`.
- [ ] В `WeatherSettings` нет поля `ApiKey`, `SectionName` = `OpenMeteo`.
- [ ] Раскладка файлов совпадает с `ARCHITECTURE.md` → «Раскладка исходников».
- [ ] Секретов в коде и отчёте нет.
- [ ] Живой вызов Open-Meteo из песочницы невозможен (нет сети) — указать в отчёте
      в разделе «Что не проверено».

## 8. Условие остановки

**Единственное условие остановки:** сборка завершается с `Ошибок: 0` и `Предупреждений: 0`,
**и** поиск по `yandex|яндекс` в `backend/**` (без `bin`/`obj`) не находит ни одной строки.

- Достигнуто → остановиться, написать отчёт, **объём не расширять**.
- Обнаружено «заодно» (находки `F-02`, `F-06`, `F-08`, `F-09`, `F-11`) → в раздел
  «Отклонения и замечено» отчёта, но **не в код**.
- Условие недостижимо по внешней причине → остановиться **раньше** и сообщить.

## 9. Что НЕ трогать

- `src/WeatherBot.Application/**`, `Domain/WeatherInfo.cs` и enum-ы.
- `src/WeatherBot.Web/appsettings.Development.json` — содержит реальный токен.
- `ARCHITECTURE.md`, `WORKFLOW.md`, `.dsh/AGENTS.md`, `README.md`, `memory-bank/**`,
  `ai-tasks/**` (кроме своего отчёта) — зона архитектора.
- `backend/Directory.Packages.props`, `backend/Directory.Build.props`, `backend/.globalconfig`.
- Тестовые проекты не создавать — это задача `01-02`.
- Найденные попутно проблемы `F-02`, `F-06`, `F-08`, `F-09`, `F-11` — не исправлять.

## 10. Лимит попыток и честный отчёт

- Не более **3 попыток** на один блокер. Дальше — стоп и отчёт.
- Restore не требуется: новых пакетов задача не добавляет.
- Не получилось — написать прямо: что не вышло, сколько попыток, что пробовал, какие ошибки.
  Молчаливое «готово» при неработающем результате — грубое нарушение.

## 11. Порядок сдачи

1. Выполнить приёмку из §7 и сохранить дословный вывод команд.
2. Написать отчёт `ai-tasks/01-weather-provider/remove-yandex_report_v1.md`
   по шаблону `ai-tasks/_templates/report.md`.
3. Приложить список созданных, изменённых и удалённых файлов.
