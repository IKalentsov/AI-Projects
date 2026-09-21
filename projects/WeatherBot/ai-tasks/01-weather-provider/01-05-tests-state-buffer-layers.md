# 01-05-tests-state-buffer-layers

**Требует изменения архитектуры:** нет.
**Статус:** согласована
**Задача:** 01-weather-provider
**Зависит от:** `01-04-tests-provider-digest-di` — контур должен быть зелёным
**Слой / проект:** `tests`

---

## 1. Цель

Закрыть оставшиеся тесты: состояние бота, кольцевой буфер журнала, маскировка секретов
и архитектурный тест направления зависимостей между слоями.

Четыре файла, но три из них маленькие. Сборка после **каждого** файла.

## 2. Контекст

**Обязательно прочитать** (в этом порядке): `memory-bank/snapshot.md`,
`memory-bank/activeContext.md`, `ARCHITECTURE.md` («Тестирование», «Направления зависимостей»),
`WORKFLOW.md` §2, `.dsh/AGENTS.md`.

- Контур создан в `01-03`, расширен в `01-04`. Прогон тестов — **прямым вызовом сборки**.

### ⚠️ Атрибуты xUnit v3 — не выдумывать

`[Fact]`, `[Theory]`, `[InlineData(...)]`, пространство имён **`Xunit`**; для `Should()` нужен
**`using AwesomeAssertions;`**. Названия атрибутов в v3 **не менялись** относительно v2.
Если сборка ругается — читай текст ошибки, не угадывай.

### 🔎 Архитектурный тест: брать API из README пакета, а не из памяти

Предыдущая попытка провалилась здесь с `CS0246: ArchUnitDomain / TypeCategory` — типы
использовались без нужных `using`. Рабочий пример лежит **внутри установленного пакета**:

- `~/.nuget/packages/tngtech.archunitnet.xunitv3/0.13.4/README.md`
- `~/.nuget/packages/tngtech.archunitnet/0.13.4/README.md`

**Прочитай README перед написанием теста.** Из него — точные пространства имён:

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Xunit;
```

Документация: https://archunitnet.readthedocs.io/en/latest/

### 🚫 Два запрета из разбора прошлого провала

1. **Никаких массовых замен исходников скриптами** — только `write` / `edit` поштучно.
2. **Сборка после каждого файла.**

### Официальные скиллы — загрузить перед началом

**Загружать через `open_skill` по точному имени** (в каталоге сессии их описаний нет):

| Скилл | Зачем в этой задаче |
|-------|---------------------|
| `run-tests` | синтаксис `dotnet test` и фильтры; прогон без `dotnet test` |
| `directory-build-organization` | иерархия `Directory.Build.props`, центральные версии пакетов |

В отчёте (§8 шаблона) указать, какие загружены и что применено. «Не использовались» — тоже
допустимый ответ, но он должен быть написан явно.

### Ограничения среды

`-m:1` обязателен и для `restore`, и для `build`; `-p:NuGetAudit=false` обязателен;
`dotnet test` на MTP падает на именованном канале — тесты гоняются прямым вызовом сборки.

## 3. Файлы

**Создать:**

- `tests/WeatherBot.UnitTests/BotStateManagerTests.cs`
- `tests/WeatherBot.UnitTests/InMemoryLogBufferTests.cs`
- `tests/WeatherBot.UnitTests/SecretMaskerTests.cs`
- `tests/WeatherBot.ArchitectureTests/LayerDependenciesTests.cs`

**Не трогать:** `*.csproj`, `tests/Directory.Build.props`, `Directory.Packages.props`,
`WeatherBot.slnx`, `global.json`, всё в `src/**`, тесты из `01-03` и `01-04`,
все `.md`-артефакты и `ai-tasks/**` (кроме своего отчёта).

## 4. Шаги

1. `BotStateManagerTests.cs` → **сборка**.
2. `InMemoryLogBufferTests.cs` → **сборка**.
3. `SecretMaskerTests.cs` → **сборка**.
4. `LayerDependenciesTests.cs` → **сборка**.
5. Прогнать приёмку из §7.

## 5. Контракты и форматы данных

### `BotStateManagerTests` — `BotStateManager`, Singleton, потокобезопасный

- стартовое состояние: не запущен, `LastSentAt` и `NextTickAt` пусты;
- `Start()` / `Stop()` меняют `IsRunning`; `Stop()` обнуляет `NextTickAt`;
- `MarkSent` / `MarkNextTick` проставляют значения; `MarkNextTick(null)` обнуляет;
- параллельные вызовы из нескольких потоков не приводят к исключению и не теряют состояние.

### `InMemoryLogBufferTests` — `InMemoryLogBuffer`, ёмкость `Capacity` = 100

- хранится не больше `Capacity` записей; при переполнении вытесняется самая старая;
- `GetRecent(n)` — последние `n` в хронологическом порядке;
- `GetRecent(0)` и отрицательное значение → пустой список;
- `Add` с пустой строкой отклоняется.

### `SecretMaskerTests` — `SecretMasker`, `internal static`

- секрет встречается один раз и несколько раз → все вхождения заменены;
- пустой или `null` секрет → текст без изменений;
- `null` или пустой текст → пустая строка.

### `LayerDependenciesTests` — направление зависимостей

Проверяется **фактическое** направление зависимостей между пятью проектами `src`:

- `WeatherBot.Domain` не зависит ни от одного другого проекта решения;
- `WeatherBot.Application` зависит только от `WeatherBot.Domain`;
- `WeatherBot.Contracts` не зависит ни от одного проекта решения;
- `WeatherBot.Infrastructure` не зависит от `WeatherBot.Web`;
- `WeatherBot.Web` — единственный, кто видит и `Infrastructure`, и `Contracts`.

Реализация — на `TngTech.ArchUnitNET` (пакеты уже подключены), по примеру из README.
Собирать архитектуру один раз в статическом поле (`ArchLoader().LoadAssemblies(...).Build()`).

**Допустимый запасной вариант:** если флюентного правила для нужной проверки не находится,
разрешено проверить те же правила через `Assembly.GetReferencedAssemblies()` по загруженным
сборкам — это тот же контракт. Если выбран запасной вариант, **сказать об этом в отчёте**;
пакеты ArchUnitNET при этом не удалять и `*.csproj` не трогать.

**Проверка обязана падать при нарушении** — иначе она бесполезна.

## 6. Тесты

| Уровень | Проект | Что покрыть |
|---------|--------|-------------|
| unit | `WeatherBot.UnitTests` | `BotStateManager`, `InMemoryLogBuffer`, `SecretMasker` |
| architecture | `WeatherBot.ArchitectureTests` | направление зависимостей между слоями |

Все ранее написанные тесты обязаны остаться зелёными.

## 7. Приёмка

Каталог — `backend/WeatherBot`. Вывод команд приводить **дословно**.

```bash
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false
dotnet tests/WeatherBot.UnitTests/bin/Debug/net10.0/WeatherBot.UnitTests.dll
dotnet tests/WeatherBot.ArchitectureTests/bin/Debug/net10.0/WeatherBot.ArchitectureTests.dll
```

**Критерии приёмки:**

- [ ] Сборка: `Ошибок: 0`, `Предупреждений: 0`.
- [ ] Обе сборки: `Пройден!`, `сбой: 0`; число пройденных приведено и включает тесты из `01-03`
      и `01-04`.
- [ ] Архитектурный тест действительно проверяет правила из §5, а не является пустой заглушкой:
      объяснить в отчёте, какое правило каким тестом проверяется.
- [ ] Продакшн-код этой задачи не менялся. Ориентир: `git status --short -- src` — допустима
      **только** строка `WeatherBot.Infrastructure.csproj` (`InternalsVisibleTo` добавлен
      в `01-01`, это ожидаемо); любая другая строка — нарушение.

## 8. Условие остановки

**Единственное условие остановки:** сборка — `Ошибок: 0`, `Предупреждений: 0`, **и** обе тестовые
сборки прогнаны прямым вызовом с `сбой: 0` и ненулевым числом тестов.

- Достигнуто → остановиться, написать отчёт, **объём не расширять**.
- **Контекст подходит к концу или задача «не идёт»** → остановиться **сразу** и зафиксировать
  в отчёте: что сделано, где встал, какая команда что вывела. Не дожимать, не переписывать вслепую.
- Обнаружено «заодно» → в раздел «Отклонения и замечено» отчёта, но **не в код**.

## 9. Что НЕ трогать

- Всё в `backend/WeatherBot/src/**`.
- `*.csproj`, `tests/Directory.Build.props`, `Directory.Packages.props`, `WeatherBot.slnx`,
  `global.json`.
- Тесты, созданные `01-03` и `01-04`.
- `ARCHITECTURE.md`, `WORKFLOW.md`, `.dsh/AGENTS.md`, `README.md`, `memory-bank/**`,
  `ai-tasks/**` (кроме своего отчёта).

## 10. Лимит попыток и честный отчёт

- Не более **3 попыток** на один блокер. Дальше — стоп и отчёт.
- **Массовые замены исходников скриптами запрещены.**
- Не получилось — написать прямо: что не вышло, сколько попыток, что пробовал, какие ошибки.

## 11. Порядок сдачи

1. Выполнить приёмку из §7, сохранить дословный вывод.
2. Написать отчёт `ai-tasks/01-weather-provider/tests-state-buffer-layers_report_v1.md`
   по шаблону `ai-tasks/_templates/report.md` — обязательно с разделом «Использованные скиллы».
3. Приложить список созданных файлов и число пройденных тестов по каждому проекту.
