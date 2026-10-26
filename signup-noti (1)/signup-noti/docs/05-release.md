# 05. Release & Vận hành

## 1. Phiên bản
Dùng Semantic Versioning. Bản đầu tiên: `v1.0.0`. Mỗi service build thành image Docker riêng, gắn cùng tag phiên bản.

## 2. Checklist trước khi release
- [ ] Unit test pass (`./scripts/test-all.sh`)
- [ ] E2E pass (`./scripts/e2e.sh`) trên môi trường sạch
- [ ] Các test chịu lỗi X-02, X-03, X-04 pass
- [ ] Không có secret/mật khẩu trong repo hay log
- [ ] README và tài liệu cập nhật
- [ ] Đã xử lý hoặc chấp nhận rõ ràng các rủi ro điểm >= 6 (xem `03-risks.md`)
- [ ] Đã thay in-memory bằng DB thật nếu lên production (R5)

## 3. Các bước release
```bash
git tag v1.0.0 && git push --tags

# 3 service .NET: build context là ./backend, Dockerfile nằm trong thư mục từng service
for s in UserService NotificationService EmailSenderService; do
  img=$(echo "$s" | tr '[:upper:]' '[:lower:]')
  docker build -f backend/$s/Dockerfile -t your-registry/$img:1.0.0 ./backend
  docker push your-registry/$img:1.0.0
done

# frontend (nginx phục vụ file build của React)
docker build -t your-registry/frontend:1.0.0 ./frontend
docker push your-registry/frontend:1.0.0
```

## 4. Môi trường
| Môi trường | Hạ tầng | Ghi chú |
|---|---|---|
| Local/dev | docker-compose (1 Kafka, 1 RabbitMQ, Mailhog) | Toàn bộ trong một lệnh |
| Staging | Kafka 3 broker, RabbitMQ, SMTP sandbox | Chạy E2E tự động |
| Production | Cụm Kafka/RabbitMQ được quản lý (managed), SMTP provider thật | Cấu hình bằng secret manager |

## 5. Triển khai và rollback
- Triển khai theo thứ tự: **email-sender-service, notification-service, user-service, frontend** (consumer trước, producer sau, giao diện cuối) để consumer luôn sẵn sàng nhận dữ liệu mới.
- Frontend gọi `/api` cùng origin qua reverse proxy (nginx/ingress); không cần bật CORS.
- Contract sự kiện chỉ được thay đổi theo hướng tương thích ngược; thêm trường mới, không xóa/đổi tên trường.
- Rollback: triển khai lại image tag trước đó. Vì Kafka và RabbitMQ giữ message nên rollback không làm mất dữ liệu; idempotency tránh gửi trùng.

## 6. Giám sát (khuyến nghị tối thiểu)
| Chỉ số | Cảnh báo khi |
|---|---|
| Kafka consumer lag của `notification-service` | Tăng liên tục > 5 phút |
| Độ sâu queue `notification.email` | > 1000 và tăng |
| Số message trong `notification.email.dlq` | > 0 |
| Tỉ lệ 5xx của user-service | > 1% |
| Log lỗi `publish_failed` của user-service | Có xuất hiện |

## 7. Xử lý sự cố
| Triệu chứng | Kiểm tra | Cách xử lý |
|---|---|---|
| Không có email | Mailhog/SMTP, log email-sender-service | Kiểm tra DLQ, sửa nguyên nhân, đẩy lại message từ DLQ |
| Có user nhưng không có sự kiện | Log `publish_failed` ở user-service | Gửi bù từ danh sách user bị lỗi (cần outbox ở v2) |
| Email trùng | Log `duplicate_skipped` | Kiểm tra idempotency store (Redis ở v2) |
| Consumer lag tăng | Log notification-service, trạng thái RabbitMQ | Scale số instance (tối đa bằng số partition) |

## 8. Biên bản giới hạn của v1
- Dữ liệu user lưu in-memory (mất khi restart).
- Idempotency in-memory.
- Chưa có rate limit/xác thực.
- Chưa có Transactional Outbox.
