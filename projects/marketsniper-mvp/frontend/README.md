# frontend — карта фронтенда

**Последнее обновление:** 2026-09-10

Правила работы с фронтендом — `frontend/AGENTS.md`. Стек и версии — `docs/frontend/stack.md`.
Общие правила проекта (роли, цикл задачи, DoD) — корневой `AGENTS.md`.

Код появится отдельной задачей; сейчас в репозитории только правила и карта.

## Что где будет лежать

```
frontend/
├── apps/
│   ├── web/                  # SPA: поиск, результаты, карточка предложения, кабинет
│   └── landing/              # лендинг (отдельное приложение, SSG/SSR) — позже
├── packages/
│   ├── ui/                   # дизайн-система: компоненты shadcn/ui, токены, globals.css
│   ├── api-client/           # сгенерированный из OpenAPI клиент + query-хуки
│   ├── shared/               # типы, утилиты, константы, env-доступ
│   ├── eslint-config/        # общий eslint.config.mjs
│   └── tsconfig/             # tsconfig.base.json и пресеты
├── pnpm-workspace.yaml       # список пакетов монорепо
└── package.json              # корневые скрипты, версии инструментов
```

Подробное дерево, слои и правила импортов — `docs/frontend/project-structure.md`.

## Документация

| Документ | О чём |
|----------|-------|
| `docs/frontend/stack.md` | Источник правды по версиям; обоснование выбора; риски |
| `docs/frontend/project-structure.md` | Структура, слои, правила размещения кода |
| `docs/frontend/data-and-state.md` | TanStack Query, Zustand, nuqs, формы, ошибки |
| `docs/frontend/testing.md` | Vitest, RTL, MSW, Playwright, покрытие |
| `docs/frontend/quality.md` | ESLint, tsconfig, lefthook, a11y, производительность |
| `docs/adr/0003-frontend-react-spa.md` | Почему Vite + React SPA, а не Next.js |
| `docs/adr/0004-ui-tailwind-shadcn.md` | Почему Tailwind + shadcn/ui |

## Команды (целевые)

```bash
cd frontend
pnpm install
pnpm dev            # dev-сервер apps/web
pnpm lint           # ESLint
pnpm typecheck      # tsc --noEmit
pnpm test           # Vitest: unit + component + integration
pnpm test:coverage  # покрытие
pnpm test:e2e       # Playwright
pnpm build          # сборка
pnpm api:generate   # регенерация клиента из OpenAPI
```

Требования к окружению: Node.js 24 LTS, pnpm 10 (версия фиксируется полем
`packageManager` в `package.json`).

## Связанные документы

- Инфраструктура и контейнеры фронтенда — `docker/README.md`.
- Предметная область и сценарии — `docs/domain/overview.md`.
- Правила постановки задач исполнителю — `ai-tasks/README.md`.
