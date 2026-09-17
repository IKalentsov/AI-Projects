# docker — контейнеры проекта

Всё окружение проекта живёт в контейнерах: PostgreSQL, Redis, backend, frontend,
reverse-proxy и инструменты разработки. Локальные правила — в корневом `AGENTS.md`,
архитектура — в `ARCHITECTURE.md`.

## Файлы

```
docker/
├── compose/
│   ├── compose.yaml          # единая точка входа (include остальных)
│   ├── compose.infra.yaml    # postgres + redis (обязательный минимум)
│   ├── compose.app.yaml      # backend + frontend + proxy (профиль app)
│   └── compose.tools.yaml    # seq / pgadmin / sonarqube (профили)
├── backend/
│   └── Dockerfile            # появится вместе с .NET-проектом
├── frontend/
│   └── Dockerfile            # появится вместе с React-приложением
└── proxy/
    └── Caddyfile             # /api → backend, остальное → frontend
```

## Первый запуск

```bash
# 1. Переменные окружения
cp .env.example .env          # затем вписать свои пароли

# 2. Инфраструктура (создаёт сеть marketsniper-net)
docker compose -f docker/compose/compose.infra.yaml --env-file .env up -d

# 3. Проверка
docker compose -f docker/compose/compose.infra.yaml ps   # все — healthy
```

## Типовые сценарии

```bash
# Только инфраструктура (при разработке код запускается нативно)
docker compose -f docker/compose/compose.infra.yaml --env-file .env up -d

# Инфраструктура + Aspire Dashboard (логи, трейсы, метрики)
docker compose -f docker/compose/compose.yaml --env-file .env \
  --profile observability up -d

# Полный стек в контейнерах (после появления Dockerfile'ов)
docker compose -f docker/compose/compose.yaml --env-file .env \
  --profile app --build up -d

# Инструменты: pgAdmin и SonarQube
docker compose -f docker/compose/compose.yaml --env-file .env \
  --profile tools --profile sonar up -d

# Остановить всё (данные в именованных томах сохраняются)
docker compose -f docker/compose/compose.yaml --env-file .env down

# Остановить и удалить данные (чистый старт)
docker compose -f docker/compose/compose.yaml --env-file .env down -v
```

## Порты (по умолчанию)

| Сервис | Хост | Контейнер | Назначение |
|--------|------|-----------|-----------|
| PostgreSQL | 5435 | 5432 | Основная БД (5434 занят DirectoryService) |
| Redis | 6380 | 6379 | Кэш/rate-limit |
| Backend | 8080 | 8080 | ASP.NET Core API |
| Frontend | 5173 | 8080 | Раздача статики |
| Proxy | 80 | 80 | Единая точка входа |
| Aspire Dashboard | 18888 | 18888 | Логи, трейсы, метрики (OTLP) |
| Aspire OTLP | 18889 | 18889 | Приём телеметрии |
| Seq (опционально) | 8081 | 80 | Просмотр логов; лицензия проприетарная |
| pgAdmin | 8082 | 80 | UI для БД |
| SonarQube | 9000 | 9000 | Статанализ |

Порты меняются в `.env` — не в compose-файлах.

## Профили

| Профиль | Что поднимает |
|---------|---------------|
| (без профиля) | PostgreSQL, Redis — базовый минимум |
| `app` | backend, frontend, reverse-proxy |
| `observability` | Aspire Dashboard (MIT) — основной инструмент dev |
| `seq` | Seq — опционально: лицензия проприетарная (EULA), бесплатен только тариф Individual |
| `tools` | pgAdmin |
| `sonar` | SonarQube Community Build + его БД |

`--profile "*"` поднимает всё сразу.

## Правила

- **Секреты только через `.env`** (шаблон — `.env.example`). `.env` не коммитится.
- **Версии образов пинятся точно** — `postgres:18.6-alpine`, `redis:8.10.1-alpine`, без `latest`,
  плавающих тегов и образов без тега (даже для dev-инструментов и CI). Обновление — вручную,
  отдельным коммитом. Правило и порядок — `docs/architecture/infrastructure-docker.md`
  («Версии образов: точный пин»), решение — `docs/adr/0012-image-version-pinning.md`.
- У каждого сервиса — `healthcheck`; зависимости — через `condition: service_healthy`.
  Приложение не должно стартовать раньше готовности БД.
- Порты наружу публикует только proxy (в prod-контуре). Остальные сервисы доступны
  внутри сети `marketsniper-net`.
- Данные — в именованных томах с префиксом `marketsniper-`.
- `backend` и `frontend` собираются multi-stage: SDK/node — только на этапе сборки,
  в runtime-образе их нет. Запуск — от непривилегированного пользователя.
- Масштабирование: `docker compose up --scale backend=3` работает только при отказе
  от публикации порта backend наружу (трафик идёт через proxy).

## Что появится вместе с кодом

- `docker/backend/Dockerfile` — multi-stage сборка .NET 10 (`sdk` → `aspnet`), non-root,
  `HEALTHCHECK`, кэш NuGet через `--mount=type=cache`.
- `docker/backend/Dockerfile` (target `development`) — для `dotnet watch` в контейнере.
- `docker/frontend/Dockerfile` — multi-stage (`node` + pnpm → nginx/Caddy), SPA-fallback,
  кэш статики, non-root.
- При необходимости — `compose.override.yaml` для локальных правок (в git не попадает).

Правила написания этих файлов — скилл `docker-and-ci` и `docs/architecture/infrastructure-docker.md`.
