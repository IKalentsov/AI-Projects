# QwenAnswers — запуск тестов

## Что где лежит

```
QwenAnswers/
└── QwenAnswers.Tests/
    └── QwenAnswers.Tests/
        ├── Unit/          ← чистая логика: конфиг, валидация, история, сессия, консольный цикл
        ├── Integration/   ← реальное окружение: ваш .env, секреты, запросы к модели
        ├── Architecture/  ← договорённости репозитория: шаблон, .gitignore, README, сборка
        ├── Support/       ← вспомогательные классы для тестов
        └── QwenAnswers.Tests.csproj
```

Рабочий каталог для всех команд ниже:

```powershell
cd H:\CSharp\MeProjects\AIProjects\AI-Projects\projects\QwenAnswers\QwenAnswers.Tests
```

## Категории тестов

| Категория | Фильтр | Нужен `.env` | Нужна запущенная модель |
|---|---|---|---|
| Обычные тесты | без фильтра | нет | нет |
| Архитектурные | `Category=Architecture` | нет | нет |
| Готовность настройки | `Category=Readiness` | да | нет |
| Живые | `Category=Live` | да | да + `AI_RUN_LIVE_TESTS=1` |

`Category=Readiness` падает, если `.env` отсутствует или заполнен неверно. Это не поломка: тесты проверяют настройку, для которой они и написаны. В тексте ошибки указано, что именно поправить.

## Сборка

```powershell
dotnet build QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1 --nologo
```

`-m:1` отключает многонодовую сборку MSBuild. В некоторых окружениях (ограниченные права на процессы) worker-ноды не стартуют, и сборка падает без внятного сообщения об ошибке — сборка в один узел работает всегда.

## Запуск всех тестов

```powershell
dotnet test QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1
```

## Запуск по категориям

```powershell
# только проверка настройки .env
dotnet test QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1 --filter "Category=Readiness"

# только архитектурные договорённости
dotnet test QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1 --filter "Category=Architecture"

# только живые тесты (нужен запущенный сервер и флаг AI_RUN_LIVE_TESTS=1)
dotnet test QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1 --filter "Category=Live"

# всё, что не требует ни .env, ни модели (например, в CI)
dotnet test QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1 --filter "Category!=Readiness&Category!=Live"
```

## Запуск отдельного класса

```powershell
dotnet test QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1 --filter "FullyQualifiedName~EnvConfigLoaderTests"
```

Классы тестов и то, что они проверяют:

| Класс | Категория | Что проверяет |
|---|---|---|
| `EnvConfigLoaderTests` | — | Синтаксис `.env`: комментарии, кавычки, пробелы, дубликаты, BOM, CRLF, числовые параметры |
| `AppConfigValidatorTests` | — | Обязательные поля, схема и формат `AI_ENDPOINT`, список всех проблем сразу |
| `ChatHistoryManagerTests` | — | System-сообщение, снимок запроса, лимит реплик, удаление только парами |
| `ChatSessionTests` | — | Что уходит модели, что попадает в историю, отмена, таймаут, ошибки |
| `ChatSessionFactoryTests` | — | Сборка клиента и сессии по конфигурации (system-подсказка, лимит) |
| `ChatLoopTests` | — | Вывод, команды `exit`/`quit`, Esc, EOF, устойчивость к сбоям |
| `ConsoleKeyInfoResultTests` | — | Преобразование клавиши консоли, детект Ctrl |
| `ProjectContractTests` | Architecture | Шаблон `.env-public`, `.gitignore`, README, `.csproj`, решение |
| `EnvironmentReadinessTests` | Readiness | Ваш `.env`: наличие, загрузка, валидация |
| `SecretsNotCommittedTests` | Readiness | Ключ из `.env` не встречается в файлах проекта |
| `LiveModelTests` | Live | Реальный запрос к модели, список моделей, две реплики диалога |

## Живые тесты

1. Запустите сервер с моделью.
2. Убедитесь, что `.env` заполнен (`Category=Readiness` зелёный).
3. Включите флаг — в окружении или в `.env`:

```powershell
$env:AI_RUN_LIVE_TESTS = "1"
dotnet test QwenAnswers.Tests\QwenAnswers.Tests.csproj -m:1 --filter "Category=Live"
```

Без флага эти тесты помечаются как `Skipped` с причиной, а не падают.

## Зависимости

- **xunit** v2.9.3 — фреймворк тестирования
- **xunit.runner.visualstudio** v2.5.4 — адаптер для Visual Studio / CLI
- **Microsoft.NET.Test.Sdk** v18.0.1 — SDK запуска тестов
- **Moq** v4.20.72 — подмена `IChatClient` и `IChatSession`
- **FluentAssertions** v8.2.0 — читаемые утверждения

## Как тесты находят файлы проекта

`Support/TestPaths.cs` поднимается от каталога сборки вверх до `QwenAnswers.slnx` и берёт `.env`, `.env-public`, `.gitignore`, `README.md` и `.csproj` оттуда. Путь не зависит от глубины `bin` и от конфигурации сборки.
