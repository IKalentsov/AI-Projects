---
name: dotnet-development
description: Правила написания C#-кода в этом проекте: слои, паттерны, EF Core, обработка ошибок, стиль.
whenToUse: При написании или правке любого кода на C#/.NET в backend/.
---

# .NET Development

## Перед началом

1. Прочитать `backend/AGENTS.md` (правила слоя) и при необходимости `ARCHITECTURE.md`.
2. Найти, как похожая задача уже решена в проекте (grep по домену, а не «с нуля»).
3. Понять, в какой слой попадает изменение (см. `docs/architecture/clean-architecture.md`).
4. Если задача меняет контракт/структуру/пакеты — сначала сообщить пользователю (это архитектура).

## Рабочий процесс

1. **Тест** на новое поведение (unit — для домена и логики).
2. **Домен**: правило живёт в сущности/value object, а не в сервисе.
3. **Core**: use case принимает примитивы/DTO, возвращает `Result<T>`.
4. **Инфраструктура**: реализация интерфейса, объявленного в Core.
5. **Web**: тонкий контроллер + маппинг `Result` в HTTP.
6. Прогнать: сборку → тесты → `dotnet format --verify-no-changes`.

## Стиль C#

- `file-scoped namespace`, `Nullable=enable`, `ImplicitUsings=enable`.
- Имена: типы/методы — `PascalCase`, локальные и параметры — `camelCase`,
  приватные поля — `_camelCase` (см. `.editorconfig`), константы — `PascalCase`.
- Асинхронные методы — суффикс `Async`, принимают `CancellationToken` последним параметром.
- `var` — только когда тип очевиден из правой части; для встроенных типов — явный тип.
- Предпочитать `switch`-выражения, pattern matching, collection expressions (`[..]`).
- Никаких `#region`, закомментированного кода и «магических» чисел.
- Комментарий оправдан, когда объясняет **почему**, а не **что**.

## Result Pattern

```csharp
public static Result<Offer, Error> Create(OfferId id, Price price, SellerId sellerId)
{
    if (price.Amount <= 0)
        return OfferErrors.InvalidPrice();            // типизированная ошибка

    return new Offer(id, price, sellerId);            // успех
}
```

Правила:

- Ошибки — типизированные (`Error` с кодом и сообщением), а не строки «на всё».
- Исключения — только для сбоев инфраструктуры; бизнес-ошибки — всегда `Result`.
- `Result` нельзя игнорировать: либо обработать, либо вернуть выше.
- В контроллере — единый маппинг `Result` → HTTP-код + ProblemDetails.

## Value Objects

```csharp
public sealed record Price
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    private Price(decimal amount, Currency currency) { Amount = amount; Currency = currency; }

    public static Result<Price> Create(decimal amount, Currency currency) =>
        amount < 0 ? PriceErrors.Negative() : new Price(amount, currency);
}
```

- Валидация — в фабрике, а не в месте использования.
- VO неизменяем; сравнение — по значению.
- Сырые `string`/`decimal`/`Guid` через границы слоёв не проходят без веской причины.

## EF Core

- Конфигурация — отдельный класс `IEntityTypeConfiguration<T>`; подключение через
  `ApplyConfigurationsFromAssembly`.
- Имена таблиц/колонок — `snake_case`; связь по ID, без навигационных коллекций через агрегаты.
- Чтение — `AsNoTracking()` + проекция в DTO; никакой ленивой загрузки.
- Никаких запросов в цикле (N+1); при необходимости — `Include`/`Select` одним запросом.
- Индексы — под все фильтры и сортировки; проверять план запроса для тяжёлых выборок.
- Миграции — только через `dotnet ef migrations add`; имена — по смыслу (`AddOfferIndexes`).
- Dapper — для сложных выборок, отчётов и аналитики; SQL — параметризованный.

## Асинхронность и ресурсы

- `async` до самого низа; `.Result`/`.Wait()`/`async void` запрещены.
- `CancellationToken` пробрасывается во все I/O-вызовы.
- `IHttpClientFactory` + типизированные клиенты; таймаут, ретраи с backoff, circuit breaker.
- `IDisposable`/`IAsyncDisposable` — через `using`/`await using`.

## Обработка ошибок и логирование

- Ошибка должна иметь контекст: что делали, какой ресурс, какой идентификатор.
- Логи — структурированные, без интерполяции строк:
  `_logger.LogWarning("Offer {OfferId} not found for query {Query}", offerId, query);`
- Не логировать секреты, токены, персональные данные и полные ответы внешних API.
- Correlation ID — в логах и в ответе.

## Конфигурация

- `IOptions<T>` + `appsettings.{Environment}.json` + переменные окружения.
- Секреты — только через окружение / user-secrets; в git не попадают никогда.
- Значения по умолчанию — в коде, переопределения — в конфигурации; «магических» строк нет.

## Типичные ошибки

| Ошибка | Как правильно |
|--------|---------------|
| Логика в контроллере | Вынести в use case (Core) |
| `Domain` ссылается на EF Core | Маппинг — в инфраструктуре |
| Строка вместо VO | `Price`, `MarketplaceId`, `ProductUrl` |
| Возврат `null` | `Result`/`Option` с явной ошибкой |
| `catch (Exception) { }` | Ловить конкретное, логировать, не глотать |
| N+1 запросы | Проекция одним запросом |
| Синхронный вызов внешнего API | `async` + таймаут + ретрай |
| Новый пакет «на глаз» | Версия в `Directory.Packages.props`, лицензия проверена |

## Чек-лист самопроверки

- [ ] Код лежит в правильном слое; зависимости — внутрь.
- [ ] Бизнес-правила — в домене, а не в сервисе/контроллере.
- [ ] Использованы VO и типизированные ошибки; нет magic strings.
- [ ] Есть тесты на новое поведение (unit; integration — если затронут API/БД).
- [ ] `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` — чисто.
- [ ] Логи структурированные, без чувствительных данных.
- [ ] Новых пакетов нет либо они согласованы и внесены в `Directory.Packages.props`.
