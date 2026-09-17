# AI-Projects

Мои проекты для души: пишу их с помощью нейросетей, чтобы расти как software engineer.

Каждый проект живёт в отдельной папке внутри [`projects/`](projects/), собирается, тестируется и
запускается **самостоятельно** — со своими зависимостями, документацией, правилами и тестами.

---

## Проекты

| Проект | Что это | Стек | Состояние |
|--------|---------|------|-----------|
| **[MarketSniper](projects/marketsniper-mvp/)** | Поиск проверенных поставщиков и товаров на маркетплейсах (Ozon, Яндекс.Маркет, Wildberries, ВкусВилл) по текстовому запросу | .NET 10 (Clean Architecture), ASP.NET Core, PostgreSQL 18, Redis 8, React + TypeScript, Docker + Caddy, GitLab CI | Домен в работе: `Currency`, `Money`, `Rating`, `Slug` + 44 unit-теста. Каркас бэкенда собирается, фронтенд не начат |
| **[QwenAnswers](projects/QwenAnswers/)** | Консольный чат с локально запущенной AI-моделью через OpenAI-совместимый API (Ollama, vLLM, LM Studio, llama.cpp) | .NET 10, Microsoft.Extensions.AI + OpenAI SDK, xUnit + Moq + FluentAssertions | Готов и покрыт тестами: unit, integration (проверка `.env`) и живые тесты против запущенной модели |

Состояние и планы каждого проекта — в его `README.md` и `memory-bank/`.

---

## Раскладка репозитория

```
AI-Projects/
├── projects/
│   ├── marketsniper-mvp/   # сервис поиска товаров на маркетплейсах
│   └── QwenAnswers/        # консольный чат с локальной AI-моделью
├── LICENSE                 # MIT
├── README.md               # этот файл
└── REPOSITORIES.md         # реестр проектов и правила границ
```

У каждого проекта есть свой `README.md` и `memory-bank/` (состояние между сессиями).
Процессные документы — `AGENTS.md`, `WORKFLOW.md`, `ARCHITECTURE.md`, `docs/` — проект заводит,
когда в них появляется нужда: у MarketSniper они есть, у QwenAnswers пока нет.

---

## Как добавить проект

1. Создать папку `projects/<имя-проекта>/`.
2. Добавить строку в реестр — [`REPOSITORIES.md`](REPOSITORIES.md).
3. Завести внутри проекта `README.md` и `memory-bank/`; процессные документы
   (`AGENTS.md`, `WORKFLOW.md`, `ARCHITECTURE.md`, `docs/`) — когда понадобятся.

Правила границ между проектами, владение файлами и процесс работы описаны
в [`REPOSITORIES.md`](REPOSITORIES.md).

---

## Лицензия

[MIT](LICENSE) © Ilya Kalentsov
