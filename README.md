# PulseNet — Базовый скелет социальной сети (OTUS Highload Architect, ДЗ №1)

**PulseNet** — монолитное веб-приложение социальной сети на **.NET 10 (`net10.0`)** и **PostgreSQL** без использования ORM (прямая работа через ADO.NET провайдер `Npgsql` с параметризованными SQL-запросами).

---

## 1. Реализованные требования ТЗ

### Функциональные возможности
- **Регистрация пользователя (`POST /user/register`)** — создание анкеты со следующими полями:
  - Имя (`first_name`)
  - Фамилия (`second_name`)
  - Дата рождения (`birthdate`, формат `YYYY-MM-DD`)
  - Пол (`gender`: `male` | `female` | `unspecified`, совместимое расширение `openapi.json` — при отсутствии поля в запросе по умолчанию сохраняется `unspecified`)
  - Интересы / о себе (`biography`)
  - Город (`city`)
  - Пароль (`password`)
- **Простейшая авторизация (`POST /login`)** — аутентификация по идентификатору анкеты (`id`) и паролю (`password`) с выдачей подписанного Bearer-токена (`HMAC-SHA256`).
- **Получение анкеты по ID (`GET /user/get/{id}`)** и просмотр собственной анкеты по токену (`GET /user/me`).
- **Встроенный веб-интерфейс (`/` и `/profile/{id}`)** — интерактивная форма входа и регистрации с анимацией SVG-маскота, морфингом облака, звёздным созвездием на Canvas, нижним стеклянным док-баром, всплывающим поиском анкеты по UUID и страницей просмотра анкеты.

### Нефункциональные требования и безопасность
- **Без ORM:** используется исключительно `NpgsqlDataSource` + `NpgsqlCommand` ([`PostgresUserRepository.cs`](src/PulseNet.Api/Data/PostgresUserRepository.cs)). Отсутствие любых ORM (`EF Core`, `Dapper`, `NHibernate` и др.) и динамической склейки SQL контролируется статическим fail-closed гейтом [`SqlAndOrmArchitectureGate.cs`](src/PulseNet.Api/Security/SqlAndOrmArchitectureGate.cs).
- **Устойчивость к SQL-инъекциям:** все SQL-запросы являются константами времени компиляции и используют строго типизированные параметры `NpgsqlParameter<T>` (`NpgsqlDbType.Uuid`, `Varchar`, `Date`, `Text`, `TimestampTz`). На границе API идентификаторы проверяются типизированной обёрткой [`UserId.cs`](src/PulseNet.Api/Domain/UserId.cs).
- **Безопасное хранение паролей:** алгоритм `PBKDF2-HMAC-SHA256` (`100 000` итераций, криптостойкая 16-байтная соль `RandomNumberGenerator`, ключ 32 байта) со сравнением за константное время через `CryptographicOperations.FixedTimeEquals` ([`PasswordHasher.cs`](src/PulseNet.Api/Security/PasswordHasher.cs)).
- **Без репликации, шардирования, кэширования и вторичных индексов:** в [`db/init.sql`](db/init.sql) создаётся только таблица `users` с первичным ключом `PRIMARY KEY (id)`.

---

## 2. Структура проекта

```text
├── Dockerfile                                    # Многоступенчатая сборка .NET 10
├── docker-compose.yml                            # Локальный стек: PostgreSQL 17 + PulseNet.Api
├── global.json                                   # Привязка к .NET SDK 10.0
├── openapi.json                                  # Спецификация OTUS Highload Architect
├── db/
│   └── init.sql                                  # DDL-схема таблицы users без вторичных индексов
├── postman/
│   └── PulseNet.postman_collection.json          # Postman-коллекция с авто-тестами
├── src/
│   └── PulseNet.Api/
│       ├── Contracts/ApiContracts.cs             # DTO контрактов OpenAPI
│       ├── Data/IUserRepository.cs               # Порт репозитория пользователей
│       ├── Data/PostgresUserRepository.cs        # Параметризованный ADO.NET репозиторий Npgsql
│       ├── Domain/                               # UserId, Gender, UserProfile
│       ├── Endpoints/UserEndpoints.cs            # Маршруты /login, /user/register, /user/get/{id}, /user/me
│       ├── Security/                             # PBKDF2, HMAC-токены, аудит, статический гейт SQL/ORM
│       ├── Validation/InputValidator.cs          # Валидация на границе API
│       └── wwwroot/                              # Встроенный фронтенд (HTML/CSS/JS)
└── tests/
    └── PulseNet.Tests/                           # Модульные, интеграционные тесты и приманки гейта
```

---

## 3. Инструкция по локальному запуску

### Вариант А: Запуск через .NET 10 CLI и локальный PostgreSQL

1. Создайте базу данных `pulsenet` и примените схему (если схема не применена, приложение также автоматически выполнит `CREATE TABLE IF NOT EXISTS users` при старте):
   ```bash
   createdb pulsenet 2>/dev/null || true
   psql -d pulsenet -f db/init.sql
   ```

2. Запустите монолит **PulseNet**:
   ```bash
   export PULSENET_DB_CONNECTION="Host=localhost;Port=5432;Database=pulsenet;Username=$(whoami)"
   export PULSENET_TOKEN_SECRET="FAKEPII_HmacKey_777000001_PulseNet_Synthetic_Secret_Key_32B"
   ~/.dotnet/dotnet run --project src/PulseNet.Api/PulseNet.Api.csproj --urls "http://localhost:8080"
   ```

3. Откройте в браузере:
   - Веб-интерфейс (вход, регистрация, поиск и страница анкеты): `http://localhost:8080`
   - Проверка здоровья: `http://localhost:8080/healthz`

### Вариант Б: Запуск через Docker Compose

```bash
docker compose up --build -d
```
Сервис будет доступен на `http://localhost:8080`, а база данных PostgreSQL 17 — на порту `5432` с автоматически применённым [`db/init.sql`](db/init.sql).

---

## 4. Проверка через curl и Postman

### Примеры запросов (curl)

1. **Регистрация пользователя (`POST /user/register`):**
   ```bash
   curl -s -X POST http://localhost:8080/user/register \
     -H "Content-Type: application/json" \
     -d '{
       "first_name": "FAKE_Ivan",
       "second_name": "Zz_Petrov",
       "birthdate": "1996-05-14",
       "gender": "male",
       "biography": "FAKEPII_Highload architecture, PostgreSQL, .NET 10",
       "city": "FAKEPII_City_777000001",
       "password": "FAKEPII_Pass_900000001!"
     }'
   ```
   Ответ:
   ```json
   {"user_id":"e4d2e6b0-cde2-42c5-aac3-0b8316f21e58"}
   ```

2. **Авторизация (`POST /login`):**
   ```bash
   curl -s -X POST http://localhost:8080/login \
     -H "Content-Type: application/json" \
     -d '{
       "id": "<USER_ID_ИЗ_ШАГА_1>",
       "password": "FAKEPII_Pass_900000001!"
     }'
   ```
   Ответ:
   ```json
   {"token":"pnet...."}
   ```

3. **Получение анкеты по ID (`GET /user/get/{id}`):**
   ```bash
   curl -s http://localhost:8080/user/get/<USER_ID_ИЗ_ШАГА_1>
   ```

### Запуск Postman-коллекции

1. Откройте Postman и нажмите **Import**.
2. Выберите файл [`postman/PulseNet.postman_collection.json`](postman/PulseNet.postman_collection.json).
3. По умолчанию переменная `baseUrl` настроена на `http://localhost:8080`.
4. Запустите коллекцию через **Run collection** по порядку шагов `1 -> 6`:
   - Шаг 1 регистрирует анкету и автоматически сохраняет `user_id` в переменную коллекции `{{userId}}`.
   - Шаг 2 выполняет вход по `{{userId}}` и сохраняет токен в `{{authToken}}`.
   - Шаги 3–6 проверяют чтение анкеты по ID, доступ по Bearer-токену, защиту от SQL-инъекции (`400 Bad Request`) и ответ `404 Not Found` для несуществующего пользователя.

---

## 5. Запуск автотестов

```bash
~/.dotnet/dotnet test PulseNet.sln
```
Тестовый набор включает модульные тесты криптографии и валидации, проверку приманок и мутантов статического гейта отсутствия ORM/SQLi и интеграционные тесты против реального PostgreSQL.
