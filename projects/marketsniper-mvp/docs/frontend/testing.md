# Фронтенд: тестирование

**Последнее обновление:** 2026-09-10

Правила тестирования фронтенда. Общая стратегия тестирования проекта и требования DoD —
корневой `AGENTS.md` (§9); версии инструментов — `docs/frontend/stack.md`.

## 1. Пирамида

| Уровень | Инструмент | Что проверяем | Доля усилий |
|---------|-----------|---------------|-------------|
| Unit | Vitest 5 | Чистая логика: маппинг DTO → модель, форматтеры, парсеры параметров, валидаторы | ~20% |
| Component | Vitest 5 + RTL 16 | Один компонент: рендер по пропсам, пользовательские события, доступность | ~30% |
| Integration | Vitest 5 + RTL + MSW 2.15 | Фича/страница целиком: данные, состояния, URL, формы — как в реальном приложении | ~40% |
| E2E | Playwright 1.63 | Ключевые сквозные сценарии на реальном стенде | ~10% |

Правило: если сценарий можно проверить интеграционным тестом — не пишем e2e;
если проверяется чистая функция — не поднимаем компонент.

## 2. Unit (Vitest)

Расположение: рядом с кодом, `<имя>.test.ts`.

```ts
// features/offer-search/model/map-offer.test.ts
import { describe, expect, it } from 'vitest';
import { mapOffer } from './map-offer';

describe('mapOffer', () => {
  it('помечает предложение частичным, если площадка не вернула цену', () => {
    const result = mapOffer({ id: '1', price: null, marketplace: 'ozon' });

    expect(result.isPartial).toBe(true);
    expect(result.priceLabel).toBe('Цена не указана');
  });
});
```

Правила: без сети и таймеров реального времени; время фиксируем (`vi.setSystemTime`);
тестируем границы (пустой запрос, максимальная длина, `null`/`undefined`), а не только happy path.

## 3. Component (React Testing Library)

Тестируем **поведение**, а не реализацию ([guiding principles](https://testing-library.com/docs/guiding-principles/)).

Обязательно:

- Запросы только по доступному имени/роли: `getByRole`, `getByLabelText`, `getByText`.
  `getByTestId` — исключение с комментарием-обоснованием.
- Взаимодействия — `user-event`, а не `fireEvent`.
- Проверяем то, что видит и делает пользователь: текст, доступные имена, изменения на экране.
- Асинхронность — `findBy*`/`waitFor`, без `setTimeout` в тестах.

Запрещено:

- Обращаться к внутреннему состоянию компонента, пропсам-«кишкам», приватным функциям.
- Снапшоты разметки (`toMatchSnapshot`) — они ломаются от любой правки вёрстки.
- Проверять количество вызовов внутренних хуков и моков, если это не контракт.
- Обёртывать всё в `act()` вручную «чтобы не было предупреждений» — чинить причину.

```tsx
// features/offer-search/ui/offer-search-form.test.tsx
it('показывает ошибку валидации при слишком коротком запросе', async () => {
  const user = userEvent.setup();
  renderWithProviders(<OfferSearchForm onSubmit={vi.fn()} />);

  await user.type(screen.getByLabelText('Поисковый запрос'), 'о');
  await user.click(screen.getByRole('button', { name: 'Найти' }));

  expect(await screen.findByText('Введите минимум 2 символа')).toBeInTheDocument();
});
```

## 4. Integration (RTL + MSW)

**Определение.** Интеграционный тест рендерит фичу или страницу целиком (роутер, QueryClient,
тема, формы) и подменяет **только сетевой слой** — через MSW. Моки модулей (`vi.mock`)
для внутренних модулей приложения не используются: они превращают тест в проверку реализации.

Что покрывает интеграционный тест:

- переход «пользователь ввёл запрос → увидел результат»;
- все состояния экрана: загрузка, пусто, ошибка, успех;
- влияние фильтров и сортировки на запрос к API (проверяем параметры запроса через MSW);
- синхронизацию состояния с URL;
- доступность ключевых элементов (роль, имя, связь ошибки с полем).

```tsx
// features/offer-search/offer-search.integration.test.tsx
import { http, HttpResponse } from 'msw';
import { server } from '@shared/testing/msw-server';

it('показывает предложения по запросу и пишет запрос в URL', async () => {
  server.use(
    http.get('/api/v1/offers', ({ request }) => {
      const url = new URL(request.url);
      expect(url.searchParams.get('q')).toBe('грецкие орехи');
      return HttpResponse.json({ items: [offerFixture], total: 1, partial: false });
    }),
  );

  renderWithProviders(<OfferSearchPage />, { route: '/search' });
  await userEvent.type(screen.getByLabelText('Поисковый запрос'), 'грецкие орехи');
  await userEvent.click(screen.getByRole('button', { name: 'Найти' }));

  expect(await screen.findByRole('heading', { name: 'Грецкие орехи' })).toBeInTheDocument();
  expect(window.location.search).toContain('q=');
});
```

Правило: фича считается покрытой, если есть минимум один интеграционный тест на основной
сценарий и по одному на состояния «пусто» и «ошибка».

## 5. E2E (Playwright)

- Только ключевые сценарии продукта: «поиск → результат → переход на маркетплейс»,
  «фильтры → результат», «карточка предложения». Полный список фиксируется в задаче.
- Локаторы — user-facing: `getByRole`, `getByLabel`, `getByText`
  ([best practices](https://playwright.dev/docs/best-practices)). CSS/XPath-селекторы запрещены.
- Автождущие assertions (`expect(locator).toBeVisible()`), никаких `page.waitForTimeout`.
- Артефакты при падении: trace + скриншот + видео (trace viewer обязателен в CI-разборе).
- Стенд поднимается окружением (docker compose) или dev-сервером; тесты не зависят
  от реальных API маркетплейсов — backend-моки или тестовый контур.

## 6. Моки и фикстуры

| Что | Правило |
|-----|---------|
| Хендлеры MSW по нашим эндпоинтам | Генерируются из OpenAPI (orval) в `packages/api-client/src/generated/mocks` |
| Хендлеры с особым поведением (ошибки, частичный ответ, задержки) | Пишем руками в `shared/testing/handlers/`, поверх сгенерированных |
| Фикстуры данных | `shared/testing/fixtures/` — фабрики с дефолтами (`makeOffer({ price: 100 })`), без копирования больших JSON |
| Реальная сеть | Запрещена во всех тестах, кроме e2e на тестовом стенде |
| Моки модулей | Запрещены, кроме внешних библиотек, которые нельзя подменить сетью (например, `matchMedia`) |

Глобально в `vitest.setup.ts`: поднимается MSW-сервер (`beforeAll`), сбрасываются хендлеры
(`afterEach`), проверяется отсутствие «непойманных» запросов (`onUnhandledRequest: 'error'`).

## 7. Что НЕ тестируем

- Сгенерированный код (`packages/api-client/src/generated/**`) — его корректность
  обеспечивает генератор и контрактные проверки.
- Типы и конфиги (`*.d.ts`, `vite.config.ts`, `eslint.config.mjs`).
- Сторонние компоненты UI-кита «на предмет работы» — только наша композиция и наши пропсы.
- Вёрстку и CSS-классы (цвет, отступы) — это проверяет дизайн-ревью и визуальный контроль.
- Внутренние детали реализации (какие хуки вызвались, сколько ререндеров прошло).

## 8. Покрытие

| Область | Целевое покрытие |
|---------|------------------|
| `features/**` | 70–80% строк и функций |
| `entities/**`, `shared/lib`, `shared/api` | 70–80% |
| Критичные пути: поиск, фильтрация, маппинг ответа, работа с URL | 100% ветвей логики |
| `shared/ui` (компоненты дизайн-системы) | по факту использования: покрываем то, что имеет логику |
| Сгенерированный код, типы, конфиги, `routes/**` | не измеряем |

- Метрика — «строки и функции», без `perFile`-порогов: они заставляют писать пустые тесты.
- Падение покрытия ниже порога — красный пайплайн; повышение порога — отдельной задачей.
- Покрытие не цель: непокрытый критичный сценарий важнее, чем 100% по утилите.

## 9. Детерминированность

1. Нет реальной сети (MSW или мок-сервер), нет обращений к внешним API маркетплейсов.
2. Нет ожиданий по времени: вместо `sleep` — `findBy*`, `waitFor`, `expect.poll`.
3. Время и таймзона фиксированы: `vi.setSystemTime(new Date('2026-09-10T00:00:00Z'))`.
4. Случайность фиксирована: `vi.spyOn(Math, 'random')` или фабрики с seed.
5. Тесты не зависят от порядка выполнения; состояние сбрасывается в `afterEach`
   (`queryClient.clear()`, `server.resetHandlers()`, `cleanup()`).
6. Один тест — одно поведение; имя описывает сценарий и ожидание по-русски.
7. Флейки не «перезапускаем до зелёного»: тест либо чинится, либо удаляется с обоснованием.

## 10. Структура и именование тестов

```
features/offer-search/
├── api/use-offer-search.test.ts              # unit: хук с MSW (границы параметров)
├── model/map-offer.test.ts                   # unit: чистая логика
├── model/filters.test.ts                     # unit: состояние фильтров
├── ui/offer-search-form.test.tsx             # component: форма и валидация
├── offer-search.integration.test.tsx         # integration: страница целиком
└── ...
apps/web/e2e/
├── offer-search.spec.ts                      # e2e: поиск → результат
└── offer-card.spec.ts                        # e2e: карточка предложения
```

- Суффиксы: `.test.ts(x)` — unit/component, `.integration.test.tsx` — интеграционный,
  `.spec.ts` — e2e (Playwright).
- Тестовые утилиты — в `shared/testing` (`renderWithProviders`, `msw-server`, `fixtures`).
- Фикстуры не дублируются между фичами: общее — в `shared/testing/fixtures`.

## 11. Команды

```bash
cd frontend
pnpm test                      # все unit + component + integration (watch в dev)
pnpm test -- --project unit    # только unit-проект
pnpm test:coverage             # покрытие (v8)
pnpm test:e2e                  # Playwright, все сценарии
pnpm test:e2e --ui             # режим отладки
pnpm test:e2e -- --trace on    # запись трассировок
```

Перед сдачей задачи обязателен полный прогон `pnpm test` и, если затронуты пользовательские
сценарии, `pnpm test:e2e`; в CI запускаются оба набора.
