# Инфраструктура в контейнерах (Docker)

**Последнее обновление:** 2026-09-10
Решение «всё окружение в контейнерах» — `docs/adr/0006-container-first.md`, выбор базовых образов —
`docs/adr/0011-docker-images.md`. Команды запуска и состав файлов — `docker/README.md`.
Здесь — правила: что где живёт, как пишутся Dockerfile'ы, что запрещено.

## Топология контуров

| Контур | Что поднимается | Профиль |
|--------|-----------------|---------|
| Инфраструктура | `postgres`, `redis` | без профиля (всегда) |
| Приложения | `backend`, `frontend`, `proxy` | `app` |
| Наблюдаемость | `aspire-dashboard`, `seq` | `observability`, `seq` |
| Инструменты | `pgadmin`, `sonarqube` + его БД | `tools`, `sonar` |

**Сеть.** `marketsniper-net` (bridge) создаётся `docker/compose/compose.infra.yaml`, в остальных файлах —
`external: true`, поэтому инфраструктура поднимается первой (иначе «network not found»). `postgres` и
`redis` в prod-контуре порт не публикуют: доступ по имени сервиса внутри сети.
**Тома** — именованные, с префиксом `marketsniper-` (`postgres-data`, `redis-data`, `caddy-*`, `sonar-*`).
Всё, что нельзя терять, живёт только в томе; `down -v` — сознательное разрушение окружения.
**Порты.** Наружу публикует только `proxy` (prod-контур); остальные публикации — отладочные (`.env`).

| Сервис | Хост | Контейнер | Назначение |
|--------|------|-----------|-----------|
| Proxy (Caddy) | 80 | 80 | Единая точка входа: API, SPA, health |
| Backend | 8080 | 8080 | Отладка API напрямую, минуя proxy |
| Frontend | 5173 | 8080 | Отладка статики напрямую |
| PostgreSQL | 5435 | 5432 | Подключение из IDE/psql с хоста (5434 занят DirectoryService) |
| Redis | 6380 | 6379 | Подключение из redis-cli с хоста |
| Aspire Dashboard | 18888 / 18889 | 18888 / 18889 | UI наблюдаемости / приём OTLP (gRPC) |
| Seq | 8081 / 5341 | 80 / 5341 | UI логов / приём (опциональный профиль) |
| pgAdmin | 8082 | 80 | UI для БД (профиль `tools`) |
| SonarQube | 9000 | 9000 | Статанализ (профиль `sonar`) |

## Правила Dockerfile'ов

**Общее.** Multi-stage обязателен: SDK и Node присутствуют только на стадии сборки. Runtime работает от
непривилегированного пользователя. В контекст сборки не попадает лишнее — `.dockerignore` в корне:

```
**/bin  **/obj  **/node_modules  **/dist  .git  .vs  .idea
*.user  .env*  **/TestResults  **/coverage*.xml  **/playwright-report
```

### Версии образов: точный пин

**Правило:** каждый образ пинится **точной стабильной версией** и обновляется только вручную.
Решение и обоснование — `docs/adr/0012-image-version-pinning.md`.

| Что | Разрешено | Запрещено |
|-----|-----------|-----------|
| Stateful-сервисы (PostgreSQL, Redis) | `postgres:18.6-alpine`, `redis:8.10.1-alpine` | `postgres:18-alpine`, `postgres`, `postgres:latest` |
| Сборка и рантайм приложений (.NET SDK/aspnet, Node, Caddy) | `sdk:10.0.401-noble`, `node:24.21.0-alpine`, `caddy:2.11.4-alpine` | `sdk:10.0`, `node:24-alpine`, `caddy:2-alpine` |
| Инструменты разработки (pgAdmin, Aspire Dashboard, SonarQube) | точная версия | `latest`, плавающий тег линейки (`sonarqube:community`) |
| Образы CI (Trivy, Syft, cosign, docker CLI) | точная версия | `latest`, образ без тега |

Почему так:

- Плавающий тег (`18-alpine`) двигается при выходе минорной версии, `latest` — при любой.
  `docker compose pull` (он есть в сценарии деплоя) молча подтянет новое — и это ровно тот
  случай, когда «утром всё сломалось».
- Для PostgreSQL это ещё опаснее: путь тома содержит мажор (`/var/lib/postgresql/18/docker`),
  поэтому скачок мажора при `image: postgres` даёт пустую БД или отказ старта.
- Образ **без тега** (`image: postgres`) — это и есть `latest`, просто незаметно.

Порядок обновления версии:

1. Узнаём о новой версии из Renovate (dashboard/PR) — не из внезапного `pull`.
2. Читаем release notes: breaking changes, требования к данным и конфигу.
3. Для stateful-сервисов — проверяем совместимость данных (для мажора PostgreSQL: `pg_upgrade`
   и смена пути тома).
4. Меняем тег **одним отдельным коммитом** (не вперемешку с фичами), прогоняем тесты.
5. Обновление образов — плановая работа (например, раз в месяц), security-патчи — вне очереди.

Проверка (fitness-функция): джоба падает, если в `docker/**` найден `:latest`, плавающий тег
или образ без тега. Правило вступает в силу вместе с пайплайном (см. `ci/README.md`).

### Текущие пины (проверено 10.09.2026)

Digest — справочная информация для аудита (в compose не зашит). Прочерк = не проверялся:
реестр MCR отдаёт манифест в формате, который не принимает инструмент выборки.

| Сервис | Образ | Digest (index) |
|--------|-------|----------------|
| PostgreSQL (infra и БД SonarQube) | `postgres:18.6-alpine` | `sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2` |
| Redis | `redis:8.10.1-alpine` | `sha256:becdda6c7f4b3fb42e42fd7f120bbf5c54c4caaaf16f26da24e4563d2c1f0576` |
| Caddy (proxy) | `caddy:2.11.4-alpine` | `sha256:5f5c8640aae01df9654968d946d8f1a56c497f1dd5c5cda4cf95ab7c14d58648` |
| Node (сборка фронта и CI) | `node:24.21.0-alpine` | `sha256:be80f76cf40ec8e42b9bec49f60a55e0660f30af58d3e5a25530785b30ea67e2` |
| .NET SDK (CI) | `mcr.microsoft.com/dotnet/sdk:10.0.401-noble` | — |
| .NET runtime (образ backend) | `mcr.microsoft.com/dotnet/aspnet:10.0.12-noble` | — |
| Aspire Dashboard (dev) | `mcr.microsoft.com/dotnet/aspire-dashboard:13.5.2` | — |
| pgAdmin (профиль `tools`) | `dpage/pgadmin4:9.17` | `sha256:2f4ce946ddf8360680d7eff4eaba1d91859eb6b4003e6623bad5c63a322c2f4d` |
| Seq (профиль `seq`) | `datalust/seq:2026.1.17114` | `sha256:fa405502a3c57884c88477b78b55ca76c5a3fb1a4d20c6a185c24e750db34056` |
| SonarQube Community (профиль `sonar`) | `sonarqube:26.9.0.129388-community` | `sha256:aa7146fd72ef79ea8ca06315ca3121db9bb1c24a83ba5255e8df67b43dee6e04` |
| Trivy (CI) | `aquasec/trivy:0.74.0` | — |
| Syft (CI) | `anchore/syft:v1.51.1` | — |
| cosign (CI, заготовка) | `ghcr.io/sigstore/cosign/cosign:v3.1.3` | — |
| Docker CLI (CI) | `docker:29.7.2-cli` | `sha256:3f4743208d2338c934d7b8bcfbe1bb54c0b2355c510ad5e0f31c0c4a54bd704e` |

Что важно знать при обновлении этой таблицы:

- Проверка «плавающий ли тег» — по **совпадению digest**, а не по дате: `postgres:18-alpine`
  и `postgres:18.6-alpine` дают один и тот же index-digest, значит мажорный тег двигается сам.
- Тег `latest` тянется при `pull` **всегда**, даже при `pull_policy: missing` — ещё один довод
  его не использовать.
- Образ `bitnami/cosign` больше не имеет версионных тегов (только `latest-metadata`) — заменён
  на `ghcr.io/sigstore/cosign/cosign`.

**Базовые образы .NET.** Сборка — `mcr.microsoft.com/dotnet/sdk:10.0.401-noble`, runtime — один из двух:

| Вариант | Плюсы | Минусы |
|---------|-------|--------|
| `aspnet:10.0.12-noble` | есть shell и пакетный менеджер, ставится `curl` → healthcheck в compose работает привычно | больше размер и CVE-поверхность |
| `aspnet:10.0.12-noble-chiseled-composite` | non-root по умолчанию, нет shell, фреймворк скомпилирован R2R → меньший образ и быстрый старт | нет shell/curl/`apt` → `HEALTHCHECK` только exec-формой, сложнее отладка |

**Рекомендация:** на MVP — `aspnet:10.0.12-noble` + `curl`; после стабилизации контура — переход на
`chiseled-composite` с exec-пробой. Экономия десятков мегабайт не стоит потери shell, пока инфраструктура
меняется ежедневно. Chiseled — не «дефолт Microsoft», а «drop-in replacement», если Dockerfile не зависит
от shell-скриптов ([ubuntu-chiseled](https://github.com/dotnet/dotnet-docker/blob/main/documentation/ubuntu-chiseled.md)).

**Кэш слоёв.** Restore/pnpm install — отдельным слоем, до копирования исходников; кэш пакетов — cache mount
([Optimize cache usage](https://docs.docker.com/build/cache/optimize/)): `target=/root/.nuget/packages`
и `target=/pnpm/store`. В CI кэш переиспользуется через registry-кэш buildx (`ci/templates/docker.yml`).

**HEALTHCHECK.** `curl` **не входит** в Linux-образы .NET ([Health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)):
ставим его в runtime-слой или берём другой инструмент. В chiseled/distroless нет shell, curl и wget —
остаётся exec-форма:

```dockerfile
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
  CMD ["/app/MarketSniper.Web", "--healthcheck"]   # приложение само опрашивает /health/live, exit 0/1
```

Проба задаётся **в одном месте** — сейчас в compose: `curl /health/live` у `backend`, `wget /healthz` у `frontend`.

**R2R и trimming.** `PublishReadyToRun` — только по замерам (размер растёт в 2–3 раза,
[ReadyToRun](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)). **Trimming запрещён**:
доступен только для self-contained и ломается на рефлексии EF Core
([trim-self-contained](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)).

**Dockerfile backend** (стадии `build` и `runtime`; CI собирает `--target runtime`):

```dockerfile
# syntax=docker/dockerfile:1.7
# Стадия сборки: SDK нужен только здесь
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build
ARG TARGETARCH
WORKDIR /src
# 1) restore отдельным слоем: кэш не рушится от правок исходников
COPY backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/**/*.csproj ./src-tmp/
RUN for f in $(find src-tmp -name '*.csproj'); do mkdir -p "src/${f#src-tmp/}"; mv "$f" "src/${f#src-tmp/}"; done && rm -rf src-tmp
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore --locked-mode -a $TARGETARCH
# 2) публикация без повторного restore
COPY backend/ ./
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/MarketSniper.Web/MarketSniper.Web.csproj -c Release -a $TARGETARCH --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
# curl нужен только для healthcheck в compose (в базовом образе его нет)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 DOTNET_EnableDiagnostics=0 TZ=UTC
COPY --link --from=build /app .
USER $APP_UID                     # non-root: переменная определена в образах .NET 8+
EXPOSE 8080
LABEL org.opencontainers.image.title="MarketSniper backend"
ENTRYPOINT ["dotnet", "MarketSniper.Web.dll"]
```

**Dockerfile frontend** (стадии `build` и `runtime`; статику отдаёт Caddy внутри образа):

```dockerfile
# syntax=docker/dockerfile:1.7
# Стадия сборки: Node и pnpm нужны только здесь
FROM node:24.21.0-alpine AS build
ENV PNPM_HOME=/pnpm PATH=/pnpm:$PATH
RUN corepack enable
WORKDIR /repo
# 1) зависимости по lockfile — слой не инвалидируется правками кода
COPY frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml ./
COPY frontend/patches ./patches
RUN --mount=type=cache,target=/pnpm/store pnpm fetch
COPY frontend/ ./
RUN --mount=type=cache,target=/pnpm/store pnpm install -r --offline --frozen-lockfile
# 2) VITE_* вкомпилируются в бандл: только несекретная конфигурация
ARG VITE_API_BASE_URL=/api/v1
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN pnpm --filter web build

FROM caddy:2-alpine AS runtime
COPY docker/frontend/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /repo/apps/web/dist /srv
USER caddy                       # non-root; слушаем 8080, не 80
EXPOSE 8080
LABEL org.opencontainers.image.title="MarketSniper frontend"
HEALTHCHECK --interval=15s --timeout=3s --retries=5 \
  CMD ["wget", "-qO-", "http://127.0.0.1:8080/healthz"]   # busybox wget есть в alpine
```

Caddy внутри frontend-образа отвечает за SPA-fallback и кэш статики (`/api/*` сюда не доходит — его
забирает proxy):

```caddyfile
:8080 {
	encode zstd br gzip
	handle /healthz { respond "ok" 200 }
	handle { root * /srv; try_files {path} /index.html; file_server }
	@hashed path /assets/*
	header @hashed Cache-Control "public, max-age=31536000, immutable"
	route { try_files {path} /index.html
	        header /index.html Cache-Control "public, max-age=0, must-revalidate" }
}
```

**`VITE_*` — конфигурация времени сборки.** Vite статически заменяет `import.meta.env.VITE_*` в бандле,
поэтому секретов там быть не может ([Vite env](https://vite.dev/guide/env-and-mode)). Три практики:
вкомпилировать при сборке (образ на каждую среду — **наш выбор на MVP**); подставлять на старте через
`window.env` + `envsubst` (`vite-plugin-runtime-env` → один образ на все среды); отдавать `/config.json`
и читать при старте. Значения по умолчанию — относительные (`/api/v1`).

## Compose

**Split-файлы.** `docker/compose/compose.yaml` — точка входа с `include:` остальных файлов: `infra`
(всегда), `app`, `tools`. `include` выбран потому, что каждый файл грузится со своим project directory —
относительные пути не «плывут», а зависимости остаются явными
([Include](https://docs.docker.com/compose/how-tos/multiple-compose-files/include/)).
`compose.override.yaml` — только локальные правки, в git не попадает.
**Профили.** Сервис без `profiles` включается всегда — это `postgres` и `redis`, ядро окружения.
Профили проекта: `app`, `proxy`, `observability`, `seq`, `tools`, `sonar`; запуск нескольких —
`--profile app --profile observability` или `COMPOSE_PROFILES=app,observability`
([profiles](https://docs.docker.com/reference/compose-file/profiles/)).
**Порядок старта.** Compose ждёт только «контейнер запущен», поэтому зависимости задаются явно:
`depends_on: {postgres: {condition: service_healthy}}`, а у БД и Redis есть `healthcheck`
(`pg_isready`, `redis-cli ping`) — [Control startup order](https://docs.docker.com/compose/how-tos/startup-order/).
**Реплики и порты.** `container_name` **запрещает** масштабирование сервиса — поэтому у `backend` и
`frontend` его нет (он оставлен у неизменяемых сервисов: БД, инструменты). Второе ограничение —
фиксированный порт `HOST:CONTAINER`: для реплик либо убираем `ports` (трафик идёт через `proxy`), либо
задаём диапазон `"8080-8089:8080"` ([ports](https://docs.docker.com/reference/compose-file/services/#ports)).
Команда: `docker compose --profile app up -d --scale backend=3`.
**Секреты.** `ARG`/`ENV` в Dockerfile секретов не содержат: они видны в `docker history` и
`docker inspect`. Локально — `.env` (шаблон `.env.example`, сам файл не коммитится); в prod — Compose
`secrets:` с монтированием в `/run/secrets/<name>`, для PostgreSQL — `POSTGRES_PASSWORD_FILE`
([Secrets in Compose](https://docs.docker.com/compose/how-tos/use-secrets/)). В .NET файловый секрет
читается как `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)`.
**Watch.** `develop.watch` требует в образе `stat`/`mkdir`/`rmdir` и прав записи в target (каталог
копируется с `COPY --chown`): `sync` — исходники, `rebuild` — манифесты пакетов ([Compose Develop](https://docs.docker.com/reference/compose-file/develop/)).

## Reverse-proxy

Caddy выбран потому, что это один статический бинарь, конфиг — десять строк, а `encode zstd br gzip`
(brotli) и проксирование WebSocket работают без сторонних модулей (в официальном nginx brotli нет).
Маршруты (`docker/proxy/Caddyfile`):

| Путь | Куда | Комментарий |
|------|------|-------------|
| `/api/*` | `backend:8080` | Таймауты увеличены под внешние вызовы маркетплейсов |
| `/scalar/*`, `/openapi/*` | `backend:8080` | Документация API и схема |
| `/health/*` | `backend:8080` | Пробы для внешнего мониторинга |
| всё остальное | `frontend:8080` | SPA; fallback на `index.html` делает Caddy внутри frontend-образа |

SSE требует отключённого буферинга (`flush_interval -1`), WebSocket работает без настройки. TLS в prod —
автоматический (Let's Encrypt) плюс заголовки безопасности (HSTS, `X-Content-Type-Options`, `X-Frame-Options`).

## Наблюдаемость

**Единый транспорт — OTLP.** Приложение не знает адресата: меняется только `OTEL_EXPORTER_OTLP_ENDPOINT`;
остальное (`OTEL_SERVICE_NAME`, `OTEL_RESOURCE_ATTRIBUTES`, протокол) — переменные compose
([OTLP exporter config](https://opentelemetry.io/docs/languages/sdk-configuration/otlp-exporter/)). Логи
идут тем же транспортом, `TraceId`/`SpanId` берутся из `Activity.Current` — лог связан с трейсом.

| Контур | Что смотрим | Почему |
|--------|-------------|--------|
| Dev | **Aspire Dashboard** (профиль `observability`) | логи + трейсы + метрики в одном UI, MIT, без настройки; данные в памяти, без аутентификации → только localhost |
| Dev, опционально | Seq (профиль `seq`) | удобный поиск по логам; **лицензия проприетарная** — `docs/adr/0010-observability-otlp.md` |
| Prod-кандидат | Grafana + Loki + Tempo | open source (AGPLv3), свой OTLP-приём; подключается сменой endpoint'а |

**Health-checks.** `/health/live` — процесс жив (зависимости не проверяются, иначе рестарт по кругу);
`/health/ready` — БД и Redis отвечают. Тяжёлые запросы в пробах запрещены (`SELECT 1` — максимум),
таймаут проверки — единицы секунд.

## Windows + WSL2

- Исходники — **в Linux-файловой системе** (`~/projects/...`), не в `/mnt/c/...`: bind mount из Linux-ФС
  быстрее, а inotify-события приходят только для файлов внутри WSL
  ([WSL 2 best practices](https://docs.docker.com/desktop/features/wsl/best-practices/)).
- HMR: при правках файлов Windows-процессом Vite не увидит изменений — либо редактор внутри WSL, либо
  `server.watch: { usePolling: true }` (дорого по CPU) — [server.watch](https://vite.dev/config/server-options#server-watch).
- `dotnet watch` в контейнере требует `DOTNET_USE_POLLING_FILE_WATCHER=1` — [dotnet watch](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch).
  Быстрее держать backend нативно, а в контейнерах — только инфраструктуру; полный цикл — перед пушем.
- Synchronized file shares в Compose требуют платной подписки — на бесплатном тарифе bind mounts медленнее.

## Чек-лист «контейнер готов»

- [ ] Multi-stage: SDK/Node есть только в build-стадии; runtime — non-root (`USER $APP_UID` / `USER caddy`).
- [ ] `.dockerignore` исключает `bin/obj/node_modules/dist/.env*`; кэш пакетов — cache mount;
      версия образа зафиксирована **точно** (без `latest`, плавающих тегов и образов без тега).
- [ ] `healthcheck` есть (Dockerfile **или** compose — не оба), зависимости — `condition: service_healthy`.
- [ ] Секреты приходят из `.env`/`secrets:`, не через `ARG`/`ENV`.
- [ ] Порт наружу публикует только proxy; сервис масштабируется без конфликта портов.
- [ ] Сервис виден в `docker compose ps` как `healthy`, логи идут в stdout.
