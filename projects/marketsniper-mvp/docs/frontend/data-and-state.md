# Фронтенд: данные и состояние

**Последнее обновление:** 2026-09-10

Правила работы с серверными данными, клиентским состоянием, URL и формами. Версии —
`docs/frontend/stack.md`, структура — `docs/frontend/project-structure.md`,
формы в контексте экрана — скилл `react-development`.

## 1. Разделение ответственности

| Вид состояния | Инструмент | Примеры |
|---------------|-----------|---------|
| Серверные данные | TanStack Query | результаты поиска, карточка предложения, продавцы |
| Состояние в URL | nuqs | запрос, фильтры, сортировка, страница |
| Клиентское состояние | Zustand | тема, свёрнутые панели, черновик фильтров |
| Локальное состояние | `useState` | открыт диалог, активная вкладка |
| Состояние формы | react-hook-form | поля и ошибки ввода |

Правило: **не дублировать** серверные данные в Zustand и не копировать URL-состояние
в `useState`.

## 2. TanStack Query: ключи запросов

```ts
// features/offer-search/api/offer-keys.ts
export const offerKeys = {
  all: ['offers'] as const,
  lists: () => [...offerKeys.all, 'list'] as const,
  list: (params: OfferSearchParams) => [...offerKeys.lists(), params] as const,
  details: () => [...offerKeys.all, 'detail'] as const,
  detail: (id: string) => [...offerKeys.details(), id] as const,
};
```

- Иерархия `all → lists → list(params)` — инвалидация по префиксу работает предсказуемо.
- Порядок элементов массива важен, объекты хешируются по содержимому
  ([query keys](https://tanstack.com/query/latest/docs/framework/react/guides/query-keys)).
- Ключ собирается только фабрикой; параметры нормализуются (без `undefined`, массивы отсортированы).

## 3. staleTime, gcTime, инвалидация

```ts
export function useOfferSearch(params: OfferSearchParams) {
  return useQuery({
    queryKey: offerKeys.list(params),
    queryFn: ({ signal }) => searchOffers(params, signal),
    enabled: params.query.trim().length >= 2,
    staleTime: 60_000,
    gcTime: 5 * 60_000,
    retry: (failureCount, error) => !isClientError(error) && failureCount < 2,
    placeholderData: keepPreviousData,
  });
}
```

| Данные | staleTime |
|--------|-----------|
| Результаты поиска, цены, наличие | 30–60 секунд |
| Карточка предложения | 5 минут |
| Справочники (площадки, категории) | 30 минут или `Infinity` |
| Пользовательские настройки | 5 минут + инвалидация после сохранения |

Дефолты TanStack Query агрессивны: данные сразу устаревшие, неактивные запросы живут 5 минут,
ошибки повторяются трижды ([important defaults](https://tanstack.com/query/latest/docs/framework/react/guides/important-defaults)).
Поэтому `staleTime` задаём осознанно под каждый ресурс. Инвалидация — в мутации, по префиксу,
с возвратом промиса (`onSettled: () => queryClient.invalidateQueries({ queryKey: offerKeys.lists() })`).

## 4. Оптимистичные обновления

Гайд: [optimistic updates](https://tanstack.com/query/latest/docs/framework/react/guides/optimistic-updates).

1. **Без кэша.** Оптимистичный объект строим из `variables` и показываем рядом со списком
   (`isPending && <OfferCardSkeleton title={String(variables)} />`). Откат не нужен: при ошибке
   временный элемент исчезает.
2. **Через кэш (`onMutate`).** Нужен, когда обновление видно в нескольких местах. Обязательны
   отмена текущих запросов, снапшот, откат и инвалидация:

```ts
onMutate: async (offerId) => {
  await queryClient.cancelQueries({ queryKey: offerKeys.lists() });
  const previous = queryClient.getQueryData(offerKeys.lists());
  queryClient.setQueryData(offerKeys.lists(), markSaved(offerId));
  return { previous }; // снапшот для отката
},
onError: (_error, _offerId, context) =>
  queryClient.setQueryData(offerKeys.lists(), context?.previous),
onSettled: () => queryClient.invalidateQueries({ queryKey: offerKeys.lists() }),
```

Правило: оптимистично обновляем только действия, которые почти всегда успешны (сохранить,
отметить просмотренным); поиск и «серьёзные» операции — с индикатором загрузки.

## 5. Что НЕ кладём в кэш

Секреты, токены и персональные данные (они не должны попадать даже в devtools и логи);
«сырые» ответы маркетплейсов (на фронт приходит только наш DTO); бинарные данные; клиентское
состояние (диалоги, черновики, тема); всё, что должно переживать перезагрузку страницы —
это URL (nuqs) или осознанно `localStorage`.

## 6. Zustand

```ts
// features/offer-filters/model/filters-draft.store.ts
export const useFiltersDraft = create<FiltersDraftState>()((set) => ({
  draft: defaultFilters,
  setDraft: (draft) => set({ draft }),
  reset: () => set({ draft: defaultFilters }),
}));
```

- Для чего: UI-состояние приложения, черновики до «Применить» (применённое уходит в URL),
  межфичевые флаги.
- Запрещено: хранить серверные данные; делать «один store на всё»; подписываться на весь store
  (`useStore()` без селектора — только `useStore(s => s.field)`); мутировать состояние напрямую.
- Экшены маленькие и именованные по действию; запросы к API в Zustand не живут — они в TanStack Query.

## 7. URL-состояние (nuqs)

Ссылка на результаты поиска должна шариться и воспроизводиться, поэтому запрос, фильтры,
сортировка и страница живут в query string.

```ts
// features/offer-search/model/search-params.ts
import { parseAsArrayOf, parseAsInteger, parseAsString, parseAsStringLiteral } from 'nuqs';

export const SORT_OPTIONS = ['trust', 'price-asc', 'price-desc'] as const;

export const searchParamsParsers = {
  q: parseAsString.withDefault(''),
  marketplace: parseAsArrayOf(parseAsStringLiteral(MARKETPLACES)).withDefault([]),
  minTrust: parseAsInteger,
  sort: parseAsStringLiteral(SORT_OPTIONS).withDefault('trust'),
  page: parseAsInteger.withDefault(1),
};

// в компоненте страницы:
const [params, setParams] = useQueryStates(searchParamsParsers, { history: 'push' });
```

Схема URL: `q` — текст запроса; `marketplace` — площадки (`marketplace=ozon&marketplace=wildberries`);
`minTrust` — минимальная оценка доверия; `sort` — `trust` | `price-asc` | `price-desc`;
`page` — страница.

- URL — источник правды; кэш TanStack Query строится из этих же значений.
- Дефолты — в парсерах (`page=1`, `sort=trust`, пустой `q`), URL не засоряем.
- Ввод текста — локальное состояние + debounce; в URL пишем по сабмиту или паузе ввода.
- Невалидное значение парсер отбрасывает к дефолту, экран не падает.
- Подключение к React Router — адаптер `nuqs/adapters/react-router/v6|v7|v8` ([nuqs docs](https://nuqs.dev/docs)).

## 8. Формы: react-hook-form + zod

```ts
// features/offer-search/model/search-form-schema.ts
export const searchFormSchema = z.object({
  query: z.string().trim().min(2, 'Введите минимум 2 символа').max(120, 'Слишком длинный запрос'),
  marketplace: z.array(z.enum(MARKETPLACES)).min(1, 'Выберите хотя бы одну площадку'),
});

export type SearchFormValues = z.infer<typeof searchFormSchema>;
```

```tsx
const form = useForm<SearchFormValues>({
  resolver: zodResolver(searchFormSchema),
  defaultValues: { query: '', marketplace: [...MARKETPLACES] },
});
```

- Схема — единственный источник типа формы; отдельный `interface` для формы не пишем.
- Схема формы (ввод) и схема ответа API (контракт) — разные сущности.
- Ответ API валидируется до использования; не прошло валидацию — состояние ошибки и запись
  в лог, а не «данные без части полей»:

```ts
const offersResponseSchema = z.object({
  items: z.array(offerSchema),
  total: z.number().int().nonnegative(),
  partial: z.boolean().default(false),
});

const data = offersResponseSchema.parse(await response.json());
```

## 9. Ошибки

Backend отдаёт ProblemDetails ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457));
единый маппер в `packages/api-client/src/http/` приводит ответ к нашему типу:

```ts
export type ApiError = {
  kind: 'validation' | 'notFound' | 'conflict' | 'rateLimited' | 'server' | 'network';
  title: string;                      // текст для пользователя, по-русски
  details?: Record<string, string[]>; // ошибки по полям
  traceId?: string;                   // для поддержки
  status: number;
};
```

- Пользователю — `title` и действие; `traceId` — в технических деталях; стектрейсы и «сырой»
  JSON не показываем.
- Ошибки запросов — состояние `isError` в UI + кнопка «Повторить» (`refetch`).
- Ошибки рендера — `ErrorBoundary` в `app/` и `errorElement` маршрута.
- Ошибки мутаций — тост или сообщение рядом с действием; «тихих» провалов не бывает.

## 10. Ретраи и отмена

- GET: до 2 повторов при сети и 5xx; 4xx (кроме 429) не повторяем.
- Мутации: авторетраев нет; идемпотентность — задача backend.
- Таймаут — на уровне http-клиента (`AbortSignal.timeout(10_000)`).
- `queryFn` принимает `signal` и передаёт его в fetch: смена фильтров и размонтирование
  отменяют запрос. Отмена — не ошибка и в UI не показывается.
