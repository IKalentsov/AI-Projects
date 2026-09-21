---
name: sqlserver-blocking-troubleshooting
description: "Поиск head blocker в SQL Server и разбор причин длительной блокировки через DMV и Extended Events, с типовыми сценариями и решениями. USE WHEN: запросы висят, таймауты приложения, блокировки, «кто держит блокировку», deadlocks, KILLED/ROLLBACK, незакоммиченная транзакция. Keywords: SQL Server, blocking, head blocker, sys.dm_exec_requests, blocking_session_id, sys.dm_tran_locks, wait_type, Extended Events, deadlock, T-SQL."
whenToUse: Когда запросы блокируют друг друга, приложение ловит таймауты или нужно найти сессию, держащую блокировку.
---

# Разбор блокировок в SQL Server

> **Источник:** составлено по официальной статье Microsoft «Understand and resolve SQL Server blocking problems»
> (KB 224453) — https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/performance/understand-resolve-blocking,
> получено 2026-09-21, статья обновлялась 2026-08-04. Локальная копия, автообновления нет.

## Что считать нормой

Блокировка — нормальное свойство любой реляционной СУБД с блокировками. Проблема начинается, когда
сессия держит блокировки **долго** или **никогда не отпускает**: падает пропускная способность,
приложение получает таймауты.

Длительность удержания блокировок определяется транзакцией и уровнем изоляции:
- вне транзакции `SELECT` держит блокировки только на момент чтения ресурса;
- `INSERT`/`UPDATE`/`DELETE` держат блокировки на время выполнения запроса;
- внутри транзакции — до её завершения.

## Порядок разбора

1. Найти **head blocker** — сессию во главе цепочки блокировок.
2. Найти запрос и транзакцию, которые держат блокировки продолжительное время.
3. Понять, **почему** это происходит.
4. Устранить причину: переработать запрос и транзакцию (не «подкрутить» сервер).

## Сбор данных

**Быстрые способы в SSMS:**
- Object Explorer → Reports → Standard Reports → **Activity - All Blocking Transactions**
  (показывает транзакции во главе цепочки, Blocking SQL Statement и Blocked SQL Statement);
- Activity Monitor, колонка **Blocked By**.

**DMV/DMF:**
- `sys.dm_exec_requests` — колонка `blocking_session_id` (`0` = не заблокирована);
- `sys.dm_exec_sessions` — все соединения, включая неактивные;
- `sys.dm_exec_sql_text(sql_handle)` — текст выполняющегося батча (`NULL` = запрос не выполняется);
- `sys.dm_exec_input_buffer(session_id, request_id)` — последний переданный движку текст;
- `sys.dm_os_waiting_tasks` — по каким `wait_type` ждут активные запросы;
- `sys.dm_tran_locks` — какие блокировки удерживаются (осторожно: на проде возвращает много строк);
- `sys.dm_tran_active_transactions`, `sys.dm_tran_session_transactions`,
  `sys.dm_tran_database_transactions` — открытые транзакции и их длительность.

Короткий запрос «кто кого блокирует»:

```sql
SELECT r.session_id, r.blocking_session_id, r.wait_type, r.wait_time, r.wait_resource,
       r.status, r.command, t.text
FROM sys.dm_exec_requests AS r
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0;
```

Большие готовые скрипты (полная цепочка блокировок, список удерживаемых блокировок по таблице)
приведены в исходной статье KB 224453 — брать оттуда, а не сочинять.

**Extended Events (SQL Trace и SQL Server Profiler устарели).** Для разбора блокировок обычно
включают категории: `blocked_process_report`, `lock_deadlock`, `attention`, `error_reported`,
`sql_batch_starting`/`sql_batch_completed`, `rpc_starting`/`rpc_completed`, `login`/`logout`,
`existing_connection`, а также предупреждения `sort_warning`, `hash_warning`,
`missing_join_predicate`, `missing_column_statistics`.
`blocked_process_report` **не выдаётся по умолчанию** — порог задаётся опцией
`blocked process threshold` через `sp_configure` (в секундах).

## Типовые сценарии

`wait_type`, `open_transaction_count` и `status` берутся из `sys.dm_exec_requests`
и `sys.dm_exec_sessions`.

| # | wait_type | open_tran | status | Разрешится само? | Симптомы |
|---|-----------|-----------|--------|------------------|----------|
| 1 | NOT NULL | >= 0 | runnable | Да, когда запрос завершится | растут `reads`, `cpu_time`, `memory_usage`; большая длительность запроса |
| 2 | NULL | > 0 | sleeping | Нет, но SPID можно убить | в XEvents виден `attention` — таймаут или отмена запроса |
| 3 | NULL | >= 0 | runnable | Нет, пока клиент не вычитает все строки или не закроет соединение | `open_transaction_count = 0` при READ COMMITTED — типичный признак |
| 4 | разный | >= 0 | runnable | Нет, пока клиент не отменит запросы или не закроет соединения | `hostname` head blocker совпадает с `hostname` блокируемой сессии |
| 5 | NULL | > 0 | rollback | Да | `attention` в XEvents: таймаут, отмена или rollback |
| 6 | NULL | > 0 | sleeping | В итоге — когда ОС определит, что сессия неактивна | `last_request_start_time` намного раньше текущего времени |

## Решения

- **Сценарий 1** — оптимизировать запрос (это производительность): смотреть Query Store, при
  невозможности — уносить тяжёлый запрос с OLTP на отчётную систему или read-only реплику.
  Отдельно учитывать эскалацию блокировок (row/page → table): держать транзакции короткими.
- **Сценарий 2** — приложение обязано в обработчике ошибок выполнять `IF @@TRANCOUNT > 0 ROLLBACK TRAN`;
  в процедурах, начинающих транзакцию, рассмотреть `SET XACT_ABORT ON`. Учитывать, что при
  пуле соединений транзакция живёт до переиспользования соединения.
- **Сценарий 3** — приложение обязано вычитывать **все** строки результата до конца;
  серверная пагинация через `OFFSET/FETCH` этому не противоречит.
- **Сценарий 4 (распределённый дедлок)** — SQL Server не может его обнаружить: одна из сторон
  находится на уровне приложения. Помогает заданный query timeout, ломающий дедлок.
- **Сценарий 5 (`KILLED/ROLLBACK`)** — ждать завершения отката; принудительная остановка инстанса
  обычно контрпродуктивна. Не выполнять крупные пакетные операции в часы нагрузки.
- **Сценарий 6 (осиротевшая транзакция)** — исправлять обработку ошибок в приложении
  (`try/catch/finally`, `SET XACT_ABORT ON`); соединение можно завершить командой `KILL <spid>`.

## Ограничения и осторожность

- `KILL` может выполняться до 30 секунд; не убивать сессии вслепую, не разобравшись в причине.
- Диагностировать нужно и на стороне приложения: таймауты, отмена запросов, менеджмент соединений,
  вычитывание всех строк — частые источники блокировок.
- Сохранять снимки DMV во времени: одиночный замер не показывает тенденцию.
