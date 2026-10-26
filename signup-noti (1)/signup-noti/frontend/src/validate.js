const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Kiểm tra phía client, khớp với luật của user-service. Trả về object lỗi theo từng trường. */
export function validate({ fullName, email, password }) {
  const errors = {};
  if (!fullName || fullName.trim() === '') errors.fullName = 'Vui lòng nhập họ tên';
  if (!email || !EMAIL_RE.test(email.trim())) errors.email = 'Email không hợp lệ';
  if (!password || password.length < 8) errors.password = 'Mật khẩu phải có ít nhất 8 ký tự';
  return errors;
}
