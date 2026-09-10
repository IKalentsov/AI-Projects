# ci — шаблоны пайплайна (GitLab CI)

Точка входа — `.gitlab-ci.yml` в корне. Здесь лежат переиспользуемые шаблоны,
подключаемые через `include: local`.

## Файлы

| Файл | Отвечает за |
|------|-------------|
| `templates/dotnet.yml` | сборка, форматирование, тесты и публикация бэкенда |
| `templates/frontend.yml` | линт, типы, тесты, сборка и e2e фронтенда |
| `templates/docker.yml` | сборка и публикация образов (buildx + registry-кэш) |
| `templates/security.yml` | SCA (Trivy), SBOM (Syft), SonarQube по расписанию, заготовка подписи (cosign) |

## Стадии

```
build → test → security → package → deploy
```

| Джоб | Стадия | Когда запускается |
|------|--------|-------------------|
| `dotnet:build` | build | изменения в `backend/**` |
| `dotnet:format` | test | после сборки |
| `dotnet:test` | test | после сборки (unit + integration + architecture) |
| `dotnet:publish` | package | основная ветка и теги |
| `frontend:quality` | test | изменения в `frontend/**` |
| `frontend:test` | test | изменения в `frontend/**` |
| `frontend:build` | build | изменения в `frontend/**` |
| `frontend:e2e` | test | MR и основная ветка |
| `security:dependencies` | security | изменения в lock-файлах |
| `security:sbom` | security | основная ветка и теги |
| `sonar:analysis` | security | **по расписанию** и только при заданных `SONAR_HOST_URL` + `SONAR_TOKEN`; MR не блокирует |
| `docker:backend`, `docker:frontend` | package | изменения в `docker/**` или коде |
| `container_scanning` (шаблон GitLab) | security | после сборки образа |
| `semgrep-sast`, `secret_detection` (шаблоны GitLab) | security | MR и основная ветка |
| `deploy:migrate` | deploy | вручную, при заданном `DEPLOY_HOST` (до выкатки приложения) |
| `deploy:staging` | deploy | вручную из основной ветки |

## Ключевые правила

1. **`compare_to` обязателен** во всех `rules:changes`. Без него на новой ветке
   сравнение идёт с пустотой, правило всегда истинно — и запускается всё сразу.
2. **`exists` — второй фильтр**: пока `backend/MarketSniper.slnx` или
   `frontend/pnpm-lock.yaml` не созданы, джобы просто не появляются в пайплайне.
   Это позволяет закоммитить инфраструктуру раньше кода и не иметь красный пайплайн.
3. **Кэш** — по ключевым файлам (`Directory.Packages.props`, `pnpm-lock.yaml`).
   Protected- и unprotected-ветки кэш не разделяют: учитывать при отладке.
4. **Артефакты отчётов публикуются всегда** (`when: always`), иначе при падении
   тестов не будет ни TRX, ни coverage, ни скриншотов Playwright.
5. **Тесты .NET** запускаются в режиме Microsoft.Testing.Platform
   (см. `backend/global.json`, скилл `testing-strategy`). `Microsoft.NET.Test.Sdk`
   не используется.
6. **Docker-сборка** — buildx + DinD с кэшем в registry. Kaniko не используем
   (проект архивирован). Альтернатива без privileged — rootless BuildKit/Buildah.
7. **Сканирование образов** — `--ignore-unfixed`, иначе неисправимые CVE базовых
   образов будут вечно валить пайплайн.

## Что потребует Ultimate (и чем заменяем)

| Функция | Замена в Free |
|---------|---------------|
| Dependency Scanning | `security:dependencies` (Trivy fs) |
| DAST | ZAP baseline-скан вручную/по расписанию (позже) |
| License Scanning | проверка лицензий вручную при добавлении пакета (правило в `AGENTS.md`) |
| MR-виджеты безопасности | артефакты SARIF + отчёт в задаче |

## Обновление зависимостей

Для своевременного обновления NuGet/npm/Docker-образов подключаем **Renovate**
(open source, ставится как scheduled pipeline или self-hosted).
Dependabot в GitLab требует зеркалирования репозитория, поэтому не используется.

## Локальная проверка перед пушем

```bash
# backend
cd backend && dotnet build -c Release && dotnet test --solution MarketSniper.slnx -c Release

# frontend
cd frontend && pnpm lint && pnpm typecheck && pnpm test && pnpm build
```

Правила пайплайна и его развитие — `docs/architecture/ci-cd.md`.
