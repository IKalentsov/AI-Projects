# Отчёт: тикет 06 — Приборка после ревью 04–05

**Дата:** 2026-09-22
**Исполнитель:** Qwen3.6-MTP

## Что сделано

### Файл: `backend/WeatherBot/tests/WeatherBot.ArchitectureTests/LayerDependenciesTests.cs`

1. **Негативная проверка:** тест `AssertNoDependencyOn_throwsWhenForbiddenDependencyPresent` переписан с Reflection-обхода (`GetMethod`, `Invoke`, `TargetInvocationException`, `InnerException`) на прямой вызов `Assert.Throws<Xunit.Sdk.XunitException>(() => AssertNoDependencyOn(fakeDeps, "FakeLayer", "WeatherBot.Domain"))`. Метод `AssertNoDependencyOn` — `private static` того же класса, прямое обращение возможно и компилируется.

2. **Канарейка:** тест `Canary_authorizedDependency_isVisible` дополнен второй проверкой — `Infrastructure → Application` (первая пара `Application → Domain` сохранена). Теперь канарейка проверяет обе пары разрешённых зависимостей, названных в тикете 05.

3. **Имя правила AT-01:** тест переименован из `AT01_Domain_dependsOnNoOtherProjectAssembly` в `AT01_Domain_usesNoOtherProjectAssembly` — семантика «использует» унифицирована с `AT-03` и `AT-05`.

4. **`using System.Reflection`:** оставлен, так как `AssemblyName` находится в этом пространстве имён. Убраны только вызовы Reflection (`GetMethod`, `Invoke`) и обработка `TargetInvocationException`.

## Команды и вывод

### Сборка

```
dotnet build WeatherBot.slnx -c Debug -m:1 --no-restore -p:NuGetAudit=false
```

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

### Архитектурные тесты

```
dotnet tests/WeatherBot.ArchitectureTests/bin/Debug/net10.0/WeatherBot.ArchitectureTests.dll
```

```
Сводка тестового запуска: Пройден! - H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\tests\WeatherBot.ArchitectureTests\bin\Debug\net10.0/WeatherBot.ArchitectureTests.dll (net10.0|x64)
  всего: 13
  сбой: 0
  успешно выполнено: 13
  пропущено: 0
```

### Unit-тесты

```
dotnet tests/WeatherBot.UnitTests/bin/Debug/net10.0/WeatherBot.UnitTests.dll
```

```
Сводка тестового запуска: Пройден! - H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\WeatherBot\backend\WeatherBot\tests\WeatherBot.UnitTests\bin\Debug\net10.0/WeatherBot.UnitTests.dll (net10.0|x64)
  всего: 122
  сбой: 0
  успешно выполнено: 122
  пропущено: 0
```

## Критерии приёмки

| # | Требование | Факт | Статус |
|---|-----------|------|--------|
| 1 | Негативная проверка вызывает `AssertNoDependencyOn` напрямую, без Reflection | `Assert.Throws<Xunit.Sdk.XunitException>(() => AssertNoDependencyOn(...))` ✅ | ✅ |
| 2 | Канарейка: обе пары (Application→Domain, Infrastructure→Application) | обе проверки в одном тесте ✅ | ✅ |
| 3 | `AT01` переименован по семантике «использует» | `AT01_Domain_usesNoOtherProjectAssembly` ✅ | ✅ |
| 4 | Сборка: Ошибок 0, Предупреждений 0 | подтверждено ✅ | ✅ |
| 5 | UnitTests — 122, ArchitectureTests — ≥ 13 | 122 и 13 ✅ | ✅ |
| 6 | `src/**`, `*.csproj`, `Directory.Packages.props` не изменены | не тронуты ✅ | ✅ |

## Отклонения и замечания

Нет. Все три пункта (обязательный + два необязательных) выполнены.
