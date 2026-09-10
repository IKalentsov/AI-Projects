# Стратегия тестирования

**Последнее обновление:** 2026-09-10
Решение зафиксировано в `docs/adr/0008-testing-strategy.md`. Здесь — практика: что, чем и как.

## Уровни тестов

### Backend

| Уровень | Проект | Инструменты | Время прогона |
|---------|--------|-------------|---------------|
| Unit | `tests/MarketSniper.UnitTests` | xunit.v3, NSubstitute, AwesomeAssertions | секунды |
| Integration | `tests/MarketSniper.IntegrationTests` | + `WebApplicationFactory`, Testcontainers (PostgreSQL, Redis), Respawn, WireMock.Net | десятки секунд |
| Architecture | `tests/MarketSniper.ArchitectureTests` | TngTech.ArchUnitNET | секунды |

Запуск тестов — через **Microsoft.Testing.Platform** (xunit.v3 4.0; `backend/global.json`):

```bash
cd backend
dotnet test --solution MarketSniper.slnx -c Debug
dotnet test --project tests/MarketSniper.UnitTests/MarketSniper.UnitTests.csproj
dotnet test --solution MarketSniper.slnx -c Release -- --report-trx --results-directory ./TestResults
dotnet test --solution MarketSniper.slnx -c Release -- --coverage --coverage-output-format cobertura
```

`Microsoft.NET.Test.Sdk` при этом подходе не используется — не добавлять.

Architecture-тесты — это **fitness-функции архитектуры**: каждое правило имеет владельца и
исполняемую проверку, а новая граница сопровождается негативной фикстурой, доказывающей,
что проверка падает при нарушении (`WORKFLOW.md` §8,
`docs/architecture/clean-architecture.md`, раздел «Architecture fitness»).

### Frontend

| Уровень | Инструменты | Что покрывает |
|---------|-------------|---------------|
| Unit | Vitest | чистые функции, форматтеры, схемы zod, мапперы |
| Component | Vitest + React Testing Library | поведение компонента |
| Integration | Vitest + RTL + MSW | фича/страница целиком, сеть замокана |
| E2E | Playwright | сквозные сценарии на реальном стеке |

```bash
cd frontend
pnpm test              # unit + component + integration
pnpm test:coverage
pnpm test:e2e          # Playwright
```

Детали фронтенд-тестов — `docs/frontend/testing.md`.

## Что обязательно покрывать

**Backend**
- Инварианты домена и value objects: границы значений, невалидные входы, нормализация.
- Применение критериев проверки и расчёт оценки доверия — **все ветки**.
- Работа с ценами и валютами: округление, культура, отсутствие цены.
- Use cases: успех, бизнес-ошибка, отсутствие данных, конфликт.
- Эндпоинты: коды ответов, формат ошибок, пагинация, валидация входа.
- Миграции: интеграционный тест поднимает БД с нуля и применяет миграции.

**Frontend**
- Схемы валидации форм и разбор ошибок API.
- Состояния экрана: загрузка, пусто, ошибка, успех.
- Ключевой сценарий поиска: ввод запроса → результаты → карточка предложения.
- Состояние поиска в URL (шарируемая ссылка восстанавливает результат).

## Что НЕ тестируем

- Геттеры/сеттеры и тривиальные маппинги без логики.
- Сгенерированный код (API-клиент, миграции, снапшоты EF Core).
- Внешние библиотеки и фреймворк.
- Верстку «по пикселям» (snapshot-тесты CSS) — вместо этого проверяем поведение и a11y.
- Реальные обращения к маркетплейсам — только фикстуры.

## Правила написания

1. **Имя теста = сценарий + ожидание**: `Search_WithEmptyQuery_ReturnsValidationError`.
2. **Один тест — одно поведение.** Не «проверяем всё про эндпоинт» одним методом.
3. **Arrange / Act / Assert** с пустыми строками между блоками.
4. **Никакой логики в тестах**: без `if`, циклов и вычислений ожидаемых значений.
5. **Тестовые данные — явные**: билдеры (`OfferBuilder`) для объёмных объектов, но значения
   в тесте видны и понятны.
6. **Детерминированность**: `TimeProvider`/`FakeTimeProvider`, фиксированный `Guid`/`Random`
   через seed, отсутствие зависимости от порядка тестов.
7. **Интеграционные тесты изолированы**: каждый тест-класс получает чистую БД (Respawn),
   контейнер поднимается один раз на сборку (fixture).
8. **Баг → сначала тест**: падающий тест воспроизводит дефект, потом идёт исправление.

## Покрытие

- Ориентир для нового кода: **70–80%** строк/функций.
- **100%** — критичные пути: применение критериев, расчёт доверия, цены, работа с деньгами.
- Сгенерированный код и типы исключаются из расчёта.
- Покрытие — индикатор, а не цель: тест без проверки поведения не увеличивает качество.

## Тесты в пайплайне

| Стадия | Что запускается | Блокирует ли merge |
|--------|-----------------|--------------------|
| build | сборка backend и frontend | да |
| quality | анализаторы .NET, `dotnet format --verify-no-changes`, ESLint, typecheck | да |
| test | unit + architecture (быстрые), затем integration (Testcontainers) | да |
| test | Vitest (unit/component/integration) | да |
| e2e | Playwright на собранных образах | да для MR в `main` |
| sonar | анализ + quality gate | предупреждение, затем — блокировка |

Артефакты прогонов: TRX (backend) и JUnit/HTML-отчёт (frontend) — публикуются всегда,
в том числе при падении.
