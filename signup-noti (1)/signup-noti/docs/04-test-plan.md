# 04. Test Plan

## 1. Các tầng test

| Tầng | Công cụ | Phạm vi |
|---|---|---|
| Unit backend | xUnit (+ `WebApplicationFactory` cho API) | Validate, hash, handler từng service, retry/idempotency (fake broker) |
| Unit frontend | Vitest + React Testing Library | Form, validate, hiển thị kết quả/lỗi (mock `fetch`) |
| Tích hợp / E2E | docker-compose + `scripts/e2e.sh` | Luồng thật qua Kafka, RabbitMQ, Mailhog |
| Chịu lỗi (manual) | docker compose stop/start | Tắt từng service, kiểm tra không mất message |

Chạy unit test: `dotnet test backend/SignupNoti.sln` và `npm test` trong `frontend/` (hoặc `./scripts/test-all.sh` để chạy cả hai).

## 2. Test case

### user-service
| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| U-01 | Đăng ký hợp lệ | 201, có `userId`, publish đúng 1 sự kiện `user.registered` |
| U-02 | Email sai định dạng | 400, không publish |
| U-03 | Password < 8 ký tự | 400, không publish |
| U-04 | Thiếu fullName | 400 |
| U-05 | Email đã tồn tại (không phân biệt hoa thường) | 409, không publish thêm |
| U-06 | Response và sự kiện không chứa password | Đúng |
| U-07 | Kafka lỗi khi publish | API vẫn 201 (user đã lưu), lỗi được ghi log |
| U-08 | `GET /health` | 200 |

### notification-service
| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| N-01 | Nhận sự kiện hợp lệ | Publish 1 message `WELCOME_EMAIL` đúng người nhận |
| N-02 | Nhận cùng `eventId` hai lần | Chỉ publish 1 lần |
| N-03 | Sự kiện thiếu email hoặc sai cấu trúc | Bỏ qua, không publish, không throw |
| N-04 | Publish RabbitMQ lỗi | Throw để Kafka giao lại, eventId chưa bị đánh dấu đã xử lý |
| N-05 | JSON hỏng | Bỏ qua, không crash |

### email-sender-service
| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| E-01 | Gửi thành công | Ack |
| E-02 | Gửi lỗi lần 1, thành công lần 2 | Ack, đã retry |
| E-03 | Lỗi cả 3 lần | Nack không requeue (vào DLQ) |
| E-04 | Cùng `notificationId` hai lần | Chỉ gửi 1 email, cả hai lần đều ack |
| E-05 | Message sai cấu trúc | Nack không requeue |

### Frontend
| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| F-01 | Mở trang | Có 3 trường và nút Đăng ký |
| F-02 | Bấm Đăng ký khi form trống | Hiện lỗi từng trường, không gọi API |
| F-03 | Mật khẩu 7 ký tự | Bị chặn ở client, không gọi API |
| F-04 | Đăng ký thành công (201) | Gọi đúng URL/method/body, hiện thông báo thành công, xoá form |
| F-05 | Máy chủ trả 409 | Hiện "Email đã được đăng ký" |
| F-06 | Máy chủ trả 400 kèm danh sách lỗi | Hiện tất cả lỗi |
| F-07 | Lỗi mạng | Hiện thông báo không kết nối được máy chủ |
| F-08 | Đang chờ phản hồi | Nút bị khoá, mở lại sau khi xong |

### E2E / chịu lỗi
| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| X-01 | Đăng ký qua form (http://localhost:8080) hoặc qua API | Email xuất hiện trong Mailhog |
| X-02 | Tắt email-sender, đăng ký 3 user, bật lại | Cả 3 email được gửi |
| X-03 | Tắt notification-service, đăng ký, bật lại | Email vẫn được gửi (Kafka giữ offset) |
| X-04 | Tắt Mailhog, đăng ký | Message vào DLQ sau 3 lần thử |
| X-05 | Đăng ký 200 user liên tiếp | 200 email, không trùng |

## 3. Tiêu chí qua test
- 100% unit test pass.
- X-01 đến X-05 pass trên môi trường compose sạch.
- Không có password nào xuất hiện trong log (kiểm tra bằng `docker compose logs | grep`).

## 4. Chạy E2E
```bash
docker compose up -d --build
./scripts/e2e.sh
```
