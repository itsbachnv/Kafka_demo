# 01. Tài liệu Requirement

## 1. Mục tiêu
Khi khách hàng đăng ký tài khoản mới, hệ thống tự động gửi email chào mừng. Dự án dùng kiến trúc microservice để học và làm nền tảng mở rộng (thêm SMS, push, analytics sau này mà không sửa service đăng ký).

## 2. Phạm vi
**Trong phạm vi**
- API đăng ký tài khoản.
- Phát sự kiện `user.registered` lên Kafka.
- Chuyển sự kiện thành yêu cầu gửi thông báo, đẩy vào RabbitMQ.
- Worker lấy từ RabbitMQ và gửi email (dev: Mailhog).
- Giao diện web React: form đăng ký gọi API của `user-service`.

**Ngoài phạm vi (phiên bản 1)**
- Đăng nhập, xác thực JWT, quên mật khẩu.
- Xác minh email (double opt-in).
- Màn hình đăng nhập, dashboard (frontend v1 chỉ có form đăng ký).
- Gửi SMS / push.

## 3. Kiến trúc tổng quan

```
Browser (React) --HTTP /api--> [user-service] --Kafka: user.registered--> [notification-service]
                                                                   |
                                                       RabbitMQ: notification.email
                                                                   v
                                                        [email-sender-service] --SMTP--> Mailhog / SMTP thật
```

| Service | Trách nhiệm | Giao tiếp vào | Giao tiếp ra |
|---|---|---|---|
| frontend (React) | Form đăng ký, validate phía client, hiển thị kết quả | Người dùng | HTTP tới user-service |
| user-service | Nhận đăng ký, validate, lưu user, phát sự kiện | HTTP | Kafka (producer) |
| notification-service | Quyết định gửi gì, cho ai; tạo message thông báo | Kafka (consumer) | RabbitMQ (producer) |
| email-sender-service | Gửi email thật, retry, dead-letter | RabbitMQ (consumer) | SMTP |

**Vì sao tách 3 service như vậy?**
- Mỗi service có đúng một lý do để thay đổi: nghiệp vụ user / nghiệp vụ thông báo / kênh gửi.
- Kafka phù hợp cho *sự kiện nghiệp vụ* (nhiều bên cùng quan tâm, có thể replay). RabbitMQ phù hợp cho *hàng đợi công việc* (một worker xử lý một message, có ack/retry/DLQ).
- Có thể thêm `sms-sender-service` chỉ bằng cách thêm một queue mới, không đụng user-service.

## 4. Yêu cầu chức năng

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| FR-01 | `POST /api/users/register` nhận `email`, `fullName`, `password`; trả 201 kèm `userId` | Must |
| FR-02 | Validate: email đúng định dạng, password >= 8 ký tự, fullName không rỗng; sai trả 400 | Must |
| FR-03 | Email đã tồn tại trả 409 | Must |
| FR-04 | Mật khẩu được băm (PBKDF2-SHA256, có salt), không lưu/log plaintext | Must |
| FR-05 | Sau khi đăng ký thành công, phát sự kiện `user.registered` lên Kafka (key = userId) | Must |
| FR-06 | notification-service consume sự kiện và đẩy message `WELCOME_EMAIL` vào RabbitMQ queue `notification.email` | Must |
| FR-07 | email-sender-service gửi email chào mừng, ack khi thành công | Must |
| FR-08 | Gửi lỗi thì retry; quá số lần cho phép thì chuyển vào dead-letter queue | Should |
| FR-09 | Xử lý trùng lặp (idempotent): cùng `eventId` không gửi email 2 lần | Should |
| FR-10 | `GET /health` cho user-service (2 worker dựa vào log và trạng thái container) | Should |
| FR-11 | Frontend có form (họ tên, email, mật khẩu); validate phía client; gọi `POST /api/users/register` | Must |
| FR-12 | Frontend hiển thị rõ: thành công, lỗi từng trường, lỗi từ máy chủ (400/409), lỗi mạng; khoá nút khi đang gửi | Must |

## 5. Yêu cầu phi chức năng

| ID | Yêu cầu |
|---|---|
| NFR-01 | Đăng ký phản hồi < 300 ms (p95) ở tải 50 req/s, không chờ việc gửi email |
| NFR-02 | At-least-once delivery cho sự kiện và thông báo, kết hợp idempotency để tránh gửi trùng |
| NFR-03 | Một service chết không làm mất dữ liệu: Kafka và RabbitMQ (durable queue, persistent message) giữ lại |
| NFR-04 | Cấu hình qua biến môi trường (12-factor) |
| NFR-05 | Log dạng JSON, có `eventId` để truy vết xuyên service |
| NFR-06 | Chạy toàn bộ bằng `docker compose up` |
| NFR-07 | Không log dữ liệu nhạy cảm (password); email chỉ log dạng che một phần |

## 6. Hợp đồng dữ liệu (contract)

**Kafka topic `user.registered`** (key = `userId`)
```json
{
  "eventId": "uuid",
  "eventType": "user.registered",
  "occurredAt": "2026-10-21T08:55:00.000Z",
  "data": { "userId": "uuid", "email": "a@b.com", "fullName": "Nguyen Van A" }
}
```

**RabbitMQ queue `notification.email`** (durable, persistent)
```json
{
  "notificationId": "uuid",
  "sourceEventId": "uuid",
  "type": "WELCOME_EMAIL",
  "to": "a@b.com",
  "subject": "Chào mừng Nguyen Van A",
  "body": "..."
}
```
Queue lỗi: `notification.email.dlq` (qua dead-letter exchange).

## 7. Tiêu chí nghiệm thu
1. Điền form tại http://localhost:8080 (hoặc gọi API) với dữ liệu hợp lệ: nhận 201 và thấy email xuất hiện trong Mailhog (http://localhost:8025) trong 5 giây.
2. Tắt email-sender-service, đăng ký 3 user, bật lại: cả 3 email vẫn được gửi.
3. Tắt notification-service, đăng ký rồi bật lại: Kafka giao lại sự kiện, email vẫn được gửi.
4. Đăng ký trùng email trả 409 và không có sự kiện nào được phát.
5. Gửi lại cùng một sự kiện 2 lần: chỉ có 1 email.
6. Toàn bộ unit test pass.

## 8. Giả định
- Stack: backend .NET 8, frontend React 18 (Vite).
- Môi trường dev dùng 1 broker Kafka (KRaft) và 1 node RabbitMQ.
- Lưu user ở bộ nhớ (in-memory) cho phiên bản 1; có thể thay bằng PostgreSQL ở phiên bản 2 (xem file rủi ro).
