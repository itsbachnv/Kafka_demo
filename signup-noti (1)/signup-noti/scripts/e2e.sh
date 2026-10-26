#!/usr/bin/env bash
# E2E: cần `docker compose up -d --build` đã chạy xong.
set -euo pipefail

API=${API:-http://localhost:3000}
MAILHOG=${MAILHOG:-http://localhost:8025}
STAMP=$(date +%s)
EMAIL="e2e-$STAMP@example.com"

echo "-> Đăng ký $EMAIL"
code=$(curl -s -o /tmp/e2e-resp.json -w '%{http_code}' -X POST "$API/api/users/register" \
  -H 'content-type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"fullName\":\"E2E User\",\"password\":\"secret123\"}")
[ "$code" = "201" ] || { echo "FAIL: mong đợi 201, nhận $code"; cat /tmp/e2e-resp.json; exit 1; }

echo "-> Đăng ký trùng phải trả 409"
code=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/users/register" \
  -H 'content-type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"fullName\":\"E2E User\",\"password\":\"secret123\"}")
[ "$code" = "409" ] || { echo "FAIL: mong đợi 409, nhận $code"; exit 1; }

echo "-> Chờ email trong Mailhog (tối đa 30s)"
for i in $(seq 1 30); do
  count=$(curl -s "$MAILHOG/api/v2/search?kind=to&query=$EMAIL" | grep -o '"total":[0-9]*' | cut -d: -f2 || echo 0)
  if [ "${count:-0}" = "1" ]; then
    echo "PASS: nhận đúng 1 email cho $EMAIL"
    exit 0
  fi
  if [ "${count:-0}" -gt 1 ]; then
    echo "FAIL: nhận $count email (bị trùng)"; exit 1
  fi
  sleep 1
done
echo "FAIL: không thấy email sau 30s"
exit 1
