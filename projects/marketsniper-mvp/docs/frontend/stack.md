# Фронтенд: стек и версии

**Последнее обновление:** 2026-09-10

Этот документ — **источник правды по версиям фронтенда**. `ARCHITECTURE.md` на него ссылается
и не дублирует таблицы версий. Изменение версии — отдельная задача с правкой этого файла
и обоснованием в описании MR.

Все версии проверены 10.09.2026 в реестре npm (`registry.npmjs.org/<пакет>/latest`) и по
официальным релизным заметкам. Правила работы с кодом — `frontend/AGENTS.md`.

## 1. Сборка и язык

| Пакет | Версия | Пометка |
|-------|--------|---------|
| Node.js | 24 LTS (Active LTS) | фиксируется `.nvmrc` и точным образом `node:24.21.0-alpine` в Docker/CI (см. `docs/adr/0012-image-version-pinning.md`); Vite 8 требует Node 20.19+/22.12+, Vitest 5 — 22.12+ |
| pnpm | 10.x, фиксируется полем `packageManager` | монорепо |
| vite | 8.3.x | мажор 8; сборщик Rolldown ([анонс Vite 8](https://vite.dev/blog/announcing-vite8)) |
| @vitejs/plugin-react | 6.1.x | Oxc вместо Babel; React Compiler — через `reactCompilerPreset` |
| react / react-dom | 19.3.x | [React 19.3](https://react.dev/blog/2026/09/09/react-19-3): View Transitions, Fragment Refs |
| typescript | 6.0.x — **пин** | см. раздел 4 (риск TypeScript 7) |
| react-router | 8.3.x | data router, ленивые маршруты |
| babel-plugin-react-compiler | 1.0.x | React Compiler v1 ([анонс](https://react.dev/blog/2025/10/07/react-compiler-1)) |

## 2. UI

| Пакет | Версия | Пометка |
|-------|--------|---------|
| tailwindcss | 4.3.x | конфигурация CSS-first, токены в `@theme` |
| @tailwindcss/vite | 4.3.x | первый party-плагин вместо PostCSS |
| shadcn (CLI) | latest на момент задачи | код компонентов копируется в `packages/ui` |
| Base UI / Radix primitives | по зависимостям shadcn | доступность «из коробки» |
| lucide-react | по актуальной версии | иконки |
| class-variance-authority, clsx, tailwind-merge | актуальные | варианты и слияние классов |

Обоснование — `docs/adr/0004-ui-tailwind-shadcn.md`. Правила темы и доступности —
`docs/frontend/quality.md`.

## 3. Данные, состояние, формы

| Пакет | Версия | Назначение |
|-------|--------|-----------|
| @tanstack/react-query | 5.102.x | серверные данные и кэш |
| zustand | 5.0.x | клиентское состояние (UI, черновики) |
| nuqs | 2.10.x | состояние поиска в URL |
| react-hook-form | 7.87.x | формы |
| @hookform/resolvers | 5.9.x | `zodResolver` (peer `zod ^3.25 \|\| ^4`) |
| zod | 4.6.x | схемы: формы + валидация ответов API |
| orval | 8.31.x | генерация клиента из OpenAPI (хуки TanStack Query + MSW) |

Правила — `docs/frontend/data-and-state.md`.

## 4. Риск: TypeScript 7

**Факт.** В реестре npm `typescript@latest` — **7.0.2** (нативный компилятор на Go,
[обзор релиза](https://www.infoq.com/news/2026/08/typescript-7-released/)).
При этом `typescript-eslint@8.70.0` объявляет peer-зависимость `typescript >=4.8.4 <6.1.0`:
**TypeScript 7 с type-aware линтингом пока несовместим**.

**Правило (обязательное):**

1. В `package.json` фиксируем `"typescript": "6.0.x"` (без `^`, патч обновляем вручную).
2. `tsgo` (TypeScript 7) допускается **только** как отдельная быстрая проверка типов
   в CI или локально (`pnpm dlx @typescript/native-preview --noEmit`), не как версия
   для ESLint, сборки и IDE-плагинов.
3. Переход на TypeScript 7 — отдельная задача **после** выхода `typescript-eslint@9`
   (или официального подтверждения поддержки TS 7): обновляем пин, `eslint.config.mjs`,
   правила `paths` (в TS 7 `baseUrl` не работает — использовать `paths` от корня)
   и прогоняем полный набор тестов.
4. До перехода запрещено: писать код, требующий TS 7, и добавлять `baseUrl` в конфиги.

Отслеживание: страница [typescript-eslint dependency versions](https://typescript-eslint.io/users/dependency-versions).

## 5. Тесты

| Пакет | Версия | Назначение |
|-------|--------|-----------|
| vitest | 5.0.x | unit + component + integration ([Vitest 5](https://vitest.dev/blog/vitest-5)) |
| @vitest/coverage-v8 | 5.0.x | покрытие |
| jsdom | актуальная | среда для компонентных тестов |
| @testing-library/react | 16.3.x | поведение, запросы по роли |
| @testing-library/user-event | 14.x | пользовательские взаимодействия |
| @testing-library/jest-dom | 6.x | матчеры DOM |
| msw | 2.15.x | мок сетевого слоя |
| @playwright/test | 1.63.x | e2e |

Правила и определение уровней — `docs/frontend/testing.md`.

## 6. Качество кода

| Инструмент | Версия | Назначение |
|-----------|--------|-----------|
| eslint | 10.10.x | flat config; [ESLint v10](https://eslint.org/blog/2026/02/eslint-v10.0.0-released/) |
| typescript-eslint | 8.70.x | `recommendedTypeChecked`, `projectService` |
| eslint-plugin-react-hooks | latest | правила React + правила компилятора |
| eslint-plugin-jsx-a11y | latest | доступность в JSX |
| eslint-plugin-import-x | latest | `no-restricted-paths` (границы слоёв и фич) |
| prettier | 3.9.x | форматирование |
| lefthook | 1.x | git-хуки |

Правила, конфиги и чек-листы — `docs/frontend/quality.md`.

## 7. Обоснование ключевых выборов

### 7.1 Vite 8 + React SPA вместо Next.js

У нас отдельный .NET API и два разных потребителя: SPA (поиск и результаты) и будущий
лендинг. Vite 8 даёт минимальный рантайм, быстрый HMR, статическую сборку (раздаётся
любым веб-сервером или Caddy) и не навязывает серверную модель. Next.js требует
собственного Node-рантайма, а его серверные возможности (RSC, route handlers) дублировали бы
то, что уже делает backend. Подробнее — `docs/adr/0003-frontend-react-spa.md`.

### 7.2 React Router 8

Маршрутизация как данные: вложенные маршруты, ленивые загрузки, ошибки и `loader`-хуки
без привязки к фреймворку. Версия 8 — текущая мажорная, совместима с React 19 и адаптерами
nuqs ([документация](https://reactrouter.com/)).

### 7.3 Tailwind CSS 4.3 + shadcn/ui

Tailwind 4 — CSS-first: токены описываются в `@theme` и сразу становятся CSS-переменными,
плагин `@tailwindcss/vite` собирает CSS быстрее PostCSS
([Tailwind v4](https://tailwindcss.com/blog/tailwindcss-v4)). shadcn/ui отдаёт код компонентов
в репозиторий (CLI + registry, [ui.shadcn.com/docs/cli](https://ui.shadcn.com/docs/cli)),
поэтому мы не зависим от версии UI-библиотеки, полностью контролируем разметку и доступность
и не платим за лицензии. Альтернативы — `docs/adr/0004-ui-tailwind-shadcn.md`.

### 7.4 Генерация клиента: orval

**Рекомендация — orval 8.31.** Из одной OpenAPI-схемы он генерирует типы, клиент, хуки
TanStack Query и MSW-хендлеры, то есть закрывает и рантайм, и интеграционные тесты
([orval.dev](https://orval.dev)). Схема — из backend (Scalar/`Microsoft.AspNetCore.OpenApi`).

**Запасной вариант — openapi-typescript + openapi-fetch** (7.13 / 0.17): минимум кода
и рантайма, но хуки запросов и моки пишем руками ([openapi-ts.dev](https://openapi-ts.dev)).
Переход на него возможен, если генератор начнёт мешать (например, понадобится нестандартный
слой поверх клиента) — решение фиксируется новым ADR.

### 7.5 ESLint 10 + typescript-eslint вместо Biome

Biome 2.5 быстрее и умеет линтить и форматировать одним бинарником
([biomejs.dev](https://biomejs.dev)), но **не поддерживает правила, требующие информации
о типах**, а нам нужны `no-floating-promises`, `no-unsafe-*`, `no-misused-promises`
и правила React-компилятора. Поэтому: ESLint 10 (flat config) + typescript-eslint +
eslint-plugin-react-hooks, форматирование — Prettier. Возврат к вопросу — когда Biome
закроет type-aware правила.

## 8. Что НЕ используем и почему

| Технология | Почему нет |
|-----------|-----------|
| Next.js 16 | Серверный рантайм и RSC дублируют backend; для лендинга достаточно отдельного SSG/SSR-приложения позже |
| TanStack Start | Хороший вариант, но экосистема и число рецептов меньше; переходить есть смысл только под SSR-лендинг |
| Biome как линтер | Нет type-aware правил (см. 7.5) |
| NSwag / openapi-typescript-codegen | Слабый OpenAPI 3.1 и рантайм-типы `any`; заменены orval/openapi-typescript |
| moment.js | Устаревшая библиотека с изменяемым состоянием; используем `Intl` / `date-fns` |
| MUI, Ant Design, Mantine, Chakra | Тяжёлый рантайм и собственные темы; для нашего UI достаточно shadcn/ui + Tailwind |
| Redux / Redux Toolkit | Избыточно при TanStack Query + Zustand |
| CSS-in-JS (styled-components, Emotion) | Лишний рантайм и конфликт с Tailwind |
| CRA, webpack | Устарели: CRA не поддерживается, webpack медленнее Rolldown |
| Storybook (на старте) | Подключим при ≥15–20 компонентах в `packages/ui` (см. `docs/frontend/testing.md`) |
| SonarQube-правила по фронту как единственный контроль | Основной контроль — ESLint/типы/тесты; SonarQube — дополнительный отчёт в CI |

## 9. Порядок обновления версий

1. Обновление — отдельная задача (не «попутно» в фиче).
2. Проверяем релизные заметки и breaking changes, обновляем один мажор за раз.
3. Прогоняем `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build`, `pnpm test:e2e`.
4. Обновляем таблицы этого файла и, при необходимости, `docs/frontend/*`.
5. Мажорные обновления React, Vite, TypeScript, Tailwind — с записью в `docs/adr/`.
