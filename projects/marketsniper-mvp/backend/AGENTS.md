# AGENTS.md — backend (.NET)

> Специфика бэкенда. Дополняет корневой `AGENTS.md`; при конфликте приоритет у этого файла
> в вопросах .NET-кода. Архитектура целиком — в `ARCHITECTURE.md`.

## 1. Стек и решение

- .NET 10 (`net10.0`), последняя версия C#, `Nullable=enable`, `ImplicitUsings=enable`.
- Clean Architecture, 5 слоёв + тесты.
- Файл решения: `backend/MarketSniper.slnx`.
- Общие свойства сборки: `backend/Directory.Build.props` (анализаторы включены для всех проектов).
- Версии пакетов: **только** `backend/Directory.Packages.props` (центральное управление).
  В `.csproj` версии не указываются.
- Уровни правил анализаторов: `backend/.globalconfig`.

```
backend/
├── MarketSniper.slnx
├── Directory.Build.props            # TFM, анализаторы, TreatWarningsAsErrors
├── Directory.Packages.props         # версии всех пакетов
├── .globalconfig                    # уровни правил анализаторов
├── src/
│   ├── MarketSniper.Contracts/                  # DTO, контракты API (без логики)
│   ├── MarketSniper.Domain/                     # сущности, value objects, доменные правила
│   ├── MarketSniper.Core/                       # use cases, валидаторы, абстракции
│   ├── MarketSniper.Infrastructure.Postgres/    # EF Core, конфигурации, репозитории
│   ├── MarketSniper.Infrastructure.Redis/       # кэш, rate-limit, распределённые блокировки
│   └── MarketSniper.Web/                        # ASP.NET Core: контроллеры, middleware, DI
└── tests/
    ├── MarketSniper.UnitTests/                  # домен, use cases, валидаторы
    ├── MarketSniper.IntegrationTests/           # API + БД (Testcontainers), кэш
    └── MarketSniper.ArchitectureTests/          # запрет нарушений слоёв и конвенций
```

Направление зависимостей — строго внутрь:

```
Web ──▶ Infrastructure.* ──▶ Core ──▶ Domain ──▶ Contracts
```

`Domain` не знает ни о БД, ни об HTTP, ни о DI. `Core` не знает об Infrastructure.
Проверяется архитектурными тестами, а не «на честном слове».

## 2. Паттерны (обязательные)

1. **Result Pattern** (`CSharpFunctionalExtensions`): бизнес-операции возвращают
   `Result<T>` / `Result<T, Error>`; исключения — только для действительно исключительных
   ситуаций (сбой инфраструктуры). Ошибки — типизированные, с кодом, а не «строка на всё».
2. **Value Objects** вместо сырых примитивов: `Slug`, `Money`, `Price`, `Rating`, `ProductUrl`
   и т.п. Валидация — в фабрике VO, а не в контроллерах.
3. **Strongly-typed ID** для идентификаторов агрегатов.
4. **Фабрики сущностей**: `public static Result<Product> Create(...)` — никаких публичных
   конструкторов «наружу» и никаких сеттеров, ломающих инварианты.
5. **Связи между агрегатами — только по ID**, без навигационных коллекций через границы агрегатов.
6. **snake_case в БД**: таблицы и колонки (`create_at`, `update_at`, `parent_id`).
7. **Timestamps** `CreateAt` / `UpdateAt` (UTC) — у каждой сохраняемой сущности.
8. **Entity Configuration Pattern**: `IEntityTypeConfiguration<T>` в
   `Infrastructure.Postgres/Configurations`, подключение через `ApplyConfigurationsFromAssembly`.
9. **Явная валидация входа** — FluentValidation в `Core`, регистрация через DI-расширения.

## 3. Данные

- EF Core — для записи и типовых чтений; **Dapper** — для сложных/аналитических выборок.
- Чтение — `AsNoTracking()`; проекции в DTO прямо в запросе, без «тянуть агрегат целиком».
- Ленивая загрузка запрещена. Явный `Include` только там, где он нужен.
- Пагинация обязательна для любых списочных эндпоинтов (keyset предпочтительнее offset).
- Миграции — только через `dotnet ef migrations add <Name>` в
  `Infrastructure.Postgres`; руками файлы миграций не правим.
- Индексы обязательны под все поля фильтрации/сортировки поиска и под внешние ключи.
- Redis: кэш внешних ответов, rate-limit обращений к маркетплейсам, идемпотентность задач.
  Ключи — с префиксом домена и версией (`marketsniper:v1:products:{id}`).

## 4. Внешние интеграции (маркетплейсы, MCP)

- Только через `IHttpClientFactory` + типизированные клиенты; ключи и базовые URL — из конфигурации.
- Обязательны: таймаут, ретраи с backoff и jitter (`Microsoft.Extensions.Http.Resilience`),
  circuit breaker, ограничение параллелизма, уважение rate-limit площадки.
- Каждый ответ внешнего API валидируется и логируется; «сырой» ответ не течёт в домен —
  сначала маппинг в нашу модель.
- Парсинг/скрейпинг — отдельный слой с фикстурами в тестах; никаких сетевых вызовов в unit-тестах.
- Секреты внешних API — только через переменные окружения / user-secrets, не в appsettings.

## 5. API

- Контракты — в `MarketSniper.Contracts` (DTO + валидация формата), возвращаются наружу.
- Контроллеры тонкие: валидация → вызов use case → маппинг `Result` в HTTP-ответ.
- Ошибки — единый формат (ProblemDetails / единый `ErrorResponse`), без «голых» 500 наружу.
- OpenAPI — через `Microsoft.AspNetCore.OpenApi`, интерактивная документация — Scalar.
- Версионирование API — префикс `/api/v{n}` с первого дня.
- Health-checks: `/health/live` (процесс жив) и `/health/ready` (БД, Redis, внешние зависимости).
- Correlation ID — из заголовка или сгенерированный, во всех логах и в ответе.

## 6. Логирование и конфигурация

- Serilog, структурированные логи. **Запрещена** интерполяция строк в шаблонах логов.
- Логируем: входящие запросы (кроме чувствительных данных), исход use case, ошибки внешних
  вызовов с контекстом (площадка, запрос, статус), метрики длительности.
- Не логируем: токены, ключи, персональные данные, полные ответы маркетплейсов.
- Конфигурация — через `IOptions<T>` + `appsettings.{Environment}.json` + переменные окружения.
- `appsettings.Development.json` — только дев-значения; секретов в git нет.

## 7. Тесты

- `MarketSniper.UnitTests` — домен, value objects, валидаторы, use cases с моками (NSubstitute).
- `MarketSniper.IntegrationTests` — реальные HTTP-запросы через `WebApplicationFactory` и
  **реальная** PostgreSQL из Testcontainers; Redis — Testcontainers или in-memory по ситуации.
- `MarketSniper.ArchitectureTests` — ArchUnitNET: запрет ссылок Domain → Infrastructure,
  Core → Web, запрет `async void`, обязательные суффиксы (`*Handler`, `*Validator`).
- **Architecture fitness**: каждое архитектурное правило имеет владельца и исполняемую проверку,
  а новая граница — **негативную фикстуру** (сборка-нарушитель, на которой проверка обязана
  упасть). Перечень — `docs/architecture/clean-architecture.md`, раздел «Architecture fitness».
- TDD: сначала падающий тест на бизнес-правило, потом реализация.
- Один тест — одно поведение. Имя теста описывает сценарий и ожидание.

## 8. Команды

Тесты запускаются в режиме **Microsoft.Testing.Platform** (xunit.v3 4.0; см. `backend/global.json`).
`Microsoft.NET.Test.Sdk` при этом не используется — не добавлять его в тестовые проекты.

```bash
cd backend
dotnet restore --locked-mode
dotnet build --no-restore
dotnet format --verify-no-changes

# Все тесты решения (MTP-синтаксис: путь передаётся через --solution/--project)
dotnet test --solution MarketSniper.slnx -c Debug

# Точечный прогон одного проекта
dotnet test --project tests/MarketSniper.UnitTests/MarketSniper.UnitTests.csproj

# Отчёты: TRX для CI и покрытие в cobertura (для SonarQube)
dotnet test --solution MarketSniper.slnx -c Release \
  -- --report-trx --coverage --coverage-output-format cobertura --results-directory ./TestResults

# Миграции
dotnet ef migrations add <Name> --project src/MarketSniper.Infrastructure.Postgres \
  --startup-project src/MarketSniper.Web
```

## 9. Красные линии

- Менять версии пакетов вне `Directory.Packages.props`.
- Отключать анализатор/правило вместо исправления кода (только через архитектора, с обоснованием).
- Публичные сеттеры у сущностей, анемичная модель, «сервисы на всё».
- `async void`, `.Result`, `.Wait()`, `Task.Run` в ASP.NET-коде.
- Строки подключения, ключи API и пароли в коде/appsettings.
- Логика в контроллерах и в конфигурациях EF Core.
- Одна миграция на несколько несвязанных изменений.
