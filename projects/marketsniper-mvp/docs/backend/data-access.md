# Доступ к данным

**Последнее обновление:** 2026-09-10
Решение по хранилищам — `docs/adr/0005-data-postgres-redis.md`.

## Разделение ответственности

| Инструмент | Для чего | Когда использовать |
|------------|----------|--------------------|
| EF Core | запись, транзакции, типовые чтения, миграции | по умолчанию |
| Dapper | сложные выборки, отчёты, агрегаты | когда LINQ становится нечитаемым или неэффективным |
| Redis | кэш внешних ответов, rate-limit, идемпотентность | всё, что можно пересчитать |

Правило: **источник правды — PostgreSQL**. Данные в Redis всегда восстановимы.

## EF Core

### Конфигурация сущностей

```csharp
public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.Price).HasColumnName("price").HasPrecision(18, 2).IsRequired();
        builder.Property(o => o.CreateAt).HasColumnName("create_at").IsRequired();
        builder.Property(o => o.UpdateAt).HasColumnName("update_at").IsRequired();

        builder.HasIndex(o => new { o.MarketplaceId, o.ExternalId }).IsUnique();
        builder.HasIndex(o => o.SellerId);
    }
}
```

- Таблицы и колонки — `snake_case`; подключение конфигураций — `ApplyConfigurationsFromAssembly`.
- Денежные значения — `numeric(18,2)`, не `double`.
- Время — `timestamptz` (UTC).
- Обязательные индексы: внешние ключи, поля фильтрации/сортировки, уникальные бизнес-ключи.
- Полуструктурированные атрибуты карточек — `jsonb` с GIN-индексом при необходимости поиска.

### Чтение

- Всегда `AsNoTracking()` для запросов «на чтение».
- Проекция в DTO сразу в запросе: `.Select(o => new OfferDto(...))`.
- Никакой ленивой загрузки; `Include` — только осознанно и с проверкой плана.
- Списки — только с пагинацией; keyset-пагинация предпочтительнее offset на больших таблицах.
- Тяжёлые запросы — проверять `EXPLAIN ANALYZE` и добавлять индексы осознанно.

### Запись и транзакции

- Один use case — одна транзакция (`IUnitOfWork`/`DbContext.SaveChangesAsync`).
- Идемпотентность внешних операций — через уникальные ключи и/или блокировки в Redis.
- Массовые вставки — пакетами (например `ExecuteUpdate`/`COPY` через Dapper для больших объёмов).
- Конкурентный доступ — оптимистическая блокировка (`xmin`/`RowVersion`) там, где это важно.

### Миграции

```bash
cd backend
dotnet ef migrations add AddOfferPriceIndex --project src/MarketSniper.Infrastructure.Postgres \
  --startup-project src/MarketSniper.Web
dotnet ef database update --project src/MarketSniper.Infrastructure.Postgres \
  --startup-project src/MarketSniper.Web
```

- Имя миграции — по смыслу изменения, не `Update1`.
- Файлы миграций руками не правятся.
- Одна миграция — одно логическое изменение.
- Схема применяется в CI/тестах автоматически (интеграционные тесты поднимают БД с нуля).

## Dapper

```csharp
const string sql = """
    SELECT o.id, o.price, s.name AS seller_name
    FROM offers o
    JOIN sellers s ON s.id = o.seller_id
    WHERE o.category_id = @CategoryId AND o.price <= @MaxPrice
    ORDER BY o.price
    LIMIT @Limit OFFSET @Offset
    """;
```

- SQL — только параметризованный (никакой конкатенации значений).
- Имена параметров — как в запросе; DTO — отдельный тип чтения (не доменная сущность).
- Сложные запросы — с комментарием, зачем Dapper вместо EF Core.

## Redis

- Ключи: `marketsniper:v1:<domain>:<id>` — с версией схемы, чтобы менять формат без «мусора».
- TTL обязателен для всего кэша; разные типы данных — разные TTL.
- Что кэшируем: ответы внешних API, агрегаты, дорогие выборки, результаты проверки критериев.
- Что НЕ кэшируем: персональные данные, промежуточные состояния транзакций, секреты.
- Инвалидация — явная, по ключу, при изменении источника; «кэш сам протухнет» — не стратегия.
- Rate-limit обращений к маркетплейсам — обязателен, ключ на площадку и эндпоинт.

## Тестирование данных

- Unit-тесты домена — без БД.
- Интеграционные — реальный PostgreSQL (Testcontainers), миграции применяются на старте.
- Чистота между тестами — Respawn (или транзакция с откатом там, где это уместно).
- Внешние API — WireMock.Net с сохранёнными фикстурами.
