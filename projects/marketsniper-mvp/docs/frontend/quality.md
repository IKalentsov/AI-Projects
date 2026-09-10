# Фронтенд: качество кода

**Последнее обновление:** 2026-09-10

Линтинг, типы, git-хуки, доступность и производительность. Версии — `docs/frontend/stack.md`,
правила кода — `frontend/AGENTS.md`. Требования DoD — корневой `AGENTS.md` (§9).

## 1. ESLint 10 (flat config)

Состав:

- `@eslint/js` — базовый `recommended`;
- `typescript-eslint` 8.70 — `recommendedTypeChecked` + `stylisticTypeChecked`,
  типизированный линтинг через `parserOptions.projectService: true`
  ([typed linting](https://typescript-eslint.io/getting-started/typed-linting));
- `eslint-plugin-react-hooks` — `configs.flat.recommended` (включает правила React
  и правила компилятора: `set-state-in-render`, `set-state-in-effect`, `refs`);
- `eslint-plugin-jsx-a11y` — доступность в JSX (уровень `error` для ключевых правил);
- `eslint-plugin-import-x` — `no-restricted-paths` (границы слоёв и запрет cross-feature импортов);
- `eslint-config-prettier` — отключает конфликтующие с Prettier правила форматирования.

```js
// packages/eslint-config/eslint.config.mjs
import js from '@eslint/js';
import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import prettier from 'eslint-config-prettier';

export default defineConfig(
  { ignores: ['**/dist/**', '**/coverage/**', 'packages/api-client/src/generated/**'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommendedTypeChecked,
      tseslint.configs.stylisticTypeChecked,
      reactHooks.configs.flat.recommended,
      jsxA11y.flatConfigs.recommended,
      prettier,
    ],
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
    },
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unsafe-assignment': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-floating-promises': 'error',
      '@typescript-eslint/no-misused-promises': 'error',
      '@typescript-eslint/switch-exhaustiveness-check': 'error',
      '@typescript-eslint/ban-ts-comment': ['error', { 'ts-expect-error': 'allow-with-description' }],
      'no-console': ['warn', { allow: ['warn', 'error'] }],
      'import-x/no-default-export': 'error',
    },
  },
  // зоны импортов (границы слоёв) — см. docs/frontend/project-structure.md §3
  { files: ['apps/web/src/**/*.{ts,tsx}'], rules: { 'import-x/no-restricted-paths': ['error', zones] } },
);
```

Исключения (ослабление правил) допустимы только точечно и с комментарием: для
`*.config.ts`, `vite.config.ts`, `orval.config.ts` — `no-default-export` отключён.

## 2. Запрещённые конструкции

| Запрещено | Почему | Чем заменить |
|-----------|--------|--------------|
| `any`, `as unknown as`, `as any` | Отключает проверку типов | `unknown` + zod-парсер, дженерик, точный тип |
| `@ts-ignore` | Скрывает ошибку | `@ts-expect-error` с описанием (и только для багов сторонних типов) |
| `eslint-disable` без указания правила и причины | Маскирует проблему | Исправить код; в исключительных случаях — `// eslint-disable-next-line <rule> -- причина` |
| Непроверенный `JSON.parse` | Тип `any` в рантайме | zod-схема ответа |
| `useEffect` для загрузки данных | Дублирует TanStack Query | `useQuery` в хуке фичи |
| `index` как `key` в списках с изменяемым порядком | Ломает состояние элементов | Стабильный id сущности |

## 3. TypeScript

Базовый конфиг (`packages/tsconfig/tsconfig.base.json`):

```jsonc
{
  "compilerOptions": {
    "target": "ES2023",
    "lib": ["ES2023", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "moduleDetection": "force",
    "jsx": "react-jsx",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "noImplicitOverride": true,
    "noFallthroughCasesInSwitch": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "verbatimModuleSyntax": true,
    "isolatedModules": true,
    "skipLibCheck": true,
    "noEmit": true,
    "paths": {
      "@/*": ["./apps/web/src/*"],
      "@ui/*": ["./packages/ui/src/*"],
      "@api/*": ["./packages/api-client/src/*"],
      "@shared/*": ["./packages/shared/src/*"]
    }
  }
}
```

- `noUncheckedIndexedAccess` — главный практический выигрыш: `array[0]` имеет тип `T | undefined`.
- `exactOptionalPropertyTypes` — нельзя передать `undefined` туда, где поле необязательное.
- `baseUrl` не используем (в TypeScript 7 не работает) — см. `docs/frontend/stack.md`, §4.
- Версия TypeScript пинится (`6.0.x`); быстрая проверка `tsgo` — только как отдельная команда CI.

## 4. Prettier и форматирование

- Prettier 3 — единственный источник форматирования: `pnpm format` / `pnpm format:check`.
- Спорные стилевые правила ESLint отключены (`eslint-config-prettier`).
- Основные настройки: `printWidth: 100`, `singleQuote: true`, `trailingComma: "all"`,
  `semi: true` — фиксируются в `.prettierrc`.
- Ручное выравнивание и «красивые» отступы в обход Prettier — запрещены.

## 5. Git-хуки (lefthook)

```yaml
# frontend/lefthook.yml
pre-commit:
  parallel: true
  commands:
    format:
      glob: "*.{ts,tsx,json,css,md}"
      run: pnpm prettier --write {staged_files}
      stage_fixed: true
    lint:
      glob: "*.{ts,tsx}"
      run: pnpm eslint --fix {staged_files}
      stage_fixed: true
    typecheck:
      run: pnpm typecheck
pre-push:
  commands:
    tests:
      run: pnpm test -- --run
```

- Хуки ставятся один раз: `pnpm lefthook install`.
- `--no-verify` запрещён правилами проекта; полный прогон всё равно выполняется в CI.
- Хук не должен занимать больше ~30 секунд: тяжёлые проверки — в CI, не в pre-commit.

## 6. React Compiler v1 и мемоизация

- React Compiler v1 **включён** (см. [документацию](https://react.dev/learn/react-compiler/installation),
  сборка через `reactCompilerPreset` + `@rolldown/plugin-babel`).
- Поэтому `useMemo`, `useCallback`, `React.memo` пишем **только с обоснованием**:
  - значение — зависимость эффекта, и нужна стабильная ссылка;
  - измеренная проблема производительности (профиль React DevTools до/после).
- Комментарий-обоснование обязателен: `// useCallback: стабильная ссылка для useEffect в OfferList`.
- Код должен соблюдать [Rules of React](https://react.dev/reference/rules): компилятор пропускает
  компоненты с нарушениями (мутации пропсов, побочные эффекты в рендере).

## 7. Производительность

- Маршруты — ленивые (`React.lazy` + `Suspense`) с осмысленным скелетоном.
- `ErrorBoundary`: глобальный в `app/` и на каждый маршрут (`errorElement`).
- Тяжёлые части (таблицы сравнения, графики истории цен) — динамический импорт.
- Мемоизация — см. §6; «на всякий случай» не пишем.
- Изображения: `loading="lazy"`, `width`/`height` обязательны (против сдвигов вёрстки).
- Бюджет бандла: entry-чанк ≤ 200 КБ gzip; vendor — отдельным чанком;
  проверка в CI (шаг сравнения с базовым размером) + `chunkSizeWarningLimit` как ориентир.
- Core Web Vitals — целевые значения на 75-м перцентиле
  ([web.dev](https://web.dev/articles/vitals)):
  - **LCP ≤ 2.5 c**;
  - **INP ≤ 200 мс**;
  - **CLS ≤ 0.1**.
- Замер в проде — библиотека `web-vitals` с отправкой в наш сборщик метрик; в dev —
  Lighthouse/DevTools. Регресс по метрикам — повод для задачи, а не «потом разберёмся».

## 8. Доступность: чек-лист (WCAG 2.2 AA)

Проверяется на каждом экране перед сдачей:

1. Семантика: один `h1`, заголовки по уровням, списки — `<ul>/<li>`, действия — `<button>`,
   переходы — `<a href>`.
2. Формы: `<label htmlFor>` у каждого поля, `aria-invalid`, связь ошибки через
   `aria-describedby`, обязательность — текстом, а не только цветом.
3. Клавиатура: полный обход Tab, ловушка фокуса в модалках, возврат фокуса после закрытия,
   `Escape` закрывает.
4. Фокус видим (`:focus-visible`, ring-токен); `outline: none` без замены запрещён.
5. Изображения и иконки: `alt` у значимых, `alt=""` у декоративных, `aria-label`
   у кнопок-иконок.
6. Контраст: текст ≥ 4.5:1, крупный текст и границы ≥ 3:1; интерактив ≥ 24×24 CSS px
   ([WCAG 2.2](https://www.w3.org/TR/WCAG22/)).
7. Асинхронность: `aria-live="polite"` для результатов, `aria-busy` на загружаемом
   контейнере, ошибки доступны не только визуально.
8. Движение: анимации уважают `prefers-reduced-motion`; ничего не мигает чаще 3 раз/сек.
9. Автопроверки: `eslint-plugin-jsx-a11y` без ошибок; axe-проверка ключевых страниц в e2e —
   отдельной задачей, когда появится стенд.

## 9. Команды

```bash
cd frontend
pnpm lint              # ESLint по всем пакетам
pnpm lint --fix        # автоисправления
pnpm typecheck         # tsc --noEmit
pnpm format            # Prettier --write
pnpm format:check      # проверка форматирования (CI)
pnpm lefthook install  # установка git-хуков
```

Порядок локальной проверки перед сдачей задачи:
`pnpm format:check` → `pnpm lint` → `pnpm typecheck` → `pnpm test` → `pnpm build`.