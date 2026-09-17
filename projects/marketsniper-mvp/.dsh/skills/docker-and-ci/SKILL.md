---
name: docker-and-ci
description: Правила изменения контейнеров, compose-файлов и пайплайна GitLab CI в монорепозитории MarketSniperMVP.
whenToUse: При правке Dockerfile'ов, compose-файлов, Caddyfile, .gitlab-ci.yml или ci/templates/*.yml.
---

# Docker и CI

Скилл описывает **порядок действий и ограничения** при изменении контейнерного контура и пайплайна.
Справочная информация — `docs/architecture/infrastructure-docker.md` и `docs/architecture/ci-cd.md`;
решения — `docs/adr/0006-container-first.md`, `0009-gitlab-ci.md`, `0010-observability-otlp.md`,
`0011-docker-images.md`.

## Порядок работы

1. **Прочитать существующее.** Минимум: `docker/README.md`, нужный compose-файл, Dockerfile,
   `.gitlab-ci.yml` и соответствующий `ci/templates/*.yml`. Правило проекта — не переписывать
   работающее «по-своему»: сначала понять, почему сделано так.
2. **Менять минимально.** Одна задача — один слой: либо образ, либо compose, либо пайплайн.
   Смешивать «переделаю заодно» запрещено: такие правки не ревьюятся.
3. **Проверить синтаксис до запуска.**
   ```bash
   docker compose -f docker/compose/compose.infra.yaml config -q
   docker compose -f docker/compose/compose.yaml --profile app config -q
   docker compose -f docker/compose/compose.yaml --env-file .env config   # посмотреть итог
   ```
   Для пайплайна — CI Lint в GitLab (Validate → Validate pipeline) или `glab ci lint`.
4. **Проверить локально.** Инфраструктура: `up -d`, затем `docker compose ps` — все `healthy`.
   Приложения: `--profile app --build`, затем `curl -fsS localhost/health/ready` через proxy.
   Для образа — `docker build --target runtime` отдельно, до полного `up`.
5. **Описать изменение.** Правило и его причина — в `docs/architecture/*.md`; новое архитектурное
   решение — отдельный ADR (`docs/adr/README.md`). Файлы документации принадлежат архитектору:
   исполнитель их не правит, а сообщает о необходимости правки.
6. **Проверить smoke.** Поднять контур и пройти базовый сценарий — скилл `smoke-test`.

## Правила (нарушение = возврат на доработку)

- **Секреты только через окружение.** `.env` (не в git, шаблон `.env.example`), CI-переменные
  (masked/protected), Compose `secrets:` → `/run/secrets/<name>`. В `ARG`/`ENV` Dockerfile,
  в compose-`environment` «намертво» и в `appsettings*.json` — нельзя.
- **Версии образов пинятся точно** (`postgres:18.6-alpine`, `redis:8.10.1-alpine`,
  `mcr.microsoft.com/dotnet/sdk:10.0.401-noble`). Запрещены `latest`, плавающие теги
  (`18-alpine`, `2-alpine`, `community`) и образы без тега — включая dev-инструменты и CI.
  Обновление — вручную и отдельным коммитом (`docs/adr/0012-image-version-pinning.md`).
  Актуальные пины с digest'ами — `docs/architecture/infrastructure-docker.md`, «Текущие пины».
- **Healthcheck обязателен** у каждого сервиса, который от него зависит по `depends_on`.
  Проба задаётся **в одном месте** (Dockerfile **или** compose), с `start_period`, покрывающим
  миграции и прогрев. Тяжёлых запросов в пробе нет: `SELECT 1` — максимум.
- **Non-root в runtime.** `USER $APP_UID` для .NET, `USER caddy` для Caddy, `COPY --chown` для
  файлов, в которые пишет watch. Запуск от root — только обоснованное исключение.
- **Порты наружу — только у proxy.** Backend и frontend доступны внутри `marketsniper-net`;
  публикация их портов — отладочная опция из `.env`. Это же условие масштабирования.
- **Multi-stage обязателен**: SDK/Node не попадают в runtime-образ.
- **Идемпотентность окружения**: любая настройка — в YAML или `.env`. Правки «руками в контейнере»
  (`docker exec`, установка пакетов) не считаются решением: после перезапуска их не будет.
- **`compare_to` во всех `rules:changes`** и `exists` там, где джоб зависит от появления файла.
- **Сканеры не должны быть вечно красными**: `--ignore-unfixed`, порог HIGH/CRITICAL, иначе
  пайплайн перестают читать.

## Типичные ошибки

| Ошибка | Чем плохо | Как правильно |
|--------|-----------|---------------|
| `image: myapp:latest` в деплое | невоспроизводимо, откат невозможен | точная версия или тег `sha`; `latest` запрещён везде |
| Плавающий тег `postgres:18-alpine` | `pull` незаметно подтянет другой минор | точная версия: `postgres:18.2-alpine` |
| Образ без тега (`image: postgres`) | это и есть `latest`: скачок мажора ломает данные | всегда указывать версию |
| Секрет в `ARG`/`ENV` Dockerfile | виден в `docker history` и `docker inspect` | Compose `secrets:` или переменная окружения на старте |
| `container_name` у масштабируемого сервиса | `--scale` падает: Compose не создаёт второй контейнер | убрать `container_name`, порт не публиковать |
| Фиксированный `ports: "8080:8080"` + реплики | конфликт портов на хосте | убрать `ports` (трафик через proxy) или диапазон `"8080-8089:8080"` |
| `rules:changes` без `compare_to` | на новой ветке правило всегда истинно → весь пайплайн впустую | `compare_to: 'refs/heads/main'`, в `main` — `HEAD~1` |
| `exists` не задан | джоб падает, пока нет решения/lockfile | `exists: ['backend/MarketSniper.slnx']` |
| Проба в Dockerfile **и** в compose | расходятся параметры, непонятно, что сработало | одно место |
| `curl` в chiseled/scratch-образе | команды нет — healthcheck вечно `unhealthy` | exec-проба приложения либо `aspnet:10.0-noble` + `curl` |
| `dotnet publish` с trimming | падение в рантайме на EF Core/рефлексии | R2R по замерам, trimming не использовать |
| Сборка образа внутри `test`-стадии | медленный сигнал, лишние минуты | образы — только в `package`, по изменениям `docker/**` |
| Сканирование без `--ignore-unfixed` | красный пайплайн из-за неисправимых CVE | `--ignore-unfixed` + HIGH/CRITICAL |
| Артефакты без `when: always` | при падении тестов нет TRX/coverage/отчёта e2e | `when: always` + `expire_in` |
| Правка файла, который «принадлежит» другому слою | смешанный дифф, невозможно ревьюить | одна задача — один слой: образ, либо compose, либо пайплайн |
| Новый каталог в репозитории без правки `changes` | джобы не запускаются для нового кода | добавить путь в `rules:changes` соответствующего шаблона |

## Как проверять пайплайн, не дожидаясь CI

```bash
# 1. Синтаксис и итоговая модель compose
docker compose -f docker/compose/compose.yaml --env-file .env config

# 2. Сборка конкретной стадии (быстрее, чем полный up)
docker build --file docker/backend/Dockerfile --target runtime -t marketsniper/backend:local .

# 3. Что реально попало в образ
docker history --no-trunc marketsniper/backend:local | head -20
docker run --rm --entrypoint sh marketsniper/backend:local -c 'id; ls /app | head'

# 4. YAML пайплайна (нужен glab или CI Lint в UI: Build → Pipeline editor → Validate)
glab ci lint
```

Правило: собирать образ локально **до** пуша, если менялись Dockerfile, базовый образ или
список зависимостей; CI — не место для первой сборки.

## Чек-лист самопроверки

- [ ] `docker compose config -q` проходит для infra и для полного `compose.yaml`.
- [ ] `docker compose ps` — все сервисы `healthy`; логи без ошибок старта.
- [ ] `curl -fsS localhost/health/ready` отвечает 200 через proxy.
- [ ] Изменённый образ собран `--target runtime`; в runtime нет SDK/Node.
- [ ] Процесс в контейнере — не root (`docker compose exec backend id`).
- [ ] Секретов нет в `git diff`, в `docker history` и в логах контейнера.
- [ ] Пайплайн на MR зелёный; артефакты отчётов доступны при падении.
- [ ] В `docs/architecture/*.md` отражено изменение; при архитектурном решении создан ADR.

## Границы ответственности

- Исполнитель меняет контейнерные и CI-файлы и **сообщает** о необходимой правке документации.
- Документацию (`docs/`, `ARCHITECTURE.md`) и ADR правит архитектор — см. корневой `AGENTS.md` §2.
- Изменение, затрагивающее топологию, порты, состав сервисов или стратегию деплоя, требует ADR
  до начала реализации, а не после.

## Связанные документы

| Документ | Что берём оттуда |
|----------|------------------|
| `docker/README.md` | команды запуска, состав compose-файлов, таблица портов |
| `docs/architecture/infrastructure-docker.md` | правила Dockerfile'ов, топология, наблюдаемость, WSL2 |
| `docs/architecture/ci-cd.md` | модель запуска, кэш и артефакты, деплой |
| `ci/README.md` | перечень джобов и стадий |
| `docs/adr/0006`, `0009`, `0010`, `0011` | принятые решения и их обоснование |
