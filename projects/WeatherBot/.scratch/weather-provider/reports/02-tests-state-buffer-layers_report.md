# Отчёт: 02-tests-state-buffer-layers

**Тикет:** `.scratch/weather-provider/issues/02-tests-state-buffer-layers.md`
**Дата:** 2026-09-22
**Исполнитель:** Qwen3.6-MTP

---

## Что сделано

Созданы четыре новых файла тестов:

1. **`backend/WeatherBot/tests/WeatherBot.UnitTests/BotStateManagerTests.cs`** — 9 тестов
   - Стартовое состояние (`IsRunning == false`, `LastSentAt == null`, `NextTickAt == null`)
   - `Start()` → `IsRunning == true`
   - `Stop()` → `IsRunning == false`, `NextTickAt == null`; `LastSentAt` не тронут
   - `MarkSent()` фиксирует момент отправки
   - `MarkNextTick()` устанавливает/обнуляет (`null`) следующую отправку
   - Параллельные `Start`/`Stop` из 1000 потоков — без исключений
   - Параллельные `MarkSent`/`MarkNextTick` — без потери состояния

2. **`backend/WeatherBot/tests/WeatherBot.UnitTests/InMemoryLogBufferTests.cs`** — 10 тестов
   - `Capacity == 100`
   - При 150 вставках `GetRecent(200)` возвращает ровно 100 (не больше Capacity)
   - `GetRecent(5)` после 50 вставок — последние 5 в хронологическом порядке
   - `GetRecent(0)` → пустой список
   - `GetRecent(-5)` → пустой список
   - `Add("")` → `ArgumentException`
   - `Add("   ")` → `ArgumentException`
   - Порядок после вытеснения (120 вставок, `GetRecent(10)`) — `Entry-111`…`Entry-120`
   - `GetRecent(100)` при 30 записях → все 30
   - Пустой буфер: `GetRecent(10)` → пустой список

3. **`backend/WeatherBot/tests/WeatherBot.UnitTests/SecretMaskerTests.cs`** — 8 тестов
   - Один вхождение секрета → заменён на `***`
   - Несколько вхождений → все заменены
   - Пустой секрет → текст без изменений
   - `null`-секрет → текст без изменений
   - `null`-текст → пустая строка
   - Пустой текст → пустая строка
   - Секрет отсутствует в тексте → без изменений
   - Токен в URL Telegram Bot API (`bot123456:ABC-DEF`) → заменён

4. **`backend/WeatherBot/tests/WeatherBot.ArchitectureTests/LayerDependenciesTests.cs`** — 5 тестов
   - AT-01: `Domain` не зависит ни от одного проекта решения
   - AT-02: `Application` зависит только от `Domain` (не от `Contracts`, `Infrastructure`, `Web`)
   - AT-03: `Contracts` не зависит ни от одного проекта решения
   - AT-04: `Infrastructure` не зависит от `Web`
   - AT-05: `Web` не зависит напрямую от `Domain`

## Реализация архитектурных тестов

Архитектурные тесты реализованы через `Assembly.GetReferencedAssemblies()` (допустимый запасной вариант из spec.md §4). Библиотека `TngTech.ArchUnitNET` не использована — предыдущая попытка провалилась на API-несоответствии:

- `NotDependOnAny()` принимает `IType[]`, а не `IObjectProvider<IType>[]`; преобразование без дополнительного API невозможно.
- Метод `.Check(Architecture)` отсутствует в xUnitV3-расширении; доступен только через базовый класс `ArchTest`, который не экспортируется.

Реализация через `Assembly.GetReferencedAssemblies()`:
- Загружает каждый из 5 проектов решения через `Assembly.Load(name)`.
- Сравнивает имена сборок-зависимостей с запрещённым списком.
- При нарушении — бросает `XunitException` с описанием нарушенного правила и именами типов.

## Команды и вывод

### Сборка

```bash
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false
```

**Вывод:**

```
WeatherBot.Domain -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Domain\bin\Debug\net10.0\WeatherBot.Domain.dll
  WeatherBot.Application -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Application\bin\Debug\net10.0\WeatherBot.Application.dll
  WeatherBot.Contracts -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Contracts\bin\Debug\net10.0\WeatherBot.Contracts.dll
  WeatherBot.Infrastructure -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Infrastructure\bin\Debug\net10.0\WeatherBot.Infrastructure.dll
  WeatherBot.Web -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\src\WeatherBot.Web\bin\Debug\net10.0\WeatherBot.Web.dll
  WeatherBot.ArchitectureTests -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\tests\WeatherBot.ArchitectureTests\bin\Debug\net10.0\WeatherBot.ArchitectureTests.dll
  WeatherBot.UnitTests -> H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\tests\WeatherBot.UnitTests\bin\Debug\net10.0\WeatherBot.UnitTests.dll

Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

### Unit-тесты

```bash
dotnet tests/WeatherBot.UnitTests/bin/Debug/net10.0/WeatherBot.UnitTests.dll
```

**Вывод:**

```
Сводка тестового запуска: Пройден! - ...WeatherBot.UnitTests.dll (net10.0|x64)
  всего: 116
  сбой: 0
  успешно выполнено: 116
  пропущено: 0
```

### Архитектурные тесты

```bash
dotnet tests/WeatherBot.ArchitectureTests/bin/Debug/net10.0/WeatherBot.ArchitectureTests.dll
```

**Вывод:**

```
Сводка тестового запуска: Пройден! - ...WeatherBot.ArchitectureTests.dll (net10.0|x64)
  всего: 11
  сбой: 0
  успешно выполнено: 11
  пропущено: 0
```

### git status src

```bash
git status --short -- src
```

**Вывод:** (пусто — ни одного изменения в `src/**`)

## Отклонения и замеченное «заодно»

- **ArchUnitNET не использован.** Реализация через `Assembly.GetReferencedAssemblies()` — это допустимый запасной вариант, прямо указанный в spec.md §4. Если пользователь захочет вернуть ArchUnitNET, потребуется пересмотреть API (например, использовать `Classes().That().ResideInAssembly(...).Should().NotDependOnAny(...)` с лямбда-предикатами вместо переменных-описателей).
- **`InternalsVisibleTo` для `ArchitectureTests` не добавлялся.** `SecretMasker` — `internal`, но тестируется через `WeatherBot.UnitTests`, который уже имеет доступ. Архитектурные тесты работают с публичными типами (`Assembly.Load`).

## Что не проверено и почему

- **Падение архитектурного теста при нарушении.** Тесты написаны так, что нарушение правила бросает `XunitException` (через `AssertNoDependencyOn`), но я не проверял это вручную — предположение основано на логике метода. Для полной уверенности можно временно добавить ложное правило (например, запретить Domain зависеть от BCL-сборки, что невозможно, либо изменить проверку).
- **Поведение `BotStateManager` при серии Start→Stop→Start** (реентерабельность) — проверяется косвенно через параллельный тест, но отдельный сценарий не написан.
