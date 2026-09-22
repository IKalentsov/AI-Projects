# Отчёт: тикет 05 — Архитектурные тесты — канарейка и точные формулировки правил

**Дата:** 2026-09-22
**Исполнитель:** Qwen3.6-MTP

## Что сделано

### 1. LayerDependenciesTests.cs — канарейка, негативная проверка, переформулировка правил

**Канарейка (`Canary_authorizedDependency_isVisible`)**: утверждает, что `WeatherBot.Application` реально видит `WeatherBot.Domain` в загруженных сборках. Если `Assembly.Load` упадёт или `GetReferencedAssemblies` вернёт пустой набор — тест немедленно падает, а не пропускает правила молча.

**Негативная проверка механики (`AssertNoDependencyOn_throwsWhenForbiddenDependencyPresent`)**: подсунул словарь с заведомо запрещённой парой `FakeLayer → WeatherBot.Domain` и вызвал приватный метод `AssertNoDependencyOn` через Reflection (поскольку метод `private static`, прямое обращение из теста невозможно). Проверяю, что бросается `Xunit.Sdk.XunitException` с корректным сообщением.

**Переформулировка AT-03 и AT-05**:
- AT-03: «Contracts не **использует** типы Domain» — комментарий явно указывает, что объявленная в `.csproj` ссылка на Domain выбрасывается компилятором при отсутствии использования; тест проверяет именно usage через AssemblyRef метаданных
- AT-05: «Web не **использует напрямую** типы Domain» — аналогично, с упоминанием находки F-16 (неиспользуемая ссылка Web → Domain)

**Документация класса**: обновлён doc-комментарий — описано, что измеряется (AssemblyRef загруженных сборок), а не декларация `.csproj`.

### 2. BotStateManagerTests.cs — переименование конкурентных тестов

- `Concurrent_StartStop_noExceptionsAndNoStateLoss` → `Concurrent_StartStop_noExceptions`: имя точно отражает проверяемое (отсутствие исключений при параллельных Start/Stop)
- `Concurrent_MarkSentAndMarkNextTick_preservesState` → `Concurrent_MarkSentAndMarkNextTick_noExceptionsAndStableState`: добавлена проверка итогового состояния — `LastSentAt.Should().NotBeNull()` после гонки

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
  длительность: 749ms
```

### Архитектурные тесты

```
dotnet tests/WeatherBot.ArchitectureTests/bin/Debug/net10.0/WeatherBot.ArchitectureTests.dll

Сводка тестового запуска: Пройден! - .../WeatherBot.ArchitectureTests.dll (net10.0|x64)
  всего: 13
  сбой: 0
  успешно выполнено: 13
  пропущено: 0
  длительность: 278ms
```

## Проверка критериев приёмки

| Критерий | Статус |
|----------|--------|
| Канарейка: разрешённая зависимость видна | ✅ `Canary_authorizedDependency_isVisible` — проверяет AssemblyRef Application→Domain |
| Негативная проверка механики | ✅ через Reflection вызывает `AssertNoDependencyOn` с запрещённой парой, ожидает `XunitException` |
| AT-03: формулировка про **использование** типов | ✅ «Contracts не использует типы Domain» + комментарий про F-10 |
| AT-05: формулировка про **использование** типов | ✅ «Web не использует напрямую типы Domain» + комментарий про F-16 |
| Doc-комментарий класса описывает измеряемое | ✅ AssemblyRef загруженных сборок, а не `.csproj` |
| BotStateManagerTests переименованы + проверка состояния | ✅ `LastSentAt.Should().NotBeNull()` после гонки |
| ArchitectureTests ≥ 13 | ✅ (13 = 5 правил + канарейка + негативная + 6 из тикета 04) |
| UnitTests ≥ 116 | ✅ (122) |
| Сборка: Ошибок: 0, Предупреждений: 0 | ✅ |
| `src/**`, `*.csproj`, `Directory.Packages.props` не изменены | ✅ |

## Что доказывает канарейка

Тест `Canary_authorizedDependency_isVisible` загружает все пять сборок решения через `Assembly.Load()` и проверяет, что в метаданных `WeatherBot.Application.dll` присутствует AssemblyRef на `WeatherBot.Domain`. Это доказывает три факта:
1. Сборки действительно существуют и загрузились (не пропущены молча)
2. `GetReferencedAssemblies()` возвращает непустой набор
3. Сравнение имён сборок работает корректно

Если любой из этих шагов сломается в другой среде — канарейка упадёт первой, до любых других правил.

## Отклонения

Нет. Все замечания ревью закрыты без отклонений.

## Что не проверялось

- Прогон на чистой сборке (без кэша) — среда агента работает офлайн
- Тесты, добавленные в тикете 04 (DiCompositionTests, TimeoutHandler), перепроверены архитектурным прогоном: все 13 тестов проходят
