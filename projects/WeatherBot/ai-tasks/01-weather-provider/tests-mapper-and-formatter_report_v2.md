# Отчёт: 01-03-tests-mapper-and-formatter (v2)

**Исходное ревью:** `tests-mapper-and-formatter.review.md`
**Правки:** `tests-mapper-and-formatter.fixes.md`
**Дата:** 2026-09-22
**Предыдущий отчёт:** `tests-mapper-and-formatter_report_v1.md` (не перезаписан)

---

## Что исправлено по fixes

### Правка 1. Убрать лишнюю зависимость и вернуть обычные атрибуты

**Сделано:**

1. **`backend/Directory.Packages.props`** — удалена строка:
   ```xml
   <PackageVersion Include="xunit.v3.extensibility.core" Version="4.0.0" />
   ```
2. **`backend/WeatherBot/tests/WeatherBot.UnitTests/WeatherBot.UnitTests.csproj`** — удалена строка:
   ```xml
   <PackageReference Include="xunit.v3.extensibility.core" />
   ```
3. **`backend/WeatherBot/tests/WeatherBot.UnitTests/OpenMeteoWeatherMapperTests.cs`**:
   - Добавлен `using Xunit;`
   - Все `[Xunit.Fact]` заменены на `[Fact]` (9 штук)
   - Все `[Xunit.Theory]` заменены на `[Theory]` (7 штук)
   - Все `[Xunit.InlineData(...)]` заменены на `[InlineData(...)]` (~40 штук)
4. **`backend/WeatherBot/tests/WeatherBot.UnitTests/WeatherMessageFormatterTests.cs`**:
   - Добавлен `using Xunit;`
   - Все `[Xunit.Fact]` заменены на `[Fact]` (12 штук)

**Результат сборки:**
```
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

**Результат тестов:**
```
Сводка тестового запуска: Пройден! - WeatherBot.UnitTests.dll (net10.0|x64)
  всего: 68
  сбой: 0
  успешно выполнено: 68
  пропущено: 0
  длительность: 556ms
```

**Вывод:** Сборка прошла без ошибок и предупреждений, все 68 тестов прошли. Зависимость `xunit.v3.extensibility.core` действительно была лишней — `using Xunit;` с обычными `[Fact]`/`[Theory]`/`[InlineData]` корректно работает с пакетом `xunit.v3` 4.0.0 на .NET 10.

---

### Правка 2. Исправить бессмысленный второй аргумент у assertion

**Сделано:**

В файле **`backend/WeatherBot/tests/WeatherBot.UnitTests/WeatherMessageFormatterTests.cs`** удалён второй аргумент `"OrdinalIgnoreCase"` из всех вызовов `Should().Contain(...)` и `Should().NotContain(...)`:

| Строка (было) | Строка (стало) |
|---|---|
| `result.Should().Contain("*Погода в Москва*", "OrdinalIgnoreCase");` | `result.Should().Contain("*Погода в Москва*");` |
| `result.Should().Contain("*Погода в Санкт-Петербург*", "OrdinalIgnoreCase");` | `result.Should().Contain("*Погода в Санкт-Петербург*");` |
| `result.Should().Contain("(UTC+2)", "OrdinalIgnoreCase");` | `result.Should().Contain("(UTC+2)");` |
| `result.Should().Contain("(UTC-4)", "OrdinalIgnoreCase");` | `result.Should().Contain("(UTC-4)");` |
| `result.Should().Contain("Нет данных", "OrdinalIgnoreCase");` | `result.Should().Contain("Нет данных");` |
| `result.Should().Contain("Без осадков", "OrdinalIgnoreCase");` | `result.Should().Contain("Без осадков");` |
| `precipLine!.Should().Contain("Нет данных", "OrdinalIgnoreCase");` | `precipLine!.Should().Contain("Нет данных");` |
| `precipLine!.Should().Contain("Дождь (умеренные)", "OrdinalIgnoreCase");` | `precipLine!.Should().Contain("Дождь (умеренные)");` |
| `precipLine!.Should().Contain("Снег (сильные)", "OrdinalIgnoreCase");` | `precipLine!.Should().Contain("Снег (сильные)");` |

**Результат сборки:**
```
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0
```

**Результат тестов:**
```
Сводка тестового запуска: Пройден! - WeatherBot.UnitTests.dll (net10.0|x64)
  всего: 68
  сбой: 0
  успешно выполнено: 68
  пропущено: 0
  длительность: 551ms
```

**Вывод:** Сборка прошла без ошибок и предупреждений, все 68 тестов прошли. Второй аргумент `"OrdinalIgnoreCase"` в `AwesomeAssertions.Contain()` — это текст пояснения (`because`), а не режим сравнения. Удаление исправляет поведение: assertion теперь работает с регистрозависимым сравнением (как и задумано), а строка `"OrdinalIgnoreCase"` больше не попадает в сообщения об ошибках.

---

## Итог

Обе правки применены, проект компилируется, все 68 тестов проходят успешно:
- **Правка 1:** зависимость `xunit.v3.extensibility.core` удалена, атрибуты используют обычный `using Xunit;` — CS0246 не возникает.
- **Правка 2:** бессмысленный второй аргумент `"OrdinalIgnoreCase"` удалён из всех 9 вызовов `Contain`/`NotContain`.
