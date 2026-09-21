# 01-03-tests-mapper-and-formatter

**Требует изменения архитектуры:** нет — раскладка тестовых проектов уже описана в `ARCHITECTURE.md`.
**Статус:** согласована
**Задача:** 01-weather-provider
**Зависит от:** `01-01-remove-yandex` — принята
**Слой / проект:** `tests`

---

## 1. Цель

Тестовый контур собирается и прогоняется зелёным, и в нём есть первые настоящие тесты:
маппер Open-Meteo (все границы из контракта) и форматтер сообщения.

Задача — **мелкая и узкая** намеренно: предыдущая попытка (задача `01-02`) провалилась, потому что
объём был слишком велик для одного прогона исполнителя. Здесь два файла тестов, и после **каждого**
файла обязательна сборка.

## 2. Контекст

**Обязательно прочитать до начала работы** (в этом порядке, не по памяти):

| # | Файл | Зачем |
|---|------|-------|
| 1 | `memory-bank/snapshot.md` | инвентарь и реестр находок |
| 2 | `memory-bank/activeContext.md` | где мы сейчас |
| 3 | `ARCHITECTURE.md` → «Тестирование», «Интеграция с Open-Meteo», «Формат сообщения» | контракты и раскладка |
| 4 | `WORKFLOW.md` §2 | команды и ограничения среды |
| 5 | `.dsh/AGENTS.md` | правила проекта |

### Что уже сделано и что сломано

Инфраструктура тестовых проектов **уже создана и рабочая** — её трогать не нужно:

- `backend/Directory.Packages.props` — версии `xunit.v3` 4.0.0, `AwesomeAssertions` 9.6.0,
  `NSubstitute` 6.2.0, `TngTech.ArchUnitNET(.xUnitV3)` 0.13.4;
- `backend/WeatherBot/tests/Directory.Build.props` — наследование от `backend/`, `IsTestProject`,
  послабления анализаторов;
- оба тестовых проекта и их регистрация в `WeatherBot.slnx`;
- `InternalsVisibleTo("WeatherBot.UnitTests")` в `WeatherBot.Infrastructure.csproj` — он нужен,
  потому что маппер `internal`.

**Все девять файлов тестов невалидны и удаляются.** Сборка сейчас падает с 732 ошибками.
Причина разобрана ниже — прочитай её внимательно, это главное в этой постановке.

### ⚠️ Правда об атрибутах xUnit v3

Предыдущий исполнитель **выдумал** синтаксис и разрушил файлы, пытаясь под него подстроиться.
Это ложь, и повторять её нельзя:

| Ложное утверждение | Как на самом деле |
|---|---|
| `[fact]` в нижнем регистре | **`[Fact]`** — ровно как в xUnit v2 |
| `[theory]` в нижнем регистре | **`[Theory]`** — ровно как в xUnit v2 |
| `[case(1, 2.5)]` вместо `[InlineData]` | **`[InlineData(1, 2.5)]`** — `[case]` не существует |
| `using Xunit.v3;` для атрибутов | **`using Xunit;`** — атрибуты живут в пространстве имён `Xunit` |

**Атрибуты xUnit v3 не менялись относительно v2.** Изменились другие вещи (внутренние API,
`TestContext`, часть сигнатур `Assert`), но не имена атрибутов.

Дополнительно: `Should()` предоставляет **AwesomeAssertions**, для него нужен
**`using AwesomeAssertions;`** — без него расширение не найдётся.

> **Если сборка ругается — читай текст ошибки и исправляй по нему.** Не додумывай API
> по памяти и не «угадывай» имена атрибутов. Сомневаешься — открой официальную документацию
> xUnit v3 или посмотри, как это сделано в `projects/marketsniper-mvp/backend/tests/`.

### 🚫 Как работать с файлами: два запрета

Оба пункта — из разбора провала предыдущего прогона, не пожелания.

1. **Запрещены массовые замены скриптами.** Никаких PowerShell/regex-проходов по исходникам:
   именно так прошлый прогон залил в файлы литеральный `\r\n` и превратил их в мусор.
   Файлы правятся **поштучно** штатными инструментами (`write` / `edit`).
2. **Сборка после каждого файла.** Порядок такой: удалил старое → собрал → написал файл →
   **собрал** → написал второй файл → **собрал** → прогнал тесты. Не «напишу всё, потом проверю»:
   именно так прошлый прогон обнаружил проблему, когда контекст уже кончился.

### Ограничения среды исполнителя

`-m:1` обязателен и для `restore`, и для `build` — без него команда завершается кодом 1, не
напечатав ни строки, а сводка врёт: `Ошибок: 0`. `-p:NuGetAudit=false` обязателен.
`dotnet test` на MTP падает на именованном канале — тесты запускаются **прямым вызовом
собранной сборки**. Restore доступен исполнителю офлайн из локального кэша.

## 3. Файлы

**Удалить (все девять, без исключений):**

- `backend/WeatherBot/tests/WeatherBot.UnitTests/OpenMeteoWeatherMapperTests.cs`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/OpenMeteoWeatherProviderTests.cs`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/WeatherMessageFormatterTests.cs`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/BotStateManagerTests.cs`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/InMemoryLogBufferTests.cs`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/WeatherDigestServiceTests.cs`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/SecretMaskerTests.cs`
- `backend/WeatherBot/tests/WeatherBot.ArchitectureTests/LayerDependenciesTests.cs`
- `backend/WeatherBot/tests/WeatherBot.ArchitectureTests/DiCompositionTests.cs`

**Создать:**

- `backend/WeatherBot/global.json` — ровно такое содержимое:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

  Зачем: на .NET 10 `dotnet test` работает только в нативном режиме MTP, который выбирается
  этой секцией. Без неё `dotnet test` отказывается работать («Testing with VSTest target is no
  longer supported … on .NET 10 SDK and later»). Сборку и прямой прогон тестов это не задевает.

- `tests/WeatherBot.UnitTests/OpenMeteoWeatherMapperTests.cs`
- `tests/WeatherBot.UnitTests/WeatherMessageFormatterTests.cs`

**Не трогать:** оба `*.csproj`, `tests/Directory.Build.props`, `backend/Directory.Packages.props`,
`backend/Directory.Build.props`, `backend/.globalconfig`, `backend/WeatherBot.slnx`,
всё в `src/**`, все `.md`-артефакты и `ai-tasks/**` (кроме своего отчёта).

## 4. Шаги

1. Удалить девять перечисленных файлов.
2. Создать `backend/WeatherBot/global.json`.
3. **Собрать** и убедиться: `Ошибок: 0`, `Предупреждений: 0`. Проекты без тестовых файлов
   собираются нормально.
4. Написать `OpenMeteoWeatherMapperTests.cs`. **Собрать сразу.** Должно быть `Ошибок: 0`.
5. Написать `WeatherMessageFormatterTests.cs`. **Собрать сразу.** Должно быть `Ошибок: 0`.
6. Прогнать приёмку из §7.

## 5. Контракты и форматы данных

### Тестируемое

- `OpenMeteoWeatherMapper` — `internal static` в `WeatherBot.Infrastructure.Weather`.
  Доступен тестам через `InternalsVisibleTo`; **менять его видимость не нужно**.
- `WeatherMessageFormatter` — конструктор принимает `IOptionsMonitor<WeatherSettings>`.
  В тесте подменить заглушкой (NSubstitute подключён): нужен `CurrentValue` с заданным `City`.

### Что обязательно покрыть — маппер

Таблицы и пороги — из постановки `01-01` §5 и `ARCHITECTURE.md`. Покрыть **все границы**:

- **давление** `ToPressureMmHg`: обычное значение; округление половины **от нуля**; низкое и высокое;
- **ветер** `ToWindDirection`: все 8 румбов; обе границы сектора (`337.5`/`22.5` — проверить
  значения `23` и `337`); вне диапазона `[0, 360)` → `Unknown`;
- **облачность** `ToCloudiness`: пороги `20`, `60`, `85` — по обе стороны каждого;
- **осадки** `ToPrecipitationType(code, precipitation, rain, snowfall)`: по одному коду из каждой
  группы WMO; приоритет `Mixed` при `rain > 0 && snowfall > 0`; правило нулевых осадков
  (`precipitation == 0 && rain == 0 && snowfall == 0` → `None`, **даже если код осадочный**);
  неизвестный код → `Unknown`;
- **интенсивность** `ToPrecipitationStrength`: границы `0.5`, `2.5`, `7.5` — по обе стороны;
- **время** `TryParseObservedAt`: корректное время со смещением (смещение проставлено верно);
  `null` и пустая строка → `false`.

### Что обязательно покрыть — форматтер

- город подставляется из настроек — проверить на **двух разных городах**;
- в тексте **нет** литерала «МСК»; смещение выводится как `UTC+N` по смещению `ObservedAt`;
- `Unknown`-значения → «Нет данных» / «—», отправка не срывается;
- осадки `None` и `Unknown` — без скобки с интенсивностью.

### Форма теста

Параметризацию делать через `[Theory]` + `[InlineData(...)]`. Один сценарий — один тест-метод.
Имена методов — с подчёркиваниями, правило `CA1707` для тестов отключено в
`tests/Directory.Build.props`.

## 6. Тесты

| Уровень | Проект | Что покрыть |
|---------|--------|-------------|
| unit | `WeatherBot.UnitTests` | `OpenMeteoWeatherMapper`, `WeatherMessageFormatter` |
| architecture | `WeatherBot.ArchitectureTests` | **в этой задаче файлов нет** — проекты пустые, тесты появятся в `01-04` и `01-05` |

Сетевые вызовы запрещены — здесь их и не требуется, оба класса без сети.

## 7. Приёмка

Каталог — `backend/WeatherBot`. Вывод команд приводить **дословно**.

```bash
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false
dotnet tests/WeatherBot.UnitTests/bin/Debug/net10.0/WeatherBot.UnitTests.dll
```

**Критерии приёмки:**

- [ ] Сборка: `Ошибок: 0`, `Предупреждений: 0`.
- [ ] Девять перечисленных файлов удалены; `git status --short -- tests` не показывает их
      как изменённые.
- [ ] `backend/WeatherBot/global.json` создан с указанным содержимым.
- [ ] `WeatherBot.UnitTests.dll`: `Пройден!`, `сбой: 0`, `пропущено: 0`; число пройденных тестов
      приведено и обосновано (сколько сценариев описано).
- [ ] **Прогон `WeatherBot.ArchitectureTests.dll` вернёт код 8** («тестов не обнаружено») —
      это ожидаемо, файлов там пока нет. Указать в отчёте, не считать ошибкой.
- [ ] Секретов в коде и отчёте нет.
- [ ] В `src/**` изменений нет: `git status --short -- src` пуст.

## 8. Условие остановки

**Единственное условие остановки:** сборка — `Ошибок: 0`, `Предупреждений: 0`, **и**
`WeatherBot.UnitTests.dll` прогнан прямым вызовом с `сбой: 0` и ненулевым числом тестов.

- Достигнуто → остановиться, написать отчёт, **объём не расширять**.
- **Контекст подходит к концу или задача «не идёт»** → остановиться **сразу**, зафиксировать
  в отчёте: что сделано, где встал, какая команда что вывела. Не дожимать, не переписывать
  вслепую, не пробовать обходные пути наугад. Неполный честный отчёт лучше испорченных файлов.
- Обнаружено «заодно» (баг в продакшн-коде, лишний пакет, опечатка) → в раздел
  «Отклонения и замечено» отчёта, но **не в код**.

## 9. Что НЕ трогать

- Всё в `backend/WeatherBot/src/**`.
- Оба `*.csproj`, `tests/Directory.Build.props`, `Directory.Packages.props`, `WeatherBot.slnx`.
- `ARCHITECTURE.md`, `WORKFLOW.md`, `.dsh/AGENTS.md`, `README.md`, `memory-bank/**`,
  `ai-tasks/**` (кроме своего отчёта).

## 10. Лимит попыток и честный отчёт

- Не более **3 попыток** на один блокер. Дальше — стоп и отчёт.
- **Массовые замены исходников скриптами запрещены** (§2). Нарушение обесценивает результат.
- Restore при необходимости — один раз: `dotnet restore WeatherBot.slnx -m:1 -p:NuGetAudit=false`.
- Не получилось — написать прямо: что не вышло, сколько попыток, что пробовал, какие ошибки.
  Молчаливое «готово» при неработающем результате — грубое нарушение.

## 11. Порядок сдачи

1. Выполнить приёмку из §7 и сохранить дословный вывод команд.
2. Написать отчёт `ai-tasks/01-weather-provider/tests-mapper-and-formatter_report_v1.md`
   по шаблону `ai-tasks/_templates/report.md` — обязательно с разделом «Использованные скиллы».
3. Приложить список созданных и удалённых файлов и число пройденных тестов.
