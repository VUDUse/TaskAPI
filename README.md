# TaskApi

## Стек

- .NET 9
- ASP.NET Core Minimal API
- PostgreSQL + EF Core (миграции)
- RabbitMQ (публикация событий)
- xUnit + Moq (интеграционные тесты)

## Структура проекта

```
TaskApi/
├── TaskApi.sln
├── TaskApi/
│   ├── Program.cs
│   ├── TaskApi.csproj
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Models/
│   │   ├── Priority.cs
│   │   ├── TaskItem.cs
│   │   ├── CreateTaskRequest.cs
│   │   └── TaskCompletedEvent.cs
│   ├── Data/
│   │   ├── TaskDbContext.cs
│   │   └── Migrations/
│   └── Services/
│       ├── IRabbitMqPublisher.cs
│       └── RabbitMqPublisher.cs
└── TaskApi.Tests/
    ├── TaskApi.Tests.csproj
    ├── CustomWebApplicationFactory.cs
    └── CompleteTaskIntegrationTest.cs
```

## API

| Метод | Эндпоинт | Описание |
|-------|----------|----------|
| POST | `/tasks` | Создать задачу |
| GET | `/tasks` | Получить все задачи (без пагинации) |
| PUT | `/tasks/{id}/complete` | Завершить задачу + отправить событие в RabbitMQ |
| DELETE | `/tasks/{id}` | Удалить задачу |

## Требования к окружению

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL (по умолчанию: `localhost:5432`, БД `taskdb`, пользователь `postgres`)
- RabbitMQ (по умолчанию: `localhost:5672`)

## Быстрый старт

### 1. Подготовка окружения (Docker)

**Вариант А: Docker Compose (рекомендуется)** — одной командой поднимается и PostgreSQL, и RabbitMQ:

```bash
cd TaskApi
docker-compose up -d
```

**Вариант Б: Docker вручную**:

```bash
# PostgreSQL
docker run -d --name taskapi-postgres \
  -e POSTGRES_DB=taskdb \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  postgres:16

# RabbitMQ
docker run -d --name taskapi-rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management
```

### 2. Запуск приложения

```bash
# Перейти в папку решения
cd TaskApi

# Очистить старые сборки (если были ошибки)
dotnet clean

# Восстановить пакеты
dotnet restore

# Собрать решение
dotnet build

# Применить миграции (если PostgreSQL запущен)
dotnet ef database update --project TaskApi

# Запуск
cd TaskApi
dotnet run
```

Приложение будет доступно по адресу: `http://localhost:5000`

### 3. Примеры запросов

```bash
# Создать задачу
curl -X POST http://localhost:5000/tasks \
  -H "Content-Type: application/json" \
  -d '{"title":"Buy milk","priority":"High"}'

# Получить все задачи
curl http://localhost:5000/tasks

# Завершить задачу
curl -X PUT http://localhost:5000/tasks/{id}/complete

# Удалить задачу
curl -X DELETE http://localhost:5000/tasks/{id}
```

## Запуск тестов

Тесты **не требуют** PostgreSQL и RabbitMQ — используют InMemory БД и моки:

```bash
cd TaskApi.Tests
dotnet test
```

> Тесты не требуют PostgreSQL и RabbitMQ — используют InMemory БД и моки.

Это позволяет проверить логику без поднятия всего окружения.

## Особенности реализации

| Требование | Реализация |
|------------|-----------|
| **Валидация Title** | `Title` не пустой, максимум 200 символов. При нарушении — 400 Bad Request. |
| **CompletedAt** | Заполняется **только** при завершении задачи (`PUT /tasks/{id}/complete`). При создании — `null`. |
| **RabbitMQ** | Публикация происходит **после** успешного сохранения в БД. Если RabbitMQ недоступен — задача всё равно завершается, ошибка пишется в лог (fail silently). |
| **Конкурентность** | Используется `RowVersion` (оптимистичная блокировка EF Core). При одновременном завершении — 409 Conflict. |
| **Graceful shutdown** | `RabbitMqPublisher` реализует `IHostedService`. При SIGTERM канал и соединение закрываются корректно. `ShutdownTimeout` установлен в 10 секунд. |
| **Миграции** | EF Core миграции включены (`Data/Migrations/`). Применяются автоматически при старте. |

## Конфигурация

Настройки подключения в `TaskApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=taskdb;Username=postgres;Password=postgres"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  }
}
```