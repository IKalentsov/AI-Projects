# Фронтенд: структура проекта

**Последнее обновление:** 2026-09-10

Правила размещения кода во фронтенд-монорепо. Версии — `docs/frontend/stack.md`,
общие правила фронтенда — `frontend/AGENTS.md`, правила работы в репозитории — корневой `AGENTS.md`.

## 1. Дерево монорепо

```
frontend/
├── apps/
│   ├── web/                                   # SPA-приложение
│   │   ├── src/
│   │   │   ├── app/                           # инициализация: провайдеры, роутер, ErrorBoundary
│   │   │   ├── routes/                        # маршруты (тонкий слой композиции)
│   │   │   ├── features/                      # бизнес-возможности (изолированы)
│   │   │   ├── entities/                      # доменные сущности (переиспользуемые)
│   │   │   └── shared/                        # UI-кит, утилиты, конфиг, тестовые утилиты
│   │   ├── e2e/                               # Playwright-тесты
│   │   ├── index.html
│   │   ├── vite.config.ts
│   │   ├── tsconfig.json
│   │   └── package.json
│   └── landing/                               # лендинг — появится отдельным приложением (SSG/SSR)
├── packages/
│   ├── ui/                                    # дизайн-система: компоненты shadcn/ui, токены, globals.css
│   │   ├── src/components/                    # button.tsx, input.tsx, card.tsx, ...
│   │   ├── src/styles/globals.css             # @import "tailwindcss"; @theme {...}; :root/.dark
│   │   ├── components.json                    # конфиг shadcn CLI
│   │   └── package.json
│   ├── api-client/                            # клиент из OpenAPI
│   │   ├── src/generated/                     # сгенерированные типы, клиент, хуки, MSW-хендлеры
│   │   ├── src/http/                          # базовый fetch-клиент, интерсепторы, обработка ProblemDetails
│   │   ├── orval.config.ts
│   │   └── package.json
│   ├── shared/                                # типы, утилиты, константы, env
│   │   ├── src/lib/
│   │   ├── src/config/
│   │   └── src/types/
│   ├── eslint-config/                         # eslint.config.mjs и зоны импортов
│   └── tsconfig/                              # tsconfig.base.json и пресеты
├── pnpm-workspace.yaml
├── lefthook.yml
└── package.json
```

Правило: в `apps/*` живёт только то, что специфично приложению; всё переиспользуемое —
в `packages/*`. Импорт из `packages/*` в `apps/*` — обычный; обратный (из пакета в приложение)
запрещён.

## 2. Слои внутри apps/web/src

| Слой | Что кладём | Чего не кладём |
|------|-----------|----------------|
| `app/` | Провайдеры (`QueryClientProvider`, тема, роутер), глобальные стили, `ErrorBoundary`, инициализация | Бизнес-логику, запросы к API |
| `routes/` | Файлы маршрутов: композиция фич и сущностей в экран, `loader`, метаданные страницы | Логику запросов, состояние, сложную вёрстку |
| `features/` | Одна бизнес-возможность: `api/`, `model/`, `ui/`, `lib/`, Public API | Код других фич, общие утилиты |
| `entities/` | Доменная сущность: карточка, модель, форматтеры, переиспользуемые хуки | Сценарии и экраны целиком |
| `shared/` | UI-кит, утилиты, конфиг, env, тестовые утилиты (`renderWithProviders`, MSW-сервер) | Знания о фичах и сущностях |

Правило «сверху вниз»: `routes` знает про `features` и `entities`; `features` — про `entities`
и `shared`; `entities` — про `shared`; `shared` не знает ни о ком. Нарушение ловится линтером (§3).

## 3. Границы: запрет cross-feature импортов

Запрещено:

- импорт из `features/a` в `features/b` — композиция только в `routes/`;
- импорт из `routes/` внутрь `features/`, `entities/`, `shared/`;
- импорт из `shared/` в `features/` или `entities/`;
- относительные «прыжки» через слои (`../../../features/...`) — используем алиасы (`@/`, `@shared/`).

Ловится правилом `import-x/no-restricted-paths` (пакет `eslint-plugin-import-x`)
в `packages/eslint-config/eslint.config.mjs`. Внимание: префикс правила — `import-x/`,
а не `import/` (иначе правило молча не сработает).

```js
// packages/eslint-config/eslint.config.mjs (фрагмент: границы слоёв)
'import-x/no-restricted-paths': ['error', {
  basePath: './apps/web/src',
  zones: [
    // фичи не знают друг о друге
    { target: './features/offer-search', from: './features', except: ['./offer-search'] },
    { target: './features/offer-filters', from: './features', except: ['./offer-filters'] },
    { target: './features/saved-offers', from: './features', except: ['./saved-offers'] },
    // shared не зависит от верхних слоёв
    { target: './shared', from: './features' },
    { target: './shared', from: './entities' },
    { target: './shared', from: './routes' },
    // entities не зависят от фич и маршрутов
    { target: './entities', from: './features' },
    { target: './entities', from: './routes' },
    // фичи не тянут код маршрутов
    { target: './features', from: './routes' },
  ],
}],
```

Новую фичу добавляем в зоны в том же MR: без этого правило её не защитит.

## 4. Пример структуры фичи

```
features/offer-search/
├── api/
│   ├── offer-keys.ts            # фабрика ключей запросов
│   ├── use-offer-search.ts       # хук поиска (обёртка над сгенерированным хуком)
│   └── use-save-offer.ts         # мутация «сохранить предложение»
├── model/
│   ├── search-params.ts          # схема zod для URL-параметров поиска (nuqs)
│   └── map-offer.ts              # маппинг DTO → модель экрана
├── ui/
│   ├── offer-search-form.tsx     # форма запроса
│   ├── offer-list.tsx            # список результатов
│   └── offer-card.tsx            # карточка предложения
├── lib/
│   └── format-price.ts           # локальные утилиты фичи
└── index.ts                      # Public API фичи: только то, что нужно routes/
```

Правила:

- `index.ts` — **явные** экспорты (`export { OfferSearchForm } from './ui/offer-search-form'`),
  без `export *`; всё, что не экспортировано, считается внутренним.
- Компонент получает данные через хук фичи; внутри `ui/` нет вызовов клиента API.
- Утилита, нужная двум фичам, переезжает в `shared/lib` (или в `entities/*`), а не импортируется
  из соседней фичи.
- Тесты лежат рядом с кодом: `offer-list.test.tsx` (компонент),
  `offer-search.integration.test.tsx` (фича целиком) — см. `docs/frontend/testing.md`.

## 5. Barrel-файлы

- Запрещены «сборные» `index.ts`, реэкспортирующие содержимое папки: мешают tree-shaking
  и скрывают реальные зависимости.
- Разрешено: `features/<name>/index.ts` и `entities/<name>/index.ts` как Public API слоя —
  только явные экспорты, только то, что реально используется снаружи.
- Импорт внутри фичи — всегда по конкретному файлу, не через её `index.ts`.

## 6. Именование

| Объект | Правило | Пример |
|--------|---------|--------|
| Папки | `kebab-case` | `offer-search`, `api-client` |
| Файлы компонентов | `kebab-case.tsx` | `offer-card.tsx` |
| Файлы хуков | `use-<что>.ts` | `use-offer-search.ts` |
| Файлы утилит/моделей | `kebab-case.ts` | `format-price.ts`, `search-params.ts` |
| Файлы тестов | `<имя>.test.ts(x)`, `<имя>.integration.test.tsx` | `offer-card.test.tsx` |
| Компоненты (React) | `PascalCase`, экспорт по имени | `export function OfferCard() {}` |
| Хуки | `useCamelCase` | `useOfferSearch` |
| Типы/интерфейсы | `PascalCase`, без префикса `I` | `OfferSearchParams` |
| Константы | `SCREAMING_SNAKE_CASE` | `MAX_QUERY_LENGTH` |
| События-пропсы | `on<Событие>` | `onSubmit`, `onOfferSelect` |

- Один компонент — один файл; несколько мелких внутренних подкомпонентов допустимы в одном
  файле, если они не экспортируются.
- Файл длиннее ~250 строк — сигнал разделить.
- Дефолтные экспорты запрещены, кроме случаев, требуемых инструментом (lazy-маршруты).

## 7. Алиасы импортов

```jsonc
// packages/tsconfig/tsconfig.base.json (фрагмент)
{
  "compilerOptions": {
    // baseUrl не используем: в TypeScript 7 он не работает (docs/frontend/stack.md, §4)
    "paths": {
      "@/*": ["./apps/web/src/*"],
      "@ui/*": ["./packages/ui/src/*"],
      "@api/*": ["./packages/api-client/src/*"],
      "@shared/*": ["./packages/shared/src/*"]
    }
  }
}
```

- В Vite 8 пути из `tsconfig` подхватываются опцией `resolve.tsconfigPaths: true`
  (см. [документацию Vite](https://vite.dev/config/shared-options#resolve-tsconfigpaths)).

## 8. Что появится вместе с кодом

- `apps/web/vite.config.ts`, `apps/web/tsconfig.json`, `orval.config.ts`,
  `packages/eslint-config/eslint.config.mjs`, `lefthook.yml`.
- `apps/landing/` — когда появится задача на лендинг; до этого момента папка не создаётся.
- `docker/frontend/Dockerfile` — сборка статики и раздача через прокси (`docker/README.md`).
