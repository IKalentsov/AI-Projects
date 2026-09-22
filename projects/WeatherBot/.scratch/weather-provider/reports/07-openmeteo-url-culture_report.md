# Отчёт: тикет 07 — Координаты уходят в Open-Meteo в культуре процесса

**Дата:** 2026-09-22
**Исполнитель:** **роль разработчика исполнена архитектором в этой же сессии** (пользователь:
«делай сам»). Штатный цикл предполагает исполнителя Qwen и отдельного ревьюера; здесь обе роли
слиты. Это отклонение от процесса, оно зафиксировано в `issues/07-…` и в ревью нет — независимого
ревьюера у этой правки не было.

## Что сделано

### 1. Тест — сначала красный

Файл `backend/WeatherBot/tests/WeatherBot.UnitTests/OpenMeteoWeatherProviderTests.cs`:
добавлен `GetCurrentAsync_requestCoordinates_areCultureInvariant`. Тест ставит
`CultureInfo.CurrentCulture = new CultureInfo("ru-RU")`, вызывает провайдера через существующую
заглушку `HttpMessageHandler` и требует точных `latitude=55.7558` и `longitude=37.6173` в
перехваченном `RequestUri`; культура восстанавливается в `finally`.

Прогон **до** правки — тест падает и показывает настоящий URL:

```
сбой WeatherBot.UnitTests.OpenMeteoWeatherProviderTests.GetCurrentAsync_requestCoordinates_areCultureInvariant (40ms)
Expected url "https://api.open-meteo.com/v1/forecast?latitude=55,7558&longitude=37,6173&current=temperature_2m,...,wind_gusts_10m&wind_speed_unit=ms&timezone=auto" to contain "latitude=55.7558".
  всего: 123
  сбой: 1
  успешно выполнено: 122
```

### 2. Правка — одна строка логики

Файл `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`:
добавлен `using System.Globalization;`, координаты в `BuildUrl` форматируются инвариантно.

```diff
-        $"?latitude={settings.Latitude}" +
-        $"&longitude={settings.Longitude}" +
+        $"?latitude={settings.Latitude.ToString(CultureInfo.InvariantCulture)}" +
+        $"&longitude={settings.Longitude.ToString(CultureInfo.InvariantCulture)}" +
```

Остальной код аудирован на ту же ошибку: `WeatherMessageFormatter` уже форматирует числа через
`CultureInfo.InvariantCulture`, `System.Text.Json` культуру не использует. Дефект был один.

### 3. Прогон после правки

```
Сводка тестового запуска: Пройден! - WeatherBot.UnitTests.dll (net10.0|x64)
  всего: 123
  сбой: 0
  успешно выполнено: 123
  длительность: 756ms

Сводка тестового запуска: Пройден! - WeatherBot.ArchitectureTests.dll (net10.0|x64)
  всего: 13
  сбой: 0
  успешно выполнено: 13
```

### 4. Полная сборка решения — обходом блокировки

Debug-сборка `WeatherBot.Web` не может заменить `WeatherBot.Infrastructure.dll`, пока приложение
запущено, поэтому чистота сборки проверена в Release (у него отдельный каталог вывода):

```
=== Release: полная сборка решения ===
Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0

=== Release: UnitTests ===      всего: 123, сбой: 0, успешно выполнено: 123
=== Release: ArchitectureTests ===   всего: 13, сбой: 0, успешно выполнено: 13
```

## Чего не удалось проверить и почему

**Debug-сборка `Web`.** Сборка `WeatherBot.Web` не может заменить
`WeatherBot.Infrastructure.dll` в своём `bin`, потому что файл держит запущенное приложение:

```
error MSB3027: Превышено допустимое число повторных попыток (10). "WeatherBot.Web (36948)"
блокирует этот файл [src\WeatherBot.Web\WeatherBot.Web.csproj]
```

Это блокировка файла, а не ошибка кода: 10 «предупреждений» в том прогоне — те же повторы копии
(`MSB3026`). Тестовые проекты собрались и прошли, а чистота сборки подтверждена Release-прогоном
выше. Для Debug-прогона нужно остановить приложение — работа пользователя.

**Живой прогон `send-now`** — тоже за пользователем: приложение, которое сейчас запущено, работает
на старом коде, правка вступит в силу после перезапуска.

## Изменённые файлы

- `backend/WeatherBot/src/WeatherBot.Infrastructure/Weather/OpenMeteoWeatherProvider.cs`
- `backend/WeatherBot/tests/WeatherBot.UnitTests/OpenMeteoWeatherProviderTests.cs`
