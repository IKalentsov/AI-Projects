# CI/CD: пайплайн монорепозитория

**Последнее обновление:** 2026-09-10
Решение — `docs/adr/0009-gitlab-ci.md`. Точка входа — `.gitlab-ci.yml`, состав джобов — `ci/README.md`
(здесь не дублируется). Тестовые уровни и артефакты — `docs/architecture/testing-strategy.md`.
Здесь — модель запуска, правила избирательности, качество, деплой.

## Модель ветвления и что запускается

Ветки: `main` (защищённая), `feature/<имя>`, `fix/<имя>`, `chore/<имя>`. Merge — только через MR
с зелёным пайплайном (см. корневой `AGENTS.md` §13).

| Событие | Пайплайн | Что запускается |
|---------|----------|-----------------|
| Пуш в `feature/*` без MR | branch pipeline | Ничего: `workflow.rules` не создаёт пайплайн для ветки без MR — экономия минут |
| MR в `main` | merge request pipeline | `build` → `test` → `security`: сборка, формат, тесты .NET и фронта, e2e, SAST, Secret Detection |
| Merge/пуш в `main` | branch pipeline | То же + `package` (образы в registry, SBOM) + Container Scanning; `deploy:staging` — вручную |
| Тег `v*` | tag pipeline | `package` (образы с тегом версии) + SBOM; деплой — по решению вручную |
| Ручной запуск (web) | branch pipeline | Полный набор, включая `security:dependencies` |

`workflow.auto_cancel.on_new_commit: interruptible` отменяет устаревшие прогоны; джобы деплоя
помечены `interruptible: false`, чтобы не оборвать выкладку на середине.

## Стадии

```
build → test → security → package → deploy
```

`build` и `test` дают быстрый сигнал по коду, `security` — сканеры, `package` — образы,
`deploy` — выкладка. Джобы связаны через `needs` (DAG): `dotnet:test` не ждёт фронтовых джоб,
`docker:backend` стартует сразу после успешной сборки. Перечень джобов, их стадии и условия —
`ci/README.md`; шаблоны — `ci/templates/*.yml`.

## Избирательность в монорепо: `compare_to` и `exists`

Монорепо означает, что один коммит может не касаться половины стека. Два обязательных правила:

1. **`rules:changes` всегда с `compare_to`.** Без него на новой ветке сравнение идёт «с пустотой»,
   правило считается истинным, и пайплайн прогоняет всё — сборку, тесты, сканеры — впустую.
   В MR сравниваем с `refs/heads/main`, в пайплайне основной ветки — с `HEAD~1`.
2. **`exists` — второй фильтр.** Пока `backend/MarketSniper.slnx` или `frontend/pnpm-lock.yaml`
   не созданы, джобы просто не появляются в пайплайне. Это позволяет коммитить инфраструктуру
   (compose, CI, docs) раньше кода и не иметь красный пайплайн «на пустом месте».

```yaml
rules:
  - if: $CI_PIPELINE_SOURCE == "merge_request_event"
    changes:
      compare_to: 'refs/heads/main'
      paths: ['backend/**/*', '.editorconfig']
    exists: ['backend/MarketSniper.slnx']
  - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
    changes:
      compare_to: 'HEAD~1'
      paths: ['backend/**/*', '.editorconfig']
    exists: ['backend/MarketSniper.slnx']
```

Матричные джобы (например, будущий прогон тестов по слоям) могут подставлять переменную в путь:
`changes: [ 'backend/src/$PROJECT/**/*' ]` — `parallel:matrix` официально поддерживается в
`rules:if/changes/exists` ([job_control](https://docs.gitlab.com/ci/jobs/job_control/)).

## Кэш и артефакты

| Что | Ключ | Где | Срок |
|-----|------|-----|------|
| NuGet | `backend/Directory.Packages.props` | `.nuget/packages` | до смены версий пакетов |
| pnpm store | `frontend/pnpm-lock.yaml` | `.pnpm-store` | до смены lockfile |
| База Trivy | `trivy-db` | `.trivycache/` | до инвалидации |

Ограничения GitLab: ключ по `cache:key:files` — максимум два файла; на джоб — максимум четыре кэша;
protected- и unprotected-ветки кэш **не разделяют** ([Caching](https://docs.gitlab.com/ci/caching/)).
Кэш — оптимизация, а не гарантия: джоб обязан работать и на пустом кэше.

Артефакты публикуются **всегда** (`when: always`), иначе при падении тестов не будет отчётов:
TRX и cobertura (backend), `coverage/` (frontend), `playwright-report/` и `test-results/` (e2e),
SARIF (Trivy), `sbom.cdx.json` / `sbom.spdx.json`. Отчёты дополнительно отдаются в UI GitLab через
`reports: junit` и `reports: coverage_report` — [Artifacts reports](https://docs.gitlab.com/ci/yaml/artifacts_reports/).
Сроки: отчёты тестов — 1 неделя, SBOM — 4 недели, бинарные артефакты сборки — 1 день.

## Сборка образов

Схема: `docker buildx` + Docker-in-Docker (`docker:dind`) с кэшем слоёв в registry
(`--cache-from/--cache-to type=registry,...,mode=max`) — `ci/templates/docker.yml`. DinD требует
`privileged` на раннере и `DOCKER_TLS_CERTDIR: "/certs"`
([DinD](https://docs.gitlab.com/ci/docker/docker_in_docker/)).

**Kaniko не используем** — проект архивирован («no longer developed or maintained»,
[GoogleContainerTools/kaniko](https://github.com/GoogleContainerTools/kaniko)) и из официальной
документации GitLab исключён. Если privileged запрещён политикой: **rootless BuildKit**
(`buildctl-daemonless.sh`) — GitLab называет его прямой заменой Kaniko, либо **rootless Buildah**
([BuildKit](https://docs.gitlab.com/ci/docker/using_buildkit/),
[Buildah](https://docs.gitlab.com/ci/docker/buildah_rootless_multi_arch/)). Переключение локально:
меняется только джоб `docker:*`, Dockerfile'ы остаются те же.

Сборка публикует только **неизменяемый тег** `:$CI_COMMIT_SHA`. Тег `latest` не публикуется
вообще: он неотличим по смыслу от «какая-то версия» и провоцирует случайное обновление
(`docs/adr/0012-image-version-pinning.md`).

## Качество и безопасность

| Проверка | Инструмент | Тир | Блокирует merge |
|----------|-----------|-----|-----------------|
| Компиляция + анализаторы .NET | `dotnet build` (`TreatWarningsAsErrors`) | — | да |
| Формат | `dotnet format --verify-no-changes` | — | да |
| Линт и типы фронта | ESLint, `tsc --noEmit` (`pnpm typecheck`) | — | да |
| Тесты (unit/integration/architecture/e2e) | xunit.v3 (MTP), Vitest, Playwright | — | да |
| SAST | GitLab `semgrep-sast` | Free | предупреждение |
| Секреты | GitLab `secret_detection` | Free | предупреждение |
| Уязвимости образа | GitLab `container_scanning` (Trivy) | Free | предупреждение |
| Зависимости (.NET/npm) | `security:dependencies` (Trivy fs) | — | да, на HIGH/CRITICAL |
| Статанализ | SonarQube Community Build | self-hosted | нет — scheduled-джоба `sonar:analysis` |

**Что Free не даёт.** Встроенный Dependency Scanning, DAST, License Scanning и secret push
protection — только Ultimate; SAST/Container Scanning/Secret Detection в Free работают, но **без**
виджетов в MR, Security-таблицы и управления уязвимостями (они в Ultimate). Поэтому SCA закрываем
Trivy (`--ignore-unfixed`, `--exit-code 1` только на HIGH/CRITICAL), а отчёты складываем
артефактами SARIF.

**SonarQube.** В Community Build поддержан C# (анализатор Roslyn), но **branch/PR-анализ и quality
gate на MR — только в платных редакциях (Developer+)**, monorepo-интеграция — Enterprise+. То есть
локальный SonarQube даёт анализ основной ветки и метрики, но не «гейт на MR»; роль гейта выполняют
тесты, анализаторы и `dotnet format`.

**Принятое решение по SonarQube в пайплайне:** MR-пайплайн им **не блокируется**. Анализ вынесен
в scheduled-джобу `sonar:analysis` (`ci/templates/security.yml`): она запускается по расписанию
и только если в настройках проекта заданы `SONAR_HOST_URL` и `SONAR_TOKEN`; падение не влияет
на merge. Пересмотр — при появлении платной редакции SonarQube (сейчас не планируется).

**SBOM и подпись.** SBOM (`syft`, CycloneDX + SPDX) генерируется на `main` и тегах — он нужен для
разбора инцидентов и запросов «что в образе». Подпись `cosign` (job `security:sign`, сейчас
`when: never`) остаётся оверинжинирингом до появления контура, который **проверяет** подпись
(admission-контроллер или шаг `cosign verify` перед `up -d` на сервере). Включать — вместе с
верификацией, а не раньше.

## Деплой

Целевая схема (без Kubernetes — масштаб MVP):

```
MR → тесты → образы в registry → deploy:staging (вручную) → сервер:
  docker compose pull && docker compose up -d --remove-orphans
```

| Окружение | Как обновляется | Откат |
|-----------|-----------------|-------|
| staging | `deploy:staging` вручную из `main`, тег `sha` | вернуть предыдущий `sha` в `.env` и `up -d` |
| prod | по тегу версии, вручную, после проверки staging | тег предыдущей версии + `up -d` |

Откат образов — мгновенный, потому что образы неизменяемы и лежат в registry; откат **миграций** —
нет: схема меняется вперёд, поэтому опасные изменения делаются в два шага (сначала совместимое
со старой версией приложения, потом удаление старого).

**Миграции БД — принятое решение.** Автоприменение миграций на старте приложения при нескольких
репликах даёт гонку, поэтому способ фиксируется по контурам:

| Контур | Способ | Почему так |
|--------|--------|-----------|
| prod и staging | **отдельный джоб пайплайна** `deploy:migrate` (тот же образ, команда `--migrate`) — выполняется до `up -d` | виден в пайплайне, отдельный лог, деплой можно прервать до старта приложения, не зависит от версии Compose |
| локальная разработка | `pre_start:` с `per_replica: false` (Compose v5.3+) либо `dotnet ef database update` вручную | быстрый цикл, реплик нет |

Один способ на контур — **не оба одновременно**. Автоприменение на старте приложения допускается
только при одиночном локальном запуске.

```yaml
# Локальный контур: миграции один раз до старта сервиса (не на каждую реплику)
backend:
  pre_start:
    - command: ["dotnet", "MarketSniper.Web.dll", "--migrate"]
      per_replica: false
```

**Правила изменения схемы.** Опасные изменения делаются в два шага: сначала совместимое со старой
версией приложения изменение, затем удаление старого. Откат миграций не выполняется.

**Бэкапы.** Параметры ниже — значения по умолчанию; утвердить до первого прод-деплоя:

| Параметр | Значение |
|----------|----------|
| Команда | `pg_dump -Fc` (custom format, восстанавливается `pg_restore`) |
| Частота | ежедневно по расписанию + перед каждой выкладкой с изменением схемы |
| Retention | 14 ежедневных + 6 месячных копий |
| Хранение | вне сервера приложения (S3-совместимое хранилище или отдельный хост) |
| Проверка | ежемесячно: восстановление на staging в отдельную БД + прогон smoke-сценария |

Тома БД резервной копией не являются: они лежат на том же хосте и умирают вместе с ним.

## Обновление зависимостей

**Renovate** (open source, self-hosted или как scheduled pipeline): один конфиг на монорепо —
NuGet (`backend/Directory.Packages.props`), npm (pnpm-workspace), Docker (образы в compose и
Dockerfile), обновления GitLab CI. Группировка: минорные и патч-версии — одним MR, мажорные —
отдельно. Dependabot в GitLab требует зеркалирования репозитория и потому не используется.
Правило: обновление базовых образов — плановая работа, а не «когда вспомним»; именно она закрывает
большую часть CVE без ручного вмешательства.

## Чек-лист «пайплайн здоров»

- [ ] MR-пайплайн зелёный, branch-пайплайна-дублёра нет (проверить `workflow.rules`).
- [ ] Джобы не запускаются «на всё»: в логе видно сработавший `compare_to`.
- [ ] При падении тестов артефакты (TRX, coverage, отчёт e2e) доступны.
- [ ] Кэш не маскирует проблему: прогон с очищенным кэшем проходит.
- [ ] Сканирование не «вечно красное»: `--ignore-unfixed`, пороги HIGH/CRITICAL.
- [ ] Образы в registry помечены `sha`; тег `latest` не публикуется и в деплой не попадает.
- [ ] Секреты — только в CI-переменных (masked/protected) и `.env`, не в логах и не в git.
- [ ] Деплой не прерывается (не `interruptible`) и имеет план отката.
