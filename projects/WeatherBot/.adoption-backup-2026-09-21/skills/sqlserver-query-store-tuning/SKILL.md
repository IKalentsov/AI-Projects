---
name: sqlserver-query-store-tuning
description: "Работа с SQL Server Query Store — включение, контроль состояния, восстановление из read-only, поиск регрессий запросов, закрепление планов. USE WHEN: запрос стал медленнее, нужно найти топ запросов по ресурсам, планы «прыгают», Query Store перешёл в read-only. Keywords: SQL Server, Query Store, sys.database_query_store_options, regressed queries, forced plan, execution plan, T-SQL."
whenToUse: Когда нужно найти регрессии производительности запросов и стабилизировать планы через Query Store.
---

# Query Store: мониторинг и стабилизация планов

> **Источник:** составлено по официальным статьям Microsoft
> «Best practices for monitoring workloads with Query Store»
> (https://learn.microsoft.com/en-us/sql/relational-databases/performance/best-practice-with-the-query-store,
> обновлена 2026-07-20) и «Monitor performance by using the Query Store»; получено 2026-09-21.
> Локальная копия, автообновления нет. Применимо к SQL Server 2016 и новее.

## Включение

```sql
ALTER DATABASE [DatabaseOne] SET QUERY_STORE = ON;
```

Репрезентативный набор данных о нагрузке обычно набирается за сутки, но разбирать данные можно сразу.

## Контроль состояния (регулярно)

Query Store может **молча** сменить режим работы. Проверка:

```sql
SELECT actual_state_desc, desired_state_desc, current_storage_size_mb, max_storage_size_mb,
       readonly_reason, interval_length_minutes, stale_query_threshold_days,
       size_based_cleanup_mode_desc, query_capture_mode_desc
FROM sys.database_query_store_options;
```

Расхождение `actual_state_desc` и `desired_state_desc` означает автоматическую смену режима,
чаще всего — переход в read-only. `readonly_reason = 65536` означает превышение квоты размера.

## Возврат в read-write

```sql
ALTER DATABASE [QueryStoreDB]
SET QUERY_STORE (OPERATION_MODE = READ_WRITE);
```

Перед этим при необходимости: увеличить `MAX_STORAGE_SIZE_MB` и/или очистить данные
(`ALTER DATABASE [QueryStoreDB] SET QUERY_STORE CLEAR;`).

Состояние ERROR: начиная с SQL Server 2017 — выключить Query Store, выполнить
`sys.sp_query_store_consistency_check` в базе, затем включить и перевести в READ_WRITE.
Для SQL Server 2016 — очистка (`CLEAR`) и повторное включение.

Профилактика: держать размер ниже максимума, включить очистку по размеру (size-based)
и по времени (time-based), `QUERY_CAPTURE_MODE = AUTO`.

## Представления SSMS и когда их брать

| Представление | Для чего |
|---|---|
| Regressed Queries | запросы, метрики которых ухудшились — связать жалобы с конкретными запросами |
| Overall Resource Consumption | общее потребление ресурсов базой, дневные и ночные паттерны |
| Top Resource Consuming Queries | самые дорогие запросы за выбранный интервал |
| Queries With Forced Plans | все текущие закреплённые планы |
| Queries With High Variation | запросы с сильной вариативностью: длительность, CPU, IO, память |
| Query Wait Statistics | категории ожиданий и вклад запросов в них (SSMS 18+, SQL Server 2017+) |
| Tracked Queries | наблюдение за важными запросами в реальном времени |

## Что делать, когда проблема найдена

- **План стал хуже** → закрепить план принудительно (force plan). Если закрепление не удаётся,
  срабатывает XEvent, и оптимизатор оптимизирует запрос обычным образом.
- **Не хватает индекса** → это видно в плане выполнения; создать индекс и проверить эффект
  в Query Store. Перед созданием прогнать проверку индекса — см. скилл `sqlserver-index-verification`.
- **Большая разница estimated и actual rows** → рассмотреть принудительную перекомпиляцию статистики.
- **Плохой запрос** → переписать, в том числе ради параметризации.

## Непараметризованные запросы

Они ломают переиспользование планов и раздувают Query Store. Поиск:

```sql
SELECT qsq.query_id, qsqt.query_sql_text
FROM sys.query_store_query AS qsq
JOIN sys.query_store_query_text AS qsqt ON qsq.query_text_id = qsqt.query_text_id
WHERE qsq.query_parameterization_type = 0;
```

Меры: параметризовать запросы (процедура, `sp_executesql`), включить `optimize for ad hoc workloads`,
при необходимости forced parameterization или plan guide, поставить `QUERY_CAPTURE_MODE = AUTO`.
Для EF Core учитывать особенности параметризации ORM (LINQ и raw SQL).

## Проверка закреплённых планов (регулярно)

```sql
SELECT p.plan_id, p.query_id, q.object_id AS containing_object_id,
       force_failure_count, last_force_failure_reason_desc
FROM sys.query_store_plan AS p
JOIN sys.query_store_query AS q ON p.query_id = q.query_id
WHERE is_forced_plan = 1;
```

## Ограничения

- Не использовать паттерн `DROP` + `CREATE` для процедур, функций и триггеров: создаётся новая
  запись запроса, статистика теряется и план нельзя закрепить — применять `ALTER`.
- Не переименовывать базу, у которой есть закреплённые планы: закрепление сломается.
- Mission-critical серверы: трассировочные флаги 7745 (не сбрасывать данные перед остановкой)
  и 7752 (асинхронная загрузка Query Store); с SQL Server 2019 флаг 7752 не действует.
- Для SQL Server 2016/2017 выпускались исправления производительности Query Store — на актуальных
  сборках они уже включены.
