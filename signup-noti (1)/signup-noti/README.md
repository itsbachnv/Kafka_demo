# Hệ thống thông báo đăng ký tài khoản

Backend **.NET 8** + frontend **React**, giao tiếp bằng **Kafka** và **RabbitMQ**.

```
Browser (React) --/api--> user-service --Kafka (user.registered)--> notification-service
                                                                          |
                                                              RabbitMQ (notification.email)
                                                                          v
                                                          email-sender-service --SMTP--> Mailhog
```

| Thành phần | Công nghệ | Vai trò |
|---|---|---|
| `frontend` | React 18, Vite, nginx | Form đăng ký, hiển thị kết quả và lỗi |
| `backend/UserService` | ASP.NET Core minimal API, Confluent.Kafka | API đăng ký, lưu user, phát sự kiện `user.registered` |
| `backend/NotificationService` | Worker Service, Confluent.Kafka, RabbitMQ.Client | Consume Kafka, tạo message `WELCOME_EMAIL`, đẩy vào RabbitMQ |
| `backend/EmailSenderService` | Worker Service, RabbitMQ.Client, MailKit | Consume RabbitMQ, gửi email (retry 3 lần, lỗi thì vào DLQ) |

## Chạy nhanh (cần Docker)
```bash
docker compose up -d --build
```
- Form đăng ký: http://localhost:8080
- API: http://localhost:3000 (ví dụ `curl -X POST localhost:3000/api/users/register -H 'content-type: application/json' -d '{"email":"a@example.com","fullName":"Nguyen Van A","password":"secret123"}'`)
- Xem email: http://localhost:8025 (Mailhog)
- Xem queue: http://localhost:15672 (guest / guest)

## Chạy khi phát triển (không build image)
```bash
docker compose up -d kafka rabbitmq mailhog      # chỉ dựng hạ tầng
cd backend/UserService && dotnet run             # http://localhost:5000 (xem log để biết cổng)
cd backend/NotificationService && dotnet run
cd backend/EmailSenderService && dotnet run
cd frontend && npm install && VITE_API_TARGET=http://localhost:5000 npm run dev   # http://localhost:5173
```
Cấu hình mặc định trong `appsettings.json` đã trỏ tới `localhost:29092` (Kafka), `localhost:5672` (RabbitMQ), `localhost:1025` (SMTP).

## Test
```bash
./scripts/test-all.sh     # dotnet test + npm test
./scripts/e2e.sh          # E2E, cần docker compose đang chạy
```

## Tài liệu
1. [Requirement](docs/01-requirements.md)
2. [Plan](docs/02-plan.md)
3. [Rủi ro](docs/03-risks.md)
4. [Test plan](docs/04-test-plan.md)
5. [Release và vận hành](docs/05-release.md)

## Giới hạn của v1
User lưu in-memory, idempotency in-memory, chưa có rate limit và Transactional Outbox (xem `docs/03-risks.md`).

## Ghi chú về .NET
- Dự án nhắm `net8.0`. Nếu máy bạn dùng .NET 10, đổi `TargetFramework` trong các `.csproj` và tag image trong Dockerfile (`sdk:8.0` -> `sdk:10.0`, tương tự cho `aspnet` và `runtime`).
- Biến môi trường dùng quy ước .NET: `Kafka__BootstrapServers`, `RabbitMq__Url`, `Smtp__Host`...
