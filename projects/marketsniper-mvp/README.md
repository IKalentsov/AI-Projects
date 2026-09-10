# MarketSniper MVP

Сервис поиска проверенных поставщиков и товаров на маркетплейсах.

Пользователь вводит запрос текстом («хочу грецкие орехи») → система собирает предложения
с Ozon, Яндекс.Маркета, Wildberries, ВкусВилла и других площадок, проверяет их по критериям
качества и надёжности продавца и выдаёт только актуальные релевантные результаты со ссылками
на карточки товаров.

---

## Статус

**Этап каркаса.** Заложены правила агентной разработки, структура репозитория, конфигурация
сборки, Docker-контур и пайплайн. Код приложений (backend-решение и React-приложение)
появится отдельными задачами — см. `ARCHITECTURE.md` §10.

---

## Что уже можно запустить

Инфраструктуру разработки — PostgreSQL и Redis:

```bash
cp .env.example .env      # заполнить пароли
docker compose -f docker/compose/compose.infra.yaml --env-file .env up -d
docker compose -f docker/compose/compose.infra.yaml ps    # все сервисы healthy
```

Плюс наблюдаемость (Aspire Dashboard, MIT):

```bash
docker compose -f docker/compose/compose.yaml --env-file .env --profile observability up -d
# UI: http://localhost:18888
```

Порты, профили и остальные сценарии — `docker/README.md`.

---

## Структура репозитория

| Каталог | Что внутри |
|---------|-----------|
| `backend/` | .NET-решение: Clean Architecture, 5 слоёв + тесты, центральные версии пакетов |
| `frontend/` | React-монорепо на pnpm: приложения и общие пакеты |
| `docker/` | Compose-файлы, Dockerfile'ы, конфиг Caddy |
| `ci/` | Шаблоны GitLab CI (подключаются из `.gitlab-ci.yml`) |
| `docs/` | Архитектура, домен, стандарты, ADR |
| `ai-tasks/` | Постановки, отчёты исполнителя, ревью, доработки |
| `.dsh/skills/` | Скиллы проекта для AI-агента |

---

## Документация

| Документ | О чём |
|----------|-------|
| `AGENTS.md` | правила проекта: расположение, роли, стек, структура, красные линии, git-ограничения |
| `WORKFLOW.md` | процесс: цикл задачи, постановки, ревью и его закрытие, Definition of Done |
| `ARCHITECTURE.md` | единый источник правды по архитектуре |
| `docs/domain/overview.md` | предметная область, сценарии, открытые вопросы |
| `docs/frontend/stack.md` | стек фронтенда и версии |
| `docs/backend/*` | соглашения кода, доступ к данным, соглашения API |
| `docs/architecture/*` | слои, инфраструктура, тестирование, CI/CD |
| `docs/adr/` | принятые архитектурные решения и их обоснование |
| `docker/README.md` | контейнеры: запуск, порты, профили |
| `ci/README.md` | пайплайн: стадии, правила, ограничения бесплатного тира |
| `ai-tasks/README.md` | как ведутся задачи |

---

## Стек (кратко)

- **Backend**: .NET 10, ASP.NET Core, Clean Architecture, EF Core + Dapper, Result Pattern.
- **Данные**: PostgreSQL 18, Redis 8.
- **Frontend**: React + TypeScript, Vite, Tailwind CSS, shadcn/ui, TanStack Query.
- **Инфраструктура**: Docker Compose, Caddy, GitLab CI, OpenTelemetry.
- **Тесты**: xUnit v3 + Testcontainers + ArchUnitNET; Vitest + RTL + MSW + Playwright.

Версии и обоснования — `ARCHITECTURE.md` и `docs/frontend/stack.md`.

---

## Целевые команды (после появления кода)

```bash
# Backend
cd backend
dotnet build
dotnet test --solution MarketSniper.slnx -c Debug

# Frontend
cd frontend
pnpm install
pnpm lint && pnpm typecheck && pnpm test && pnpm build

# Полный стек в контейнерах
docker compose -f docker/compose/compose.yaml --env-file .env --profile app up -d --build
```

---

## Правила участия

1. Ветки: `feature/<имя>`, `fix/<имя>`, `chore/<имя>`; `main` защищена.
2. Коммиты — Conventional Commits (`feat(backend): ...`, `fix(web): ...`, `docs: ...`).
3. Перед MR: сборка, тесты и статанализ зелёные (см. `AGENTS.md` §9 — Definition of Done).
4. Секреты — только в `.env` (в git не попадают).
5. Задачи ведутся через `ai-tasks/`: постановка → отчёт → ревью → доработки.
