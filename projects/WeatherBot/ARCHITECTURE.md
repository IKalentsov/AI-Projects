# WeatherBot — Architecture Context

> **Единый источник правды по архитектуре.** Перед работой над структурой, контрактами,
> стеком или пакетами — читать этот файл; после изменений — актуализировать его.

## Overview

**WeatherBot** — .NET 10 сервис, который по расписанию отправляет в Telegram-канал сводку
о текущей погоде в заданном городе (данные — HTTP API **Open-Meteo**, без ключа). Управление
ботом — через REST API; хостинг — локальный компьютер пользователя.

- **.NET версия**: .NET 10.0 (`net10.0`), последняя версия C#
- **Архитектура**: Clean Architecture (5 проектов: Contracts, Domain, Application, Infrastructure, Web)
- **База данных**: **нет** и не планируется
- **Внешние интеграции**: Telegram Bot API (Long Polling не используется — бот только отправляет);
  **Open-Meteo** (REST/JSON, **ключа не требует**)
- **API документация**: Scalar (OpenAPI v1), эндпоинт `/scalar/v1`
- **Фронтенд**: **отсутствует**. Планируется отдельный SPA на React; текущий REST API — его будущий бэкенд
- **Паттерн бизнес-ошибок**: Result (CSharpFunctionalExtensions)
- **Статус**: бэкенд собран и компилируется; живой запуск не проводился.
  Источник данных — Open-Meteo; тестовые проекты заводятся задачей `01-02`.

### Источник данных

**Open-Meteo.** Авторизация не нужна, тариф бесплатный для некоммерческого использования:
лимиты 10 000 запросов в сутки, 5 000 в час, 600 в минуту; лицензия CC-BY 4.0
с обязательной атрибуцией. Расход проекта — 48 запросов в сутки при штатном интервале
30 минут; «personal home automation» в условиях сервиса прямо отнесён к некоммерческому
использованию. Платные погодные API отклонены по решению пользователя (2026-09-21).

---

## Solution Structure

```
WeatherBot/                                  # корень проекта
├── README.md                                # обзор, API, запуск
├── ARCHITECTURE.md                          # этот файл
├── WORKFLOW.md                              # процесс, git-конвенции, команды
├── docs/agents/                             # конфиг инженерных скиллов: трекер, домен, триаж
├── .scratch/                                # фичи: spec.md, issues/, reports/, reviews/
└── backend/                                 # корень бэкенда
    ├── Directory.Build.props                # общие свойства сборки + анализаторы
    ├── Directory.Packages.props             # центральные версии пакетов
    ├── .globalconfig                        # уровни правил анализаторов
    ├── .gitignore
    └── WeatherBot/                          # каталог решения
        ├── WeatherBot.slnx                  # файл решения (5 проектов + тестовые)
        ├── src/
        │   ├── WeatherBot.Contracts/        # DTO, отдаваемые наружу
        │   ├── WeatherBot.Domain/           # модели предметной области и настройки
        │   ├── WeatherBot.Application/      # абстракции и оркестратор
        │   ├── WeatherBot.Infrastructure/   # интеграции, форматтер, фоновая задача
        │   └── WeatherBot.Web/              # REST API: точка входа, контроллеры, DI
        └── tests/
            ├── Directory.Build.props        # настройки тестовых проектов
            ├── WeatherBot.UnitTests/
            └── WeatherBot.ArchitectureTests/
```

### Описание проектов

| Проект | Назначение | Зависимости |
|--------|-----------|-------------|
| **WeatherBot.Contracts** | DTO, отдаваемые API наружу (`BotStatusResponse`, `BotActionResponse`) | — |
| **WeatherBot.Domain** | Модели предметной области (`WeatherInfo`, enum-ы) и классы настроек. **Только BCL, ни одного пакета** | — |
| **WeatherBot.Application** | Абстракции (`ITelegramSender`, `IWeatherProvider`, `IWeatherFormatter`, `IBotStateManager`, `ILogBuffer`) и оркестратор `WeatherDigestService` | Domain, CSharpFunctionalExtensions |
| **WeatherBot.Infrastructure** | Реализации абстракций: Telegram, Open-Meteo, форматтер, состояние, журнал в памяти, фоновая задача | Application, Domain, Telegram.Bot |
| **WeatherBot.Web** | REST API: `Program.cs`, DI-композиция, контроллеры, конфигурация | Contracts, Domain, Application, Infrastructure |
| **WeatherBot.UnitTests** | Модульные тесты: маппер, форматтер, состояние, буфер журнала, парсинг ответа провайдера | Domain, Application, Infrastructure |
| **WeatherBot.ArchitectureTests** | Тесты направления зависимостей между слоями | все `src`-проекты |

### Направления зависимостей

```
WeatherBot.Web ──▶ WeatherBot.Infrastructure ──▶ WeatherBot.Application ──▶ WeatherBot.Domain
        │
        └──▶ WeatherBot.Contracts
```

> **Правило**: зависимости всегда направлены **внутрь**. Domain не знает ни о DI, ни об HTTP,
> ни о Telegram; Application не знает об Infrastructure. `Contracts` — самостоятельный проект
> без ссылок: DTO наружу не используют типы Domain.

### Раскладка исходников

| Проект | Файлы |
|--------|-------|
| Domain | `WeatherInfo.cs`, `Cloudiness.cs`, `PrecipitationType.cs`, `PrecipitationStrength.cs`, `WindDirection.cs`, `TelegramSettings.cs`, `WeatherSettings.cs`, `WeatherBotSettings.cs` |
| Contracts | `Bot/BotStatusResponse.cs`, `Bot/BotActionResponse.cs` |
| Application | `Abstractions/*.cs`, `Services/WeatherDigestService.cs`, `DependencyInjectionExtension.cs` |
| Infrastructure | `Common/SecretMasker.cs`, `Messaging/TelegramSender.cs`, `Weather/OpenMeteoWeatherProvider.cs`, `Weather/OpenMeteoWeatherMapper.cs`, `Weather/OpenMeteoContracts.cs`, `Formatting/WeatherMessageFormatter.cs`, `State/BotStateManager.cs`, `BackgroundServices/WeatherBotBackgroundService.cs`, `Logging/InMemoryLogBuffer.cs`, `Logging/InMemoryLoggerProvider.cs`, `DependencyInjectionExtension.cs` |
| Web | `Program.cs`, `DependencyInjectionExtension.cs`, `Controllers/BotController.cs`, `Controllers/BotLogController.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` |

> Папка `Messaging` называется так намеренно: папка `Telegram` дала бы namespace
> `WeatherBot.Infrastructure.Telegram` и сломала бы `using Telegram.Bot` (коллизия имён).

---

## API

Все эндпоинты — REST, JSON. Документация — Scalar (`/scalar/v1`) вне Production.

| Метод | Маршрут | Назначение | Ответы |
|-------|---------|-----------|--------|
| POST | `/api/bot/start` | Запустить периодическую отправку | 200 — запущен; **409** — уже запущен |
| POST | `/api/bot/stop` | Остановить периодическую отправку | 200 — остановлен; **409** — уже остановлен |
| POST | `/api/bot/send-now` | Отправить сводку вне очереди (работает и на остановленном боте) | 200 — отправлено; **502** — погода не получена или Telegram отклонил |
| POST | `/api/bot/send-test` | Диагностика: отправить в канал тестовое сообщение, **не обращаясь** к API погоды | 200 — отправлено; **502** — Telegram отклонил |
| GET | `/api/bot/status` | `{ isRunning, lastSentAt, nextTickAt }` | 200 |
| GET | `/api/bot/logs?count=20` | Последние записи журнала (1…100) | 200 |
| GET | `/health` | `{ status: "ok" }` | 200 |
| GET | `/` | Текстовая заглушка «сервис жив» | 200 |

Тело ответов `start`/`stop`/`send-now` — `BotActionResponse { success, isRunning, message }`.

**Контроллеров два, и это требование анализатора `S6960`**: правило группирует действия
по используемым сервисам, и `logs` (только `ILogBuffer`) не связан с управлением ботом
(`IBotStateManager`, `WeatherDigestService`) — в одном классе это «две ответственности».

---

## Конфигурация

**Где что лежит:**

| Файл | Содержимое | В git |
|------|-----------|-------|
| `appsettings.json` | структура и несекретные значения по умолчанию (ID канала, город, координаты, интервал); `Telegram:BotToken` — **пустая заглушка** | да |
| `appsettings.Development.json` | реальные значения для локального запуска, включая `Telegram:BotToken` | **нет** — файл закрыт правилом `**/appsettings.Development.json` в `backend/.gitignore` |
| переменная окружения `Telegram__BotToken` | альтернативный способ задать токен (перекрывает файлы) | — |

> **Ключ погоды не нужен.** У Open-Meteo авторизации нет: ни секрета, ни переменной окружения
> для источника данных в конфигурации не остаётся (задача `01-01`).

Секции конфигурации:

| Секция | Поле | Тип | По умолчанию | Смысл |
|--------|------|-----|--------------|-------|
| `Telegram` | `BotToken` | string | `""` | токен от @BotFather |
| `Telegram` | `ChannelId` | string | `-1004293595218` | канал «Погода» (`@kalentsov_pogoda`); допустим также `@username` |
| `OpenMeteo` | `Latitude` | double | `55.7558` | широта (Москва) |
| `OpenMeteo` | `Longitude` | double | `37.6173` | долгота (Москва) |
| `OpenMeteo` | `City` | string | `Москва` | подпись в заголовке сообщения |
| `WeatherBot` | `IntervalMinutes` | int | `30` | интервал отправки |

Классы настроек (`TelegramSettings`, `WeatherSettings`, `WeatherBotSettings`) живут в **Domain**
и содержат константу `SectionName`. Биндинг — `IOptionsMonitor<T>` в
`Infrastructure/DependencyInjectionExtension.AddInfrastructure`.

**Настройки только читаются.** Запись в `appsettings.json` из приложения не реализована
(отложено вместе с админкой).

---

## Интеграция с Open-Meteo

**Запрос** (`GET`, ключа не требует):

```
https://api.open-meteo.com/v1/forecast
  ?latitude={Latitude}&longitude={Longitude}
  &current=temperature_2m,relative_humidity_2m,apparent_temperature,
           precipitation,rain,snowfall,weather_code,cloud_cover,
           pressure_msl,wind_speed_10m,wind_direction_10m,wind_gusts_10m
  &wind_speed_unit=ms
  &timezone=auto
```

**Поля ответа, которые использует проект:**

| Поле API | Единица | Куда идёт |
|----------|---------|-----------|
| `current.temperature_2m` | °C | `WeatherInfo.TemperatureC` |
| `current.apparent_temperature` | °C | `WeatherInfo.FeelsLikeC` |
| `current.relative_humidity_2m` | % | `WeatherInfo.HumidityPercent` |
| `current.pressure_msl` | **гПа** | `WeatherInfo.PressureMmHg` — перевод в мм рт.ст. |
| `current.wind_speed_10m` | м/с (запрошено `wind_speed_unit=ms`) | `WeatherInfo.WindSpeedMs` |
| `current.wind_direction_10m` | **градусы** | `WeatherInfo.WindDirection` — перевод в 8 румбов |
| `current.cloud_cover` | % | `WeatherInfo.Cloudiness` — по порогам |
| `current.weather_code` | код WMO | `WeatherInfo.PrecipitationType` |
| `current.precipitation` | мм/ч | `WeatherInfo.PrecipitationStrength` — по порогам |
| `current.time` + `utc_offset_seconds` | — | `WeatherInfo.ObservedAt` — момент наблюдения |

**Ловушки, из-за которых перевод обязателен:** давление приходит в **гектопаскалях**, а не
в мм рт.ст.; направление ветра — в **градусах**, а не румбами; тип осадков — **кодом WMO**,
а не строковым enum-ом. Точные пороги и таблица кодов — в постановке `01-01`.

**Атрибуция CC-BY 4.0 обязательна** — в `README.md` и в документации указывается, что данные
предоставлены Open-Meteo.

---

## Паттерны и правила

1. **Result Pattern**: ожидаемые бизнес-ошибки — `Result` / `Result<T>` (Application).
   Исключения — только для непредвиденных сбоев.
2. **Ошибки не покидают инфраструктуру**: `TelegramSender` ловит `ApiRequestException`,
   `RequestException`, `ArgumentException`; провайдер погоды — `HttpRequestException`,
   `TaskCanceledException` (таймаут), `JsonException`. Наружу — `Result.Failure(...)`.
3. **Секреты не хардкодятся**: пустой токен — не падение, а `Result.Failure` с записью в журнал.
   Настройка погоды секретов не содержит.
4. **Состояние — потокобезопасно**: `BotStateManager` (Singleton) использует `System.Threading.Lock`.
5. **Одновременные отправки сериализуются**: `WeatherDigestService` держит `SemaphoreSlim`,
   чтобы плановая и внеочередная отправка не наложились.
6. **Первый тик — через полный интервал**: `PeriodicTimer` не отправляет сообщение при старте.
7. **Интервал читается один раз при запуске.** Изменение `WeatherBot:IntervalMinutes` на лету
   не подхватывается и применяется после перезапуска (настройки пока read-only).
8. **Журнал — в консоль и в память**: `InMemoryLoggerProvider` (кастомный `ILoggerProvider`,
   уровень ≥ Information) пишет в `InMemoryLogBuffer` (кольцевой буфер на 100 записей).
9. **Минимум внешних зависимостей**: MediatR, AutoMapper, FluentValidation, Scrutor не используются.
10. **Маппинг значений API → домен — в маппере, домен → русский текст — в форматтере**:
    `OpenMeteoWeatherMapper` (числа и коды API → enum домена) → `WeatherMessageFormatter`
    (enum → русский).
11. **Слои не протекают**: `Contracts` не ссылается ни на что; `Domain` — только BCL;
    `Application` не знает об `Infrastructure`. Проверяется тестом в `WeatherBot.ArchitectureTests`.

### Формат сообщения

```
🌤 *Погода в Москве*

🌡 *Температура:* 5°C (ощущается как 2°C)
☁️ *Облачность:* Облачно
💧 *Влажность:* 78%
📊 *Давление:* 745 мм рт.ст.
💨 *Ветер:* 3 м/с, СЗ
🌧 *Осадки:* Дождь (слабые)

_Обновлено: 21.09.2026 14:30 (UTC+3)_
```

Разметка — legacy `Markdown` (`ParseMode.Markdown`). Заголовок берёт город из
`OpenMeteo:City`; время — локальное время точки с её смещением от UTC, как его вернул API
(`timezone=auto` + `utc_offset_seconds`). Жёстко зашитых «Москва» и «МСК» в форматтере нет.

**Маппинг значений API** (неизвестное значение → `Unknown` → «Нет данных» / «—», отправка не срывается):

| Группа | Значения API → домен → текст |
|--------|------------------------------|
| Облачность | `cloud_cover`, %: ясно / малооблачно / облачно / пасмурно |
| Осадки | `weather_code` (WMO) → Без осадков / Дождь / Снег / Град / Смешанные |
| Интенсивность | `precipitation`, мм/ч → нет / слабые / умеренные / сильные / очень сильные |
| Ветер | `wind_direction_10m`, градусы → С / СВ / В / ЮВ / Ю / ЮЗ / З / СЗ; штиль → «—» |

Точные пороги и таблица кодов WMO — в постановке `01-01`, обязательны к покрытию тестами
в `01-02`.

---

## Key Technologies & Libraries

Версии — только в `backend/Directory.Packages.props`.

| Пакет | Версия | Где | Назначение |
|-------|--------|-----|-----------|
| **Telegram.Bot** | 22.10.3.1 | Infrastructure | Telegram Bot API |
| **CSharpFunctionalExtensions** | 3.7.0 | Application | Result Pattern |
| **Microsoft.Extensions.Http** | 10.0.10 | Infrastructure | `IHttpClientFactory` для клиента Open-Meteo |
| **Microsoft.Extensions.Hosting.Abstractions** | 10.0.10 | Infrastructure | `BackgroundService`, `AddHostedService` |
| **Microsoft.Extensions.Options.ConfigurationExtensions** | 10.0.10 | Infrastructure | биндинг секций конфигурации |
| **Microsoft.AspNetCore.OpenApi** | 10.0.10 | Web | OpenAPI-спецификация |
| **Scalar.AspNetCore** | 2.16.16 | Web | интерактивная документация вместо Swagger UI |
| **xunit.v3** | 4.0.0 | tests | тестовый фреймворк; работает через Microsoft.Testing.Platform |
| **AwesomeAssertions** | 9.6.0 | tests | утверждения (свободный форк FluentAssertions: 8+ платная) |
| **NSubstitute** | 6.2.0 | tests | подмена `ITelegramSender`, `IWeatherProvider` |
| **TngTech.ArchUnitNET.xUnitV3** | 0.13.4 | ArchitectureTests | проверка направления зависимостей |

### Анализаторы

`Roslynator.Analyzers`, `Meziantou.Analyzer`, `AsyncFixer`, `SonarAnalyzer.CSharp`.
`TreatWarningsAsErrors=true`, `AnalysisMode=All` — предупреждение анализатора = ошибка сборки.

**Отключённые правила (`backend/.globalconfig`)** — каждое требует подтверждения пользователя:

| Правило | Причина |
|---------|---------|
| `CA1716` | `IBotStateManager.Start()`/`Stop()` совпадают с ключевым словом VB; имена зафиксированы постановкой |
| `CA1031` | требование постановки: «все исключения ловить внутри цикла, чтобы сервис не падал» |

Остальные отключения в `.globalconfig` снабжены комментариями, часть которых унаследована
от другого проекта и к WeatherBot не относится (находка `F-08`) — приводится в порядок отдельной
задачей.

Порядок работы с новыми замечаниями: сначала правим код; подавление — решение пользователя.

---

## Безопасность

| Мера | Где | Что даёт |
|------|-----|----------|
| Токен не в репозитории | `appsettings.Development.json` + правило `**/appsettings.Development.json` в `backend/.gitignore` | токен не попадёт в коммит; проверено `git check-ignore` |
| Маскировка секретов в логах и ответах API | `Infrastructure/Common/SecretMasker.cs` | сообщения сетевых исключений могут содержать URL Telegram с токеном — они очищаются |
| Объект исключения не логируется в Telegram-клиенте | `Messaging/TelegramSender.cs` | `ToString()` исключения не может утащить токен в журнал и в `/api/bot/logs`; логирование вынесено из `catch` |
| Только локальный адрес | `Properties/launchSettings.json` → `http://localhost:5157` | сервис не слушает внешние интерфейсы |
| Фильтр Host-заголовка | `appsettings.json` → `AllowedHosts: localhost;127.0.0.1` | запросы с чужим Host отклоняются |
| Документация API вне Production | `Program.cs` | `/scalar/v1` и `/openapi/v1.json` не публикуются в Production |
| Отсутствие ключа у источника погоды | Open-Meteo | нечего утекать: аутентификации у API нет |

**Осознанные ограничения** (приняты для локального запуска, не дефекты):

- **Аутентификации нет** — любой, кто имеет доступ к `http://localhost:5157`, может писать в канал.
  Не выставлять сервис в сеть без аутентификации.
- **Токен хранится в файле** внутри каталога проекта. Файл закрыт от git, но лежит на диске;
  при передаче каталога третьим лицам токен уедет вместе с ним.
- Токен, единожды опубликованный (чат, скриншот, лог), считается скомпрометированным —
  перевыпускается у @BotFather командой `/revoke`.

---

## Тестирование

- **Раскладка**: `backend/WeatherBot/tests/WeatherBot.UnitTests` и
  `backend/WeatherBot/tests/WeatherBot.ArchitectureTests`. **IntegrationTests не заводятся**:
  внешние швы (Telegram, Open-Meteo) подменяются, БД нет, поднимать нечего.
- **Стек**: **xUnit v3** через **Microsoft.Testing.Platform**, тестовые проекты —
  `OutputType=Exe`, `UseMicrosoftTestingPlatformRunner=true`, `TestingPlatformDotnetTestSupport=true`.
- **`tests/Directory.Build.props` обязателен** и обязан явно импортировать родительский
  `backend/Directory.Build.props`: MSBuild использует только ближайший файл, без импорта
  тестовые проекты теряют `TargetFramework`, анализаторы и общие свойства.
- Цикл перед сдачей задачи: сборка без ошибок и предупреждений → все тесты зелёные.
- Сетевые вызовы в тестах запрещены: `ITelegramSender` и `IWeatherProvider` подменяются,
  HTTP-ответы провайдера — через подставной `HttpMessageHandler`.

**Ограничения среды агента при прогоне тестов** (подробности — `WORKFLOW.md` §2):

- `dotnet test` в песочнице падает на именованных каналах — тесты запускаются **прямым вызовом
  собранной сборки**;
- `restore` и `build` требуют `-m:1`; без флага команда падает молча с кодом 1;
- **restore доступен агенту** офлайн, пока пакеты есть в локальном кэше.

---

## Команды

```bash
# каталог: backend/WeatherBot
dotnet restore WeatherBot.slnx -m:1 -p:NuGetAudit=false     # офлайн из локального кэша
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore      # -m:1 обязателен и здесь, и в restore
dotnet tests/WeatherBot.UnitTests/bin/Debug/net10.0/WeatherBot.UnitTests.dll
dotnet tests/WeatherBot.ArchitectureTests/bin/Debug/net10.0/WeatherBot.ArchitectureTests.dll
dotnet run --project src/WeatherBot.Web                     # запускает пользователь
```

Адрес по `launchSettings.json` — `http://localhost:5157`, документация — `/scalar/v1`.

**Ограничения среды агента** (проверено 2026-09-21; подробности — `WORKFLOW.md` §2):
без `-m:1` и `restore`, и `build` падают молча с кодом 1;
без `-p:NuGetAudit=false` отсутствие сети даёт `NU1900`, который при `TreatWarningsAsErrors`
становится ошибкой; `dotnet test` на MTP падает на именованном канале — тесты запускаются
прямым вызовом собранной сборки. **Restore доступен агенту** офлайн, пока пакеты есть
в локальном кэше.

---

## Key Files

| Файл | Зачем читать |
|------|-------------|
| `ARCHITECTURE.md` | этот файл |
| [`.scratch/`](.scratch/) | спецификации и тикеты фич: что в работе и что сдано |
| `backend/Directory.Packages.props` | центральные версии пакетов |
| `backend/.globalconfig` | реестр отключённых правил анализаторов |
| `src/WeatherBot.Web/Program.cs` | точка входа и middleware |
| `src/WeatherBot.Web/Controllers/BotController.cs` | управление ботом |
| `src/WeatherBot.Infrastructure/DependencyInjectionExtension.cs` | вся регистрация инфраструктуры |
| `src/WeatherBot.Infrastructure/BackgroundServices/WeatherBotBackgroundService.cs` | расписание отправки |
| `src/WeatherBot.Application/Services/WeatherDigestService.cs` | оркестрация «погода → текст → канал» |

Последнее обновление: 2026-09-22

## Владение и порядок изменения

- Единственный владелец файла — архитектор (DeepSeek V4.x Flash).
- Исполнитель читает и следует файлу; менять его запрещено.
- Порядок: подтверждение пользователя → правка `ARCHITECTURE.md` → постановка исполнителю.

## Известные проблемы

> Живой бэклог проекта. Сюда переехал реестр находок из удалённого `memory-bank/snapshot.md` §3
> (2026-09-21). Закрытая находка строку не сохраняет — вычёркивается. Реестр отключённых правил
> анализаторов — `backend/.globalconfig` и §«Анализаторы».

| ID | Проблема | Где | Значимость |
|----|----------|-----|-----------|
| F-02 | Состояние бота не переживает перезапуск: после рестарта `IsRunning=false`, фоновая задача молчит | `Infrastructure/State/BotStateManager.cs`, `BackgroundServices/WeatherBotBackgroundService.cs` | высокая |
| F-03 | Тесты не дописаны: контур и 68 тестов маппера/форматтера готовы (`01-03`), остальное — `01-04`, `01-05` | `backend/WeatherBot/tests/` | высокая |
| F-06 | `TelegramBotClient` создаётся заново на каждую отправку; таймаут отправки не задан | `Infrastructure/Messaging/TelegramSender.cs` | средняя |
| F-08 | `.globalconfig` тащит обоснования из чужого проекта («vertical slice», «DDD-сущности», «EF shadow props», «Serilog») | `backend/.globalconfig` | средняя |
| F-09 | `NextTickAt` выставляется и при остановленном боте; сразу после `start` бывает `null` | `Infrastructure/BackgroundServices/WeatherBotBackgroundService.cs` | низкая |
| F-10 | `Contracts` ссылается на `Domain`, но не использует ни одного его типа | `WeatherBot.Contracts.csproj`, §«Направления зависимостей» | низкая |
| F-11 | `ParseMode.Markdown` — legacy-разметчик: ошибка разбора ломает отправку целиком | `Infrastructure/Messaging/TelegramSender.cs` | низкая |
| F-13 | Токен бота считается скомпрометированным (передан открытым текстом), `/revoke` не сделан | конфигурация, действие пользователя | высокая |
| F-14 | Три граничных дефекта, отложенных ревью `01-01` в отменённую `01-02` и не попавших ни в одну постановку: смещение печатается как `(int)offset.TotalHours` (зоны `:30`/`:45` теряют минуты); отрицательная `precipitation` попадает в `_` и даёт `VeryHeavy`; `(int)c.RelativeHumidity2m` усекает влажность вместо округления | `Infrastructure/Formatting/WeatherMessageFormatter.cs:66`, `Infrastructure/Weather/OpenMeteoWeatherMapper.cs:88`, `Infrastructure/Weather/OpenMeteoWeatherProvider.cs:103` | низкая |

Закрытые (для справки, действий не требуют): F-01 — платный тариф делал сводку неотправляемой;
F-04 — проект не был в git; F-05 — captive dependency `IWeatherProvider`; F-07 — «Москва»/«МСК»
константами; F-12 — `ObservedAt` вместо времени наблюдения из API. Закрыты задачами `01-01`, `01-03`.

## TODO (Next Steps)

- [x] Создать бота у @BotFather, добавить его администратором канала, заполнить `Telegram:*`
- [x] Проверить связку «бот → канал»: тестовое сообщение доставлено (2026-09-18)
- [x] Отказаться от платного источника погоды в пользу Open-Meteo (2026-09-21)
- [x] `01-01`: убран платный источник, переход на Open-Meteo, город — в настройках (2026-09-21)
- [ ] `.scratch/weather-provider`: тестовый контур, маппер и форматтер готовы (68 тестов); остались
      тикеты `01` (провайдер, оркестрация, композиция DI), `02` (состояние, буфер журнала,
      маскировщик, направления зависимостей), `03` (граничные дефекты `F-14`)
- [ ] **Перевыпустить токен бота у @BotFather** — текущий был передан открытым текстом
      и считается скомпрометированным (`F-13`)
- [ ] Автозапуск периодической отправки после перезапуска приложения (`F-02`)
- [ ] Проверить полный цикл живьём: `/api/bot/start`, `send-now`, приход сводки в канал по расписанию
- [ ] Решить судьбу отключённых правил `CA1716` и `CA1031` (подтвердить или переименовать/переписать)
- [ ] Привести в порядок обоснования в `backend/.globalconfig` (`F-08`)
- [ ] Разобрать остальные находки реестра (`F-06`, `F-09`, `F-10`, `F-11`, `F-14`)
- [ ] Запись настроек из приложения (страница/эндпоинт) — отложено до админки
- [ ] React SPA поверх текущего REST API — отдельный этап
