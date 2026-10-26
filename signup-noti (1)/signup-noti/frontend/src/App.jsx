import { useState } from 'react';
import { registerUser } from './api.js';
import { validate } from './validate.js';

const initialForm = { fullName: '', email: '', password: '' };

export default function App() {
  const [form, setForm] = useState(initialForm);
  const [fieldErrors, setFieldErrors] = useState({});
  const [serverErrors, setServerErrors] = useState([]);
  const [submitting, setSubmitting] = useState(false);
  const [successEmail, setSuccessEmail] = useState(null);

  const onChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    setServerErrors([]);
    setSuccessEmail(null);

    const errors = validate(form);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    try {
      const result = await registerUser(form);
      if (result.ok) {
        setSuccessEmail(result.data?.email ?? form.email);
        setForm(initialForm);
      } else {
        setServerErrors(result.errors);
      }
    } catch {
      setServerErrors(['Không kết nối được tới máy chủ. Vui lòng kiểm tra mạng và thử lại.']);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <main className="page">
      <form className="card" onSubmit={onSubmit} noValidate aria-labelledby="title">
        <h1 id="title">Tạo tài khoản</h1>
        <p className="subtitle">Đăng ký để nhận email chào mừng.</p>

        {successEmail && (
          <div className="alert success" role="status">
            Đăng ký thành công! Chúng tôi đã gửi email chào mừng tới <strong>{successEmail}</strong>.
          </div>
        )}

        {serverErrors.length > 0 && (
          <div className="alert error" role="alert">
            <ul>
              {serverErrors.map((msg) => (
                <li key={msg}>{msg}</li>
              ))}
            </ul>
          </div>
        )}

        <label htmlFor="fullName">Họ và tên</label>
        <input
          id="fullName"
          name="fullName"
          type="text"
          autoComplete="name"
          value={form.fullName}
          onChange={onChange}
          aria-invalid={Boolean(fieldErrors.fullName)}
          aria-describedby={fieldErrors.fullName ? 'fullName-error' : undefined}
        />
        {fieldErrors.fullName && (
          <span id="fullName-error" className="field-error">
            {fieldErrors.fullName}
          </span>
        )}

        <label htmlFor="email">Email</label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          value={form.email}
          onChange={onChange}
          aria-invalid={Boolean(fieldErrors.email)}
          aria-describedby={fieldErrors.email ? 'email-error' : undefined}
        />
        {fieldErrors.email && (
          <span id="email-error" className="field-error">
            {fieldErrors.email}
          </span>
        )}

        <label htmlFor="password">Mật khẩu</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="new-password"
          value={form.password}
          onChange={onChange}
          aria-invalid={Boolean(fieldErrors.password)}
          aria-describedby={fieldErrors.password ? 'password-error' : undefined}
        />
        {fieldErrors.password && (
          <span id="password-error" className="field-error">
            {fieldErrors.password}
          </span>
        )}

        <button type="submit" disabled={submitting}>
          {submitting ? 'Đang đăng ký…' : 'Đăng ký'}
        </button>
      </form>
    </main>
  );
}
