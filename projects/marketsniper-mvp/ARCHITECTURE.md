# MarketSniper MVP — архитектура

> **Единый источник правды по архитектуре проекта.**
> Правила работы агента — `AGENTS.md`; специфика стека — `backend/AGENTS.md`, `frontend/AGENTS.md`.
> Если документ расходится с кодом — прав код, но это дефект: расхождение фиксируется и
> устраняется в том же прогоне (скилл `architecture-drift-check`).
>
> Версии фронтенд-пакетов — `docs/frontend/stack.md`; версии .NET-пакетов —
> `backend/Directory.Packages.props`. Здесь версии не дублируются.

**Последнее обновление:** 2026-09-10
**Статус:** каркас проекта. Код приложений ещё не создан (см. §10).

---

## 1. Продукт

**MarketSniper** — сервис поиска проверенных поставщиков и товаров на маркетплейсах.

Пользователь вводит свободный запрос («хочу грецкие орехи») → система собирает предложения
с маркетплейсов (Ozon, Яндекс.Маркет, Wildberries, ВкусВилл и др.), проверяет их по критериям
качества и надёжности (в перспективе — нормативные требования: чистота, обработка, условия
хранения) и выдаёт только актуальные релевантные результаты со ссылками на карточки.

Домен, сценарии и открытые вопросы — `docs/domain/overview.md`; термины — `docs/domain/glossary.md`.

---

## 2. Архитектурные решения (сводка)

| № | Решение | Документ |
|---|---------|----------|
| 0001 | Монорепозиторий: backend + frontend + инфраструктура | `docs/adr/0001-monorepo.md` |
| 0002 | Clean Architecture на .NET 10 (эталон — DirectoryService) | `docs/adr/0002-clean-architecture-dotnet.md` |
| 0003 | Фронтенд: React + Vite SPA | `docs/adr/0003-frontend-react-spa.md` |
| 0004 | UI: Tailwind CSS + shadcn/ui | `docs/adr/0004-ui-tailwind-shadcn.md` |
| 0005 | Данные: PostgreSQL + Redis | `docs/adr/0005-data-postgres-redis.md` |
| 0006 | Container-first: всё окружение в Docker | `docs/adr/0006-container-first.md` |
| 0007 | Аутентификация отложена, место зарезервировано | `docs/adr/0007-auth-deferred.md` |
| 0008 | Стратегия тестирования: пирамида + архитектурные тесты | `docs/adr/0008-testing-strategy.md` |
| 0009 | CI/CD на GitLab CI с шаблонами в `ci/` | `docs/adr/0009-gitlab-ci.md` |
| 0010 | Наблюдаемость: OpenTelemetry/OTLP, dev — Aspire Dashboard | `docs/adr/0010-observability-otlp.md` |
| 0011 | Базовые образы и стратегия сборки контейнеров | `docs/adr/0011-docker-images.md` |

---

## 3. Стек

| Слой | Технологии | Где детали |
|------|-----------|-----------|
| Backend | .NET 10, ASP.NET Core, Clean Architecture (5 слоёв), CSharpFunctionalExtensions (Result), FluentValidation, Scalar (OpenAPI) | `backend/AGENTS.md`, `docs/architecture/clean-architecture.md` |
| Данные | PostgreSQL 18 (EF Core + Dapper), Redis 8 (кэш, rate-limit, идемпотентность) | `docs/backend/data-access.md` |
| Frontend | React, TypeScript, Vite, React Router, TanStack Query, Zustand, Tailwind CSS, shadcn/ui, react-hook-form + zod | `docs/frontend/stack.md` |
| Инфраструктура | Docker + Docker Compose, Caddy (reverse-proxy), GitLab CI, SonarQube Community Build | `docs/architecture/infrastructure-docker.md`, `docs/architecture/ci-cd.md` |
| Тесты | xUnit v3 (unit / integration / architecture), Testcontainers; Vitest + RTL + MSW + Playwright | `docs/architecture/testing-strategy.md` |
| Наблюдаемость | Serilog + OpenTelemetry (OTLP); dev — Aspire Dashboard | `docs/adr/0010-observability-otlp.md` |

Ограничение по зависимостям: **только open source** с разрешительными лицензиями.
Проверенные исключения и отклонённые пакеты (платные лицензии) зафиксированы в
`backend/Directory.Packages.props` и `docs/frontend/stack.md`.

---

## 4. Структура репозитория

```
MarketSniperMVP/
├── AGENTS.md                      # правила проекта для агента и людей
├── ARCHITECTURE.md                # этот файл
├── README.md                      # обзор и быстрый старт
├── .editorconfig                  # форматирование (C#, TS, YAML)
├── .env.example                   # шаблон переменных окружения
├── .gitlab-ci.yml                 # точка входа пайплайна
├── backend/                       # .NET-решение
│   ├── MarketSniper.slnx
│   ├── global.json                # фиксация SDK + режим запуска тестов (MTP)
│   ├── Directory.Build.props      # общие свойства сборки, анализаторы
│   ├── Directory.Packages.props   # центральные версии пакетов
│   ├── .globalconfig              # уровни правил анализаторов
│   ├── src/                       # 6 проектов слоёв
│   └── tests/                     # unit / integration / architecture
├── frontend/                      # pnpm-монорепо
│   ├── apps/                      # web (+ landing при необходимости)
│   └── packages/                  # ui, api-client, shared, eslint-config, tsconfig
├── docker/                        # compose-файлы, Dockerfile'ы, Caddyfile
├── ci/                            # шаблоны GitLab CI
├── docs/                          # архитектура, домен, стандарты, ADR
├── ai-tasks/                      # постановки, отчёты, ревью, fixes
└── .dsh/skills/                   # скиллы проекта для агента
```

---

## 5. Backend: слои и зависимости

```
Web ──▶ Infrastructure.Postgres / Infrastructure.Redis ──▶ Core ──▶ Domain ──▶ Contracts
```

| Проект | Назначение |
|--------|-----------|
| `MarketSniper.Contracts` | DTO и контракты API (без логики) |
| `MarketSniper.Domain` | сущности, value objects, инварианты |
| `MarketSniper.Core` | use cases, валидаторы, абстракции |
| `MarketSniper.Infrastructure.Postgres` | EF Core, Dapper, конфигурации, миграции |
| `MarketSniper.Infrastructure.Redis` | кэш, rate-limit, распределённые блокировки |
| `MarketSniper.Web` | ASP.NET Core: контроллеры, middleware, DI, health-checks |

Правило: зависимости направлены **только внутрь**; соблюдение проверяется архитектурными
тестами (`TngTech.ArchUnitNET`). Подробности — `docs/architecture/clean-architecture.md`.

Обязательные паттерны: Result Pattern, Value Objects, strongly-typed ID, фабрики сущностей,
`snake_case` в БД, конфигурации EF Core отдельными классами.

---

## 6. Frontend: структура и контракт с API

- pnpm-монорепо: `apps/web` (SPA), `packages/ui` (компоненты и токены),
  `packages/api-client` (генерируемый клиент), `packages/shared` (утилиты и типы).
- Структура внутри приложения — feature-based с элементами FSD:
  `app/`, `routes/`, `features/`, `entities/`, `shared/`; cross-feature импорты запрещены
  и проверяются ESLint. Детали — `docs/frontend/project-structure.md`.
- **Контракт**: OpenAPI-схема генерируется backend'ом (`/openapi/v1.json`), клиент фронтенда
  генерируется из неё — ручное дублирование типов запрещено.
- Состояние поиска живёт в URL (шарируемая ссылка), серверные данные — в TanStack Query,
  клиентское состояние — в Zustand. Детали — `docs/frontend/data-and-state.md`.

---

## 7. Данные

- **PostgreSQL 18** — источник правды: реляционная модель домена, `jsonb` для
  полуструктурированных атрибутов карточек, полнотекстовый поиск, партиционирование
  исторических таблиц по мере роста.
- **Redis 8** — кэш ответов внешних API, rate-limit обращений к площадкам, идемпотентность
  фоновых задач. Данные в Redis всегда восстановимы.
- Доступ: EF Core (запись, типовые чтения, миграции), Dapper (сложные выборки, аналитика).
- Схема и соглашения — `docs/backend/data-access.md`.

---

## 8. Инфраструктура и эксплуатация

- Всё окружение в контейнерах; единственная точка входа — Caddy (маршрутизация `/api`,
  `/scalar`, `/health/*` и SPA). Реплики backend/frontend масштабируются за прокси.
- compose разделён по назначению: `compose.infra.yaml` (PostgreSQL, Redis),
  `compose.app.yaml` (backend, frontend, proxy), `compose.tools.yaml` (наблюдаемость,
  pgAdmin, SonarQube). Профили: `app`, `observability`, `seq`, `tools`, `sonar`.
- Конфигурация — через `.env` (шаблон `.env.example`); секреты в git не попадают.
- Health-checks: `/health/live` (процесс) и `/health/ready` (зависимости).
- Наблюдаемость: единый OTLP-транспорт; dev — Aspire Dashboard (MIT).
- Пайплайн: `build → test → security → package → deploy`, избирательный по каталогам,
  только бесплатные инструменты. Миграции БД применяются отдельным шагом деплоя
  (не на старте приложения — иначе гонка между репликами).

Подробности — `docs/architecture/infrastructure-docker.md`, `docs/architecture/ci-cd.md`.

---

## 9. Тестирование

| Уровень | Backend | Frontend |
|---------|---------|----------|
| Unit | xUnit v3 + NSubstitute + AwesomeAssertions | Vitest |
| Integration | `WebApplicationFactory` + Testcontainers (PostgreSQL, Redis) + WireMock.Net | Vitest + RTL + MSW |
| Architecture | ArchUnitNET (границы слоёв, конвенции) | ESLint `import-x/no-restricted-paths` |
| E2E | — | Playwright |

Правила: TDD для домена и бизнес-правил; тесты на реальных зависимостях в integration-слое;
детерминированность обязательна; на каждый баг — воспроизводящий тест.
Ориентиры покрытия: 70–80% нового кода, 100% на критичных путях.

**Architecture fitness.** У каждого архитектурного правила есть владелец (проект/слой) и
ближайшая исполняемая проверка; новая или изменённая граница сопровождается **негативной
фикстурой** — проверкой, которая падает, когда правило нарушено. Перечень правил и их проверок —
`docs/architecture/clean-architecture.md` (раздел «Architecture fitness»); процесс — `WORKFLOW.md` §8.

---

## 10. Состояние и план

**Готово (каркас):** правила для агента (`AGENTS.md`, вложенные), скиллы (`.dsh/skills`),
документация и ADR (`docs/`), шаблоны задач (`ai-tasks/`), конфигурация сборки бэкенда
(`Directory.Build.props`, `Directory.Packages.props`, `.globalconfig`, `global.json`),
Docker-контур (`docker/`), пайплайн (`ci/`, `.gitlab-ci.yml`).

**Не создано:** решения `MarketSniper.slnx` и проекты слоёв, приложение React, Dockerfile'ы,
`.dockerignore`, `compose.override.yaml`. Они появляются отдельными задачами (см. `ai-tasks/`).

**Ближайшие шаги:**

1. Согласовать доменную модель и критерии проверки (см. `docs/domain/overview.md`).
2. Зафиксировать источники данных по каждой площадке (API/MCP/парсинг) и правовые ограничения.
3. Создать решение и первый вертикальный срез: поиск → результаты → карточка предложения.
4. Определить контур деплоя (сервер, домены, TLS) и включить стадию `deploy`.

**Открытые вопросы** перечислены в `AGENTS.md` §15 и `docs/domain/overview.md` §6.

---

## 11. Владение документом

- Владелец — архитектор (DeepSeek V4.x). Исполнитель (Qwen3.6-MTP) — только чтение.
- Изменения вносятся **до** постановки задачи, затрагивающей архитектуру, и после
  подтверждения пользователя.
- При изменении структуры, контрактов, стека или пакетов документ актуализируется
  в том же прогоне (скилл `architecture-drift-check`).
