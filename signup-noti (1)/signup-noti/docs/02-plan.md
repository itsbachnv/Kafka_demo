# 02. Kế hoạch triển khai (Plan)

## 1. Công nghệ
| Hạng mục | Lựa chọn | Lý do |
|---|---|---|
| Backend | .NET 8 (C# 12), ASP.NET Core minimal API cho `user-service`, Worker Service cho 2 service còn lại | Theo yêu cầu; Worker Service phù hợp tiến trình nền chỉ consume message |
| Kafka client | Confluent.Kafka | Client chính thức của Confluent, dùng librdkafka |
| RabbitMQ client | RabbitMQ.Client 7.x (API async) | Client chuẩn cho .NET, hỗ trợ publisher confirms |
| Email | MailKit + Mailhog (dev) | MailKit là thư viện SMTP được khuyến nghị thay cho `SmtpClient` cũ |
| Test backend | xUnit, `WebApplicationFactory` | Chuẩn của hệ sinh thái .NET |
| Frontend | React 18 + Vite | Theo yêu cầu; Vite khởi động và build nhanh |
| Test frontend | Vitest + React Testing Library | Chạy cùng cấu hình Vite |
| Chạy local | Docker Compose | Một lệnh dựng toàn bộ hạ tầng và ứng dụng |

## 2. Cấu trúc thư mục
```
signup-noti/
├── docker-compose.yml
├── README.md
├── docs/                              # tài liệu
├── scripts/                           # test-all.sh, e2e.sh
├── backend/
│   ├── SignupNoti.sln
│   ├── UserService/                   # ASP.NET Core API + Kafka producer
│   ├── UserService.Tests/
│   ├── NotificationService/           # Worker: Kafka consumer -> RabbitMQ publisher
│   ├── NotificationService.Tests/
│   ├── EmailSenderService/            # Worker: RabbitMQ consumer -> SMTP
│   └── EmailSenderService.Tests/
└── frontend/                          # React (Vite) + nginx
```
Mỗi service độc lập: có csproj, Dockerfile và model riêng (không dùng chung thư viện), để có thể phát hành và scale riêng.

## 3. Các giai đoạn (ước lượng cho 1 dev)

| Giai đoạn | Công việc | Ước lượng | Sản phẩm |
|---|---|---|---|
| M0. Chuẩn bị | Chốt requirement, contract sự kiện, dựng docker-compose hạ tầng | 0,5 ngày | Kafka + RabbitMQ + Mailhog chạy được |
| M1. UserService | API đăng ký, validate, băm mật khẩu, producer Kafka, unit test | 1 ngày | Gọi API thấy event trong Kafka |
| M2. NotificationService | Consumer Kafka, tạo message, publish RabbitMQ, idempotency, unit test | 1 ngày | Event vào Kafka thì có message trong queue |
| M3. EmailSenderService | Consumer RabbitMQ, gửi SMTP, retry, DLQ, unit test | 1 ngày | Email hiện trong Mailhog |
| M4. Frontend | Form đăng ký React, validate, xử lý lỗi, test, nginx proxy | 1 ngày | Đăng ký được từ trình duyệt |
| M5. Tích hợp | docker-compose đầy đủ, test end-to-end, test chịu lỗi | 1 ngày | Đạt các tiêu chí nghiệm thu |
| M6. Release | Dockerfile, CI, tài liệu vận hành, tag phiên bản | 0,5 ngày | v1.0.0 |

Tổng: khoảng 6 ngày làm việc.

## 4. Thiết kế chi tiết các điểm quan trọng

**Kafka**
- Topic `user.registered`, 3 partition, key = `userId` (đảm bảo thứ tự theo user). Service tự tạo topic nếu chưa có.
- Consumer group `notification-service`, `EnableAutoCommit=false`, commit offset thủ công sau khi đã đẩy xong vào RabbitMQ (at-least-once). Lỗi tạm thời thì `Seek` về đúng offset và thử lại.
- Producer bật `Acks=All`, `EnableIdempotence=true`.

**RabbitMQ**
- Exchange `notification` (direct), queue `notification.email` (durable), routing key `email`.
- Dead-letter exchange `notification.dlx` và queue `notification.email.dlq`.
- Publisher bật publisher confirms; message `Persistent = true`; consumer `prefetch = 10`, ack thủ công.

**Idempotency**
- NotificationService: nhớ `eventId` đã xử lý (v1: in-memory có giới hạn kích thước; v2: Redis).
- EmailSenderService: nhớ `notificationId` đã gửi, bỏ qua bản trùng.

**Retry**
- EmailSenderService thử tối đa 3 lần (backoff 1s, 2s). Hết số lần thì `BasicNack(requeue: false)` để vào DLQ.

**Cấu hình** qua biến môi trường theo quy ước .NET (dấu `__` thay cho `:`): `Kafka__BootstrapServers`, `RabbitMq__Url`, `Smtp__Host`, `Smtp__Port`, `Retry__MaxAttempts`.

**Frontend và API**
- Trình duyệt chỉ gọi `/api/...` cùng origin. Lúc dev, Vite proxy sang `localhost:3000`; trong Docker, nginx proxy sang `user-service:8080`. Nhờ vậy không cần cấu hình CORS.
- Validate phía client chỉ để phản hồi nhanh; luật thật nằm ở `user-service`.

## 5. Chiến lược test
Xem chi tiết ở `04-test-plan.md`. Tóm tắt: xUnit cho backend, Vitest cho frontend, E2E bằng docker-compose và `scripts/e2e.sh`.

## 6. Tiêu chí hoàn thành (Definition of Done)
- Code review xong, unit test pass (backend và frontend).
- E2E pass trên docker-compose sạch.
- Không có secret trong repo.
- README hướng dẫn chạy từ đầu trong dưới 10 phút.
- Đã gắn tag phiên bản.

## 7. Hướng mở rộng sau v1
1. Thay in-memory bằng PostgreSQL (EF Core) cho user-service và Redis cho idempotency.
2. Transactional Outbox cho user-service để không mất sự kiện khi lưu DB thành công mà Kafka lỗi.
3. Thêm `sms-sender-service`, chỉ cần queue `notification.sms` mới.
4. Schema Registry (Avro/JSON Schema) cho contract sự kiện.
5. Observability: OpenTelemetry, Prometheus, Grafana.
6. Health check cho worker (liveness qua file hoặc HTTP endpoint nhỏ).
