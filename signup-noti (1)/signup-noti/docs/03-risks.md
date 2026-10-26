# 03. Phân tích rủi ro

Thang điểm: Xác suất (X) và Tác động (T) từ 1 (thấp) đến 3 (cao). Điểm = X × T.

| ID | Rủi ro | X | T | Điểm | Biện pháp giảm thiểu | Trạng thái trong v1 |
|---|---|---|---|---|---|---|
| R1 | **Dual-write**: lưu user thành công nhưng publish Kafka lỗi, user không nhận được email | 2 | 3 | 6 | Producer có retry và `acks=all`; về lâu dài dùng Transactional Outbox. Nếu publish lỗi, API trả 201 nhưng ghi log lỗi mức cao để có thể gửi bù | Giảm một phần, outbox để v2 |
| R2 | **Gửi email trùng** do at-least-once (consumer restart, rebalance, retry) | 3 | 2 | 6 | Idempotency theo `eventId` / `notificationId` | Có (in-memory) |
| R3 | Idempotency in-memory mất khi restart nên vẫn có thể trùng sau restart | 2 | 2 | 4 | Chuyển sang Redis ở v2; chấp nhận trùng hiếm gặp ở v1 | Chấp nhận |
| R4 | **Poison message**: message hỏng làm consumer kẹt mãi | 2 | 3 | 6 | Validate schema, message lỗi thì bỏ qua và log; RabbitMQ dùng DLQ | Có |
| R5 | Mất dữ liệu in-memory của user-service khi restart | 3 | 3 | 9 | Ghi rõ đây là bản demo; thay bằng PostgreSQL trước khi dùng thật | **Cao nhất, bắt buộc xử lý ở v2** |
| R6 | Kafka hoặc RabbitMQ chưa sẵn sàng khi service khởi động | 3 | 1 | 3 | Retry kết nối khi khởi động, `restart: on-failure` trong compose | Có |
| R7 | Rò rỉ dữ liệu cá nhân trong log (email, mật khẩu) | 2 | 3 | 6 | Không log password, che email; hash PBKDF2-SHA256 có salt | Có |
| R8 | Thay đổi contract sự kiện làm vỡ consumer | 2 | 2 | 4 | Có trường `eventType`, thêm trường mới theo kiểu tương thích ngược; v2 dùng Schema Registry | Có tài liệu contract |
| R9 | Email vào spam / SMTP provider giới hạn tốc độ | 2 | 2 | 4 | Dùng provider uy tín, cấu hình SPF/DKIM, rate limit ở worker | Ngoài phạm vi dev |
| R10 | Đăng ký hàng loạt (spam/bot) gây quá tải và spam email | 2 | 3 | 6 | Rate limit theo IP, captcha; xác minh email trước khi gửi nội dung | Chưa có, đưa vào backlog |
| R11 | Một broker Kafka là điểm lỗi duy nhất | 2 | 3 | 6 | Production dùng tối thiểu 3 broker, replication factor 3 | Chỉ ảnh hưởng môi trường prod |
| R12 | Khó debug xuyên service | 2 | 2 | 4 | Truyền `eventId` qua mọi bước, log JSON | Có |
| R13 | Độ phức tạp vận hành microservice cao hơn so với quy mô bài toán | 3 | 1 | 3 | Hiểu đây là dự án học tập; nếu chỉ cần gửi email đơn giản, monolith sẽ rẻ hơn | Ghi nhận |

## Rủi ro cần xử lý trước khi lên production
1. R5: dùng DB thật.
2. R1: Transactional Outbox.
3. R10: rate limit và xác minh email.
4. R11: cụm Kafka nhiều broker.
