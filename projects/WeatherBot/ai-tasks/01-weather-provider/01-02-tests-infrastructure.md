# 01-02-tests-infrastructure

**Требует изменения архитектуры:** да — уже внесено архитектором в `ARCHITECTURE.md`
(разделы «Solution Structure», «Тестирование», «Команды») и в `WORKFLOW.md` §1–2.
**Статус:** **отменена** — провалена 2026-09-21 по исчерпанию контекста исполнителя.
Заменена на три задачи: `01-03-tests-mapper-and-formatter`, `01-04-tests-provider-digest-di`,
`01-05-tests-state-buffer-layers`. **Исполнителю не передавать.** Разбор — в
`memory-bank/snapshot.md` §2–3 и `memory-bank/activeContext.md`.
**Задача:** 01-weather-provider
**Зависит от:** `01-01-remove-yandex` — сборка обязана быть зелёной до начала работы
**Слой / проект:** `tests` (продакшн-код `src/**` не меняется)

---

## 1. Цель

В решении появляются тестовые проекты `WeatherBot.UnitTests` и `WeatherBot.ArchitectureTests`
и первый набор тестов. После задачи прогон тестов становится обязательной частью цикла
и одной командой ловит регрессии в маппинге погоды, форматировании и слоях.

**Продакшн-код не меняется ни на строку.**

## 2. Контекст

**Обязательно прочитать до начала работы** (в этом порядке): `memory-bank/snapshot.md`,
`memory-bank/activeContext.md`, `ARCHITECTURE.md` (разделы «Тестирование», «Интеграция
с Open-Meteo», «Формат сообщения»), `WORKFLOW.md` §2, `.dsh/AGENTS.md`.

- Корень проекта: `projects/WeatherBot/`; решение — `backend/WeatherBot/WeatherBot.slnx`.
- Стек: .NET 10, Clean Architecture, Result Pattern (`CSharpFunctionalExtensions`).
- Тестовых проектов сейчас **не существует**. Сборка зелёная.
- Провайдер погоды — `OpenMeteoWeatherProvider` (+ `OpenMeteoWeatherMapper`,
  `OpenMeteoContracts`) в `Infrastructure/Weather/`.

**Образец конвенции тестового проекта** — в этом же монорепозитории:
`projects/marketsniper-mvp/backend/tests/` (`Directory.Build.props` и `*.csproj` из
`MarketSniper.UnitTests` / `MarketSniper.ArchitectureTests`). Читать как образец и адаптировать,
**не копировать вслепую**: у MarketSniper есть БД, Redis и integration-тесты, у нас их нет.

**Что уже сформулировано у вендора — загрузить и следовать, а не изобретать.** Четыре
официальных скилла; **загружать через `open_skill` по точному имени** — в каталоге сессии их
описаний нет, поэтому искать по имени или через `find_skills`:

| Скилл | Зачем в этой задаче |
|-------|---------------------|
| `scaffold-dotnet-test-project` | подключение тестового проекта к решению (`WeatherBot.slnx`) |
| `directory-build-organization` | иерархия `Directory.Build.props`, центральные версии пакетов |
| `migrate-vstest-to-mtp` | свойства MTP, код выхода 8, ловушка `$(IsTestProject)` |
| `run-tests` | синтаксис `dotnet test` и фильтры |

**В отчёте указать** (§8 шаблона), какие из них загружены и что именно из них применено.
«Скиллы не использовались» — тоже допустимый ответ, но он должен быть написан явно.

### Ограничения среды исполнителя

Коротко (проверено прогонами 2026-09-21): `-m:1` обязателен и для `restore`, и для `build` —
без него команда завершается кодом 1, не напечатав ни строки; `dotnet restore` **доступен
исполнителю** (офлайн из локального кэша) и требует `-p:NuGetAudit=false`; `dotnet test`
на MTP падает на именованном канале — тесты запускаются прямым вызовом собранной сборки.

### Версии пакетов — только центрально

Версии — в `backend/Directory.Packages.props`; в `.csproj` — `PackageReference` **без** `Version`.
Нужные версии уже в локальном кэше NuGet, сети для них не потребуется:

| Пакет | Версия |
|-------|--------|
| `xunit.v3` | `4.0.0` |
| `AwesomeAssertions` | `9.6.0` |
| `NSubstitute` | `6.2.0` |
| `TngTech.ArchUnitNET` | `0.13.4` |
| `TngTech.ArchUnitNET.xUnitV3` | `0.13.4` |

`Microsoft.NET.Test.Sdk` **не нужен**: xUnit v3 работает через Microsoft.Testing.Platform.

## 3. Файлы

**Создать:**

- `backend/WeatherBot/tests/Directory.Build.props`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/WeatherBot.UnitTests.csproj` + файлы тестов
- `backend/WeatherBot/tests/WeatherBot.ArchitectureTests/WeatherBot.ArchitectureTests.csproj` + файлы тестов

**Изменить:**

- `backend/WeatherBot/WeatherBot.slnx` — добавить оба проекта;
- `backend/Directory.Packages.props` — добавить версии из §2 (это **разрешено** и требуется
  задачей; в остальном файл не трогать).

**Не трогать:** всё в `src/**`, `backend/Directory.Build.props`, `backend/.globalconfig`,
все `.md`-артефакты.

## 4. Шаги

1. Добавить версии тестовых пакетов в `backend/Directory.Packages.props` (отдельная группа
   с комментарием-назначением).
2. Создать `tests/Directory.Build.props`.
3. Создать `WeatherBot.UnitTests` и написать тесты из §6.
4. Создать `WeatherBot.ArchitectureTests` и написать тест из §6.
5. Добавить оба проекта в `WeatherBot.slnx`.
6. Прогнать приёмку из §7.

## 5. Контракты и форматы данных

### `tests/Directory.Build.props` — обязательные требования

1. **Явно импортирует родительский `backend/Directory.Build.props`** — иначе MSBuild
   не унаследует `TargetFramework` и анализаторы, и сборка упадёт (`NETSDK1013`).
   Способ импорта — как в образце MarketSniper.
2. Помечает проекты как тестовые и не упаковываемые.
3. **Снимает только правила, несовместимые с природой тестов**, с комментарием-обоснованием
   на каждое (длинные имена с подчёркиваниями, «магические» значения в сценариях).
   Ориентир — набор из образца MarketSniper. Больше ничего не отключать:
   `TreatWarningsAsErrors=true` остаётся в силе.

### Тестовые проекты

- `OutputType=Exe` — обязательное требование MTP (кроме `MSTest.Sdk`, который ставит его сам);
- `IsPackable=false`;
- раннер xUnit v3 на MTP включается свойством `UseMicrosoftTestingPlatformRunner=true`;
- **интеграция с `dotnet test`:** на .NET 10 обязательна секция `test` в `global.json`
  (`backend/WeatherBot/global.json`), содержимое —
  `{"test": {"runner": "Microsoft.Testing.Platform"}}`. Без неё `dotnet test` отказывается
  работать: *«Testing with VSTest target is no longer supported … on .NET 10 SDK and later»*.
  Свойство `TestingPlatformDotnetTestSupport=true` — способ для .NET 9 и младше,
  **на .NET 10 не работает**.
  **Если `global.json` сломает сборку — удалить его** и сказать об этом в отчёте: сборка
  и прямой прогон тестов от него не зависят, `dotnet test` нужен только пользователю и CI;
- **если свойства MTP выносятся в `tests/Directory.Build.props`, не condition на
  `$(IsTestProject)`** — на момент вычисления этого файла свойство ещё не задано. Условие
  строится на имени проекта, например `$(MSBuildProjectName.EndsWith('Tests'))`;
- ссылки: `WeatherBot.UnitTests` → `Domain`, `Application`, `Infrastructure`;
  `WeatherBot.ArchitectureTests` → все пять проектов `src`;
- пакеты — без версий.

### Тест на зависимости слоёв

Проверяется **фактическое** направление зависимостей:

- `WeatherBot.Domain` не ссылается ни на один другой проект решения;
- `WeatherBot.Application` → только `WeatherBot.Domain`;
- `WeatherBot.Contracts` не ссылается ни на один проект решения;
- `WeatherBot.Infrastructure` не ссылается на `WeatherBot.Web`;
- `WeatherBot.Web` — единственный, кто видит и `Infrastructure`, и `Contracts`.

Реализация — на `TngTech.ArchUnitNET` (см. образец MarketSniper).
**Проверка обязана падать при нарушении** — иначе она бесполезна.

### Тест на композицию зависимостей — обязателен

Собирается **реальная** коллекция сервисов (`AddInfrastructure` над конфигурацией, эквивалентной
`appsettings.json`) и проверяется, что все регистрации резолвятся — как минимум `IWeatherProvider`,
`IWeatherFormatter`, `ITelegramSender`, `IBotStateManager`, `ILogBuffer`.

Почему обязателен: компилятор не ловит незарегистрированную зависимость. В задаче `01-01`
`IHttpClientFactory` остался незарегистрированным при полностью зелёной сборке, и дефект виден
только при резолве — то есть при запуске. Без этого теста класс ошибок «DI разъехался»
остаётся невидимым для агента, который проект не запускает.

## 6. Тесты

| Уровень | Проект | Что покрыть |
|---------|--------|-------------|
| unit | `WeatherBot.UnitTests` | `OpenMeteoWeatherMapper`, `OpenMeteoWeatherProvider`, `WeatherMessageFormatter`, `BotStateManager`, `InMemoryLogBuffer`, `WeatherDigestService`, `SecretMasker` |
| architecture | `WeatherBot.ArchitectureTests` | направление зависимостей между слоями |
| integration | — | **не заводится**: БД нет, внешние швы подменяются |

Сетевые вызовы запрещены: HTTP подменяется подставным `HttpMessageHandler`.

Обязательные сценарии:

`OpenMeteoWeatherMapper`
- давление: гПа → мм рт.ст., включая округление половины;
- ветер: все 8 румбов **и обе границы сектора**, значение вне `[0, 360)` → `Unknown`;
- облачность: все 4 порога **и значения ровно на границе**;
- осадки: каждый код WMO из таблицы контракта, приоритет `Mixed`, обнуление при нулевых осадках;
- интенсивность: все 5 диапазонов **и границы**;
- неизвестный код → `Unknown`.

`OpenMeteoWeatherProvider`
- успешный ответ (эталонный JSON из `01-01`) → корректный `WeatherInfo`,
  включая `ObservedAt` со смещением;
- HTTP 500 → `Result.Failure`; таймаут → `Result.Failure`; битый JSON → `Result.Failure`;
  ответ без блока `current` → `Result.Failure`;
- в сформированном запросе присутствуют `wind_speed_unit=ms` и `timezone=auto`.

`WeatherMessageFormatter`
- город подставляется из настроек (проверить на двух разных городах);
- в тексте **нет** «МСК»; время и смещение берутся из `ObservedAt`;
- значения `Unknown` → «Нет данных» / «—»; осадки `None`/`Unknown` — без скобки с интенсивностью.

`BotStateManager`
- стартовое состояние: не запущен, `LastSentAt` и `NextTickAt` пусты;
- `Start()` / `Stop()` меняют `IsRunning`; `Stop()` обнуляет `NextTickAt`;
- `MarkSent` / `MarkNextTick` проставляют значения; `MarkNextTick(null)` обнуляет;
- параллельные вызовы из нескольких потоков не приводят к исключению и не теряют состояние.

`InMemoryLogBuffer`
- не больше `Capacity` записей; при переполнении вытесняется самая старая;
- `GetRecent(n)` — последние `n` в хронологическом порядке; `GetRecent(0)` и отрицательное → пусто;
- `Add` с пустой строкой отклоняется.

`WeatherDigestService` (подставные `IWeatherProvider`, `IWeatherFormatter`, `ITelegramSender`,
`IBotStateManager`)
- успешный путь: погода получена → текст отформатирован → отправлено → `MarkSent` вызван;
- погода не получена → отправки нет, результат — ошибка провайдера;
- Telegram отклонил → результат — ошибка отправки, `MarkSent` **не** вызван.

`SecretMasker`
- секрет встречается один и несколько раз → все вхождения заменены;
- пустой или `null` секрет → текст без изменений; `null`/пустой текст → пустая строка.

## 7. Приёмка

Каталог — `backend/WeatherBot`. Вывод команд приводить **дословно**.

**Шаг 1. Пробная сборка (пакеты ещё не восстановлены — падение ожидаемо):**

```bash
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false
```

**Шаг 2. Restore — выполняет исполнитель.**

```bash
dotnet restore WeatherBot.slnx -m:1 -p:NuGetAudit=false
```

Новые тестовые проекты требуют restore. Он работает **офлайн**: нужные пакеты уже в локальном
кэше NuGet (проверено 2026-09-21, exit 0). Если шаг 1 упал на отсутствующих
`project.assets.json` — это ожидаемо для новых проектов и лечится этим restore.
Флаг `-p:NuGetAudit=false` обязателен.

**Шаг 3. Сборка после restore и прогон тестов** — прямым запуском сборок, без `dotnet test`:

```bash
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false
dotnet tests/WeatherBot.UnitTests/bin/Debug/net10.0/WeatherBot.UnitTests.dll
dotnet tests/WeatherBot.ArchitectureTests/bin/Debug/net10.0/WeatherBot.ArchitectureTests.dll
```

**Критерии приёмки:**

- [ ] Сборка: `Ошибок: 0`, `Предупреждений: 0`.
- [ ] `WeatherBot.UnitTests.dll`: `сбой: 0`, `пропущено: 0`; число пройденных приведено.
- [ ] `WeatherBot.ArchitectureTests.dll`: `сбой: 0`; число пройденных приведено.
- [ ] Если MTP вернёт **код выхода 8** («тестов не обнаружено») — привести это в отчёте
      и разобраться, а не считать молчаливым успехом.
- [ ] Оба проекта присутствуют в `WeatherBot.slnx`.
- [ ] Продакшн-код не изменён — `git status --short -- src` пуст (проверять именно `src`,
      а не весь репозиторий: в дереве есть несвязанные изменения документации).
- [ ] Документация: править не требуется.

## 8. Условие остановки

**Единственное условие остановки:** `dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false`
завершается с `Ошибок: 0` и `Предупреждений: 0`, **и** обе тестовые сборки прогнаны прямым
вызовом с `сбой: 0`.

- Достигнуто → остановиться, написать отчёт, **объём не расширять**.
- Условие недостижимо по внешней причине (нужен пакет, которого нет в локальном кэше, —
  тогда restore потребует сети) → остановиться **раньше** и сообщить об этом прямо.
- Обнаружено «заодно» → в раздел «Отклонения и замечено» отчёта, но **не в код**.

## 9. Что НЕ трогать

- **`backend/WeatherBot/src/**` — ни одного изменения.**
- `ARCHITECTURE.md`, `WORKFLOW.md`, `.dsh/AGENTS.md`, `README.md`, `memory-bank/**`,
  `ai-tasks/**` (кроме своего отчёта).
- `backend/Directory.Build.props`, `backend/.globalconfig`.
- Версии пакетов, кроме перечисленных в §2.
- `appsettings*.json`, `Properties/launchSettings.json`.

## 10. Лимит попыток и честный отчёт

- Не более **3 попыток** на один блокер. Дальше — стоп и отчёт.
- Restore выполняется один раз: `dotnet restore WeatherBot.slnx -m:1 -p:NuGetAudit=false`.
  Если он падает на пакете, которого нет в кэше, — сообщить, а не перебирать источники.
- Не получилось — написать прямо: что не вышло, сколько попыток, что пробовал, какие ошибки.

## 11. Порядок сдачи

1. Выполнить приёмку из §7 и сохранить дословный вывод команд.
2. Написать отчёт `ai-tasks/01-weather-provider/tests-infrastructure_report_v1.md`
   по шаблону `ai-tasks/_templates/report.md`.
3. Приложить список изменённых файлов и результаты прогонов.
