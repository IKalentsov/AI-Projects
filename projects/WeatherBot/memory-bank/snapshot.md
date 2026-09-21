# Snapshot — WeatherBot

> **Что это.** Слепок состояния проекта, снятый агентом при входе в задачу: инвентарь,
> чем и что проверено, реестр находок. Нужен, чтобы **не прогонять проект целиком заново**.
> Обновляется по факту изменения; при расхождении с кодом — **верен код**.
>
> **Не дублировать** `ARCHITECTURE.md` (устройство), `memory-bank/activeContext.md`
> (где мы сейчас) и `backend/Directory.Packages.props` (версии).

**Снят:** 2026-09-21 · архитектор (DeepSeek V4.x Flash)
**Обновлялся:** 2026-09-21

---

## 1. Инвентарь (что где лежит)

Корень: `H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\`

### Документы и процесс — в корне проекта

| Путь | Что |
|---|---|
| `README.md` | обзор, API, запуск, безопасность |
| `ARCHITECTURE.md` | единый источник правды по устройству |
| `WORKFLOW.md` | процесс и git-конвенции проекта |
| `memory-bank/activeContext.md`, `progress.md`, этот файл | состояние между сессиями |
| `ai-tasks/` + `_templates/{task,report,review,fixes}.md` | артефакты задач |
| `.dsh/AGENTS.md` | проектные правила агента |
| `.clinerules/` | **легаси Cline, агентом не читается** |

### Код — `backend/`

| Путь | Содержимое |
|---|---|
| `Directory.Build.props` | `net10.0`, `TreatWarningsAsErrors`, `AnalysisMode=All`, 4 анализатора |
| `Directory.Packages.props` | все версии пакетов (центрально) |
| `.globalconfig` | уровни правил анализаторов |
| `.gitignore` | закрывает `**/appsettings.Development.json` |
| `WeatherBot/WeatherBot.slnx` | решение, 5 проектов |
| `WeatherBot/src/WeatherBot.Domain/` | `WeatherInfo`, 4 enum-а, 3 класса настроек — **только BCL** |
| `WeatherBot/src/WeatherBot.Contracts/` | `Bot/BotStatusResponse.cs`, `Bot/BotActionResponse.cs` |
| `WeatherBot/src/WeatherBot.Application/` | 5 абстракций в `Abstractions/`, `WeatherDigestService`, `AddApplication()` |
| `WeatherBot/src/WeatherBot.Infrastructure/` | `Weather/` (провайдер + маппер + контракты API), `Messaging/TelegramSender.cs`, `Formatting/`, `State/`, `Logging/`, `Common/SecretMasker.cs`, `BackgroundServices/`, `AddInfrastructure()` |
| `WeatherBot/src/WeatherBot.Web/` | `Program.cs`, `DependencyInjectionExtension.cs`, `Controllers/` (2 шт.), `appsettings*.json`, `Properties/launchSettings.json` |
| `WeatherBot/tests/` | **отсутствует** |

**Тестовые проекты ещё не создавались.** Пакеты для них уже есть в локальном кэше — см. §4.

---

## 2. Проверки (чем проверено и что вышло)

| Проверка | Команда | Результат | Дата |
|---|---|---|---|
| Сборка | `dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore` (каталог `backend/WeatherBot`) | успешно, `Ошибок: 0`, `Предупреждений: 0` | 2026-09-21 |
| Сборка **без** `-m:1` | `dotnet build WeatherBot.slnx -c Debug --no-restore` | **код 1**, вывод пуст, сводка врёт: `Ошибок: 0` | 2026-09-21 |
| Restore офлайн | `dotnet restore WeatherBot.slnx -m:1 -p:NuGetAudit=false` | exit 0, восстановлены все 5 проектов | 2026-09-21 |
| Restore **без** `-m:1` | `dotnet restore WeatherBot.slnx -p:NuGetAudit=false` | код 1, ни строки вывода | 2026-09-21 |
| Запись в кэш NuGet | `New-Item` в `~/.nuget/packages` | `UnauthorizedAccessException` — но на restore не влияет | 2026-09-21 |
| `dotnet test` на MTP | пробный проект xunit.v3 + `global.json` | `UnauthorizedAccessException` в `NamedPipeClientStream.TryConnect` | 2026-09-21 |
| Прямой запуск тестов | `dotnet bin/Debug/net10.0/<Tests>.dll` | `Пройден! всего: 1, сбой: 0` | 2026-09-21 |
| Git — проект под контролем | `git -C <корень> ls-files -- ":/projects/WeatherBot"` | **76 файлов**, коммит `3e7f6d9` (2026-09-21), статус чистый | 2026-09-21 |
| Секрет не попал в git | `git ls-files` по `appsettings.Development.json` | не отслеживается — правило `**/appsettings.Development.json` работает | 2026-09-21 |
| Правила ignore | `git check-ignore -v --no-index` на пробных и тестовых путях | `_probe*/`, `[Oo]bj/`, `[Bb]in/` ловятся; реальный `.cs` — не ловится | 2026-09-21 |
| Кэш NuGet (тестовые пакеты) | перебор `~/.nuget/packages` | `xunit.v3 4.0.0`, `microsoft.testing.platform 2.4.0`, `microsoft.testing.extensions.trxreport 2.4.0`, `tngtech.archunitnet(.xunitv3) 0.13.4`, `awesomeassertions 9.6.0`, `nsubstitute 6.2.0`, `coverlet.collector 6.0.4` — **есть** | 2026-09-21 |
| Секреты в dev-конфиге | ключи без вывода значений | `Telegram:BotToken` — задан; `Telegram:ChannelId` — задан; ключа погоды нет и не требуется | 2026-09-21 |
| Живой запрос Open-Meteo (без ключа) | `GET api.open-meteo.com/v1/forecast?...` | HTTP 200, валидный JSON с текущей погодой по Москве | 2026-09-21 |

> **Ловушка измерения (дважды стоила неверного вывода 2026-09-21).** Пути **и в git-pathspec,
> и в `Test-Path`/`Get-ChildItem`** разрешаются **относительно текущего каталога**. Из каталога
> проекта `git ls-files -- projects/WeatherBot` ищет `WeatherBot/projects/WeatherBot` и молча
> отдаёт пусто, а `Test-Path 'projects/WeatherBot/_probe'` проверяет несуществующий путь и
> возвращает `False` — хотя каталог на месте. Оба раза это привело к ложному выводу.
> **Правило: проверять от корня, для git использовать магию `:/projects/WeatherBot`,
> а относительные пути — только от текущего каталога.**

**Пробелы (здесь снапшот бессилен, нужен запуск приложения):** приложение ни разу не стартовало;
эндпоинты не вызывались; плановая отправка не наблюдалась; реальные ключи не проверялись.

---

## 3. Реестр находок

Статусы: `открыто` → `в работе` → `закрыто` / `отклонено` / `отложено`.
**При закрытии находки — обновлять строку здесь, а не только в отчёте задачи.**

| ID | Находка | Где | Значимость | Статус |
|----|---------|-----|-----------|--------|
| F-01 | Ограничение платного тарифа делало сводку неотправляемой вовсе. **Закрыто решением о переходе на Open-Meteo** | `Infrastructure/Weather/` | блокирующая | закрыто решением (`01-01`) |
| F-02 | Состояние бота не переживает перезапуск: после рестарта `IsRunning=false`, фоновая задача молчит | `Infrastructure/State/BotStateManager.cs`, `BackgroundServices/WeatherBotBackgroundService.cs` | высокая | открыто |
| F-03 | Тестов нет вообще, а DoD их требует | `WeatherBot/tests/` | высокая | в работе (`01-02`) |
| F-04 | ~~Проект не в git~~ — **закрыто 2026-09-21**: проект закоммичен (`3e7f6d9`), 76 файлов под контролем; секрет `appsettings.Development.json` в git не попал | репозиторий `AI-Projects` | высокая | **закрыто** |
| F-05 | Captive dependency: `WeatherDigestService` (Singleton) захватывал `IWeatherProvider` (Transient через `AddHttpClient`). **Закрыто в `01-01`**: провайдер берёт `IHttpClientFactory` и сам зарегистрирован синглтоном | `Infrastructure/DependencyInjectionExtension.cs` | средняя | **закрыто** (`01-01`) |
| F-06 | `TelegramBotClient` создаётся заново на каждую отправку; таймаут отправки не задан | `Infrastructure/Messaging/TelegramSender.cs` | средняя | открыто |
| F-07 | «Москва» и «МСК» были зашиты константами, а координаты — в настройках. **Закрыто в `01-01`**: город берётся из `OpenMeteo:City`, смещение времени — из ответа API | `Infrastructure/Formatting/WeatherMessageFormatter.cs` | средняя | **закрыто** (`01-01`) |
| F-08 | `.globalconfig` тащит обоснования из чужого проекта («vertical slice», «DDD-сущности», «EF shadow props», «Serilog») — к WeatherBot не относятся | `backend/.globalconfig` | средняя | открыто |
| F-09 | `NextTickAt` выставляется и при остановленном боте; сразу после `start` бывает `null` | `Infrastructure/BackgroundServices/WeatherBotBackgroundService.cs` | низкая | открыто |
| F-10 | `Contracts` ссылается на `Domain`, но не использует ни одного его типа | `WeatherBot.Contracts.csproj`, `ARCHITECTURE.md` (диаграмма) | низкая | открыто |
| F-11 | `ParseMode.Markdown` — legacy-разметчик; ошибка разбора ломает отправку целиком | `Infrastructure/Messaging/TelegramSender.cs` | низкая | открыто |
| F-12 | `ObservedAt` = время получения, а не время наблюдения из API. **Закрыто в `01-01`**: момент наблюдения собирается из `current.time` + `utc_offset_seconds` | `Infrastructure/Weather/` | низкая | **закрыто** (`01-01`) |
| F-13 | Токен бота считается скомпрометированным (передан открытым текстом), `/revoke` не сделан | конфигурация, действие пользователя | высокая | открыто |

---

## 4. Внешние факты (проверено по документам и живым запросом)

### Open-Meteo (выбранный источник)

- `GET https://api.open-meteo.com/v1/forecast`, **ключ не нужен**.
- Бесплатно для некоммерческого использования: < 10 000 запросов/сутки, лицензия CC-BY 4.0.
  «Personal home automation» прямо назван некоммерческим. Наш расход — 48 запросов/сутки.
- Живой запрос по Москве отдал: `temperature_2m 13.7 °C`, `relative_humidity_2m 85 %`,
  **`apparent_temperature 13.0` (аналог `feelsLike`)**, `cloud_cover 98 %`,
  **`pressure_msl 1012.8 hPa`**, **`wind_speed_10m 7.6 km/h`**, `wind_direction_10m 183 °`,
  `weather_code 3`, `timezone Europe/Moscow`, `utc_offset_seconds 10800`.
- **Ловушки для маппинга:** давление в **гПа** (1012.8 гПа ≈ 759.6 мм рт.ст.), ветер в **км/ч**
  (или параметр `wind_speed_unit=ms`), направление ветра в **градусах**, тип осадков — через
  **`weather_code` (WMO)** либо `precipitation`/`rain`/`snowfall`.
- Требуется атрибуция CC-BY 4.0.
- Источники: [Terms](https://open-meteo.com/en/terms), [Docs](https://open-meteo.com/en/docs).

### Gismeteo / Росгидромет

- Gismeteo API продаётся **по заявке** через портал-маркетплейс — self-service бесплатного тарифа нет.
- Публичного self-service API Росгидромета/«Метеоинфо» для разработчиков не найдено.

### Среда агента (перепроверено прогонами 2026-09-21, не по чужой документации)

- **`-m:1` обязателен и для `restore`, и для `build`**: без него команда завершается кодом 1,
  не напечатав ни строки, а сводка врёт — `Ошибок: 0`. Это касается и многопроцессной сборки,
  и MSBuild-узлов.
- **`restore` доступен агенту офлайн**: `dotnet restore ... -m:1 -p:NuGetAudit=false` → exit 0.
  Прежнее утверждение «restore из песочницы невозможен» — **неверно**, унаследовано из чужого
  документа без проверки.
- **Запись в глобальный кэш NuGet запрещена** (`UnauthorizedAccessException`), но на restore
  не влияет: для пакетов, уже лежащих в кэше, NuGet в него не пишет.
- **`dotnet test` на MTP падает** на `NamedPipeClientStream.TryConnect`; обход — прямой запуск
  собранной сборки. На .NET 10 перед этим нужен `global.json` с
  `{"test": {"runner": "Microsoft.Testing.Platform"}}`, иначе ошибка «VSTest target is no longer
  supported».
- **`NU1900`** (недоступен индекс уязвимостей) при `TreatWarningsAsErrors` становится ошибкой —
  лечится `-p:NuGetAudit=false`.
- Образец конвенции тестовых проектов — `projects/marketsniper-mvp/backend/tests/`
  (тот же монорепозиторий).

---

## 5. Решения и открытые вопросы

**Решено пользователем 2026-09-21:**

- **Источник погоды — Open-Meteo** (бесплатно, без ключа). Платные погодные API отклонены,
  публичного API Росгидромета нет. Зафиксировано в `ARCHITECTURE.md`.
- **Город выносится в настройки**, значение по умолчанию — «Москва»
  (`OpenMeteo:City`), часовой пояс берётся из ответа API (`timezone=auto`).
- **Разбивка на задачи — на усмотрение архитектора.**

**Открыто:**

- [ ] Поведение после перезапуска приложения: автозапуск отправки или только вручную (F-02).
- [ ] Перевыпуск токена бота (`/revoke`) (F-13).
- [ ] Взять проект под контроль версий (F-04).

---

## 6. Как обновлять этот файл

1. Обновлять **сразу после прогона**, а не по памяти: команду и результат — дословно.
2. §1 — при добавлении/удалении файлов и проектов.
3. §3 — статус находки меняется вместе с закрывающим её отчётом/ревью.
4. §4 — при появлении новых внешних фактов; указывать дату проверки и источник.
5. §5 — вычёркивать решённое, решения переносить в `activeContext.md`.
6. Дата в шапке — обязательна. Файл держать коротким: подробности живут в `ARCHITECTURE.md`
   и в артефактах `ai-tasks/`.
