import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App.jsx';

const jsonResponse = (status, body) => ({
  ok: status >= 200 && status < 300,
  status,
  json: async () => body,
});

async function fillAndSubmit(user, { fullName = 'Nguyen Van A', email = 'a@example.com', password = 'secret123' } = {}) {
  if (fullName) await user.type(screen.getByLabelText('Họ và tên'), fullName);
  if (email) await user.type(screen.getByLabelText('Email'), email);
  if (password) await user.type(screen.getByLabelText('Mật khẩu'), password);
  await user.click(screen.getByRole('button', { name: 'Đăng ký' }));
}

describe('App (form đăng ký)', () => {
  beforeEach(() => {
    globalThis.fetch = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('F-01 hiển thị đủ 3 trường và nút đăng ký', () => {
    render(<App />);
    expect(screen.getByLabelText('Họ và tên')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
    expect(screen.getByLabelText('Mật khẩu')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Đăng ký' })).toBeInTheDocument();
  });

  it('F-02 form trống: hiển thị lỗi từng trường và không gọi API', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole('button', { name: 'Đăng ký' }));

    expect(screen.getByText('Vui lòng nhập họ tên')).toBeInTheDocument();
    expect(screen.getByText('Email không hợp lệ')).toBeInTheDocument();
    expect(screen.getByText('Mật khẩu phải có ít nhất 8 ký tự')).toBeInTheDocument();
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it('F-03 mật khẩu ngắn bị chặn ở client', async () => {
    const user = userEvent.setup();
    render(<App />);

    await fillAndSubmit(user, { password: '1234567' });

    expect(screen.getByText('Mật khẩu phải có ít nhất 8 ký tự')).toBeInTheDocument();
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it('F-04 đăng ký thành công: gọi API đúng dữ liệu, báo thành công và xoá form', async () => {
    globalThis.fetch.mockResolvedValue(
      jsonResponse(201, { userId: 'u1', email: 'a@example.com', fullName: 'Nguyen Van A' }),
    );
    const user = userEvent.setup();
    render(<App />);

    await fillAndSubmit(user);

    expect(await screen.findByRole('status')).toHaveTextContent('Đăng ký thành công');
    expect(screen.getByRole('status')).toHaveTextContent('a@example.com');

    expect(globalThis.fetch).toHaveBeenCalledTimes(1);
    const [url, options] = globalThis.fetch.mock.calls[0];
    expect(url).toBe('/api/users/register');
    expect(options.method).toBe('POST');
    expect(JSON.parse(options.body)).toEqual({
      email: 'a@example.com',
      fullName: 'Nguyen Van A',
      password: 'secret123',
    });

    expect(screen.getByLabelText('Email')).toHaveValue('');
    expect(screen.getByLabelText('Mật khẩu')).toHaveValue('');
  });

  it('F-05 email đã tồn tại (409): hiển thị thông báo từ máy chủ', async () => {
    globalThis.fetch.mockResolvedValue(jsonResponse(409, { error: 'Email đã được đăng ký' }));
    const user = userEvent.setup();
    render(<App />);

    await fillAndSubmit(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Email đã được đăng ký');
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('F-06 máy chủ trả 400 kèm errors: hiển thị tất cả lỗi', async () => {
    globalThis.fetch.mockResolvedValue(
      jsonResponse(400, { errors: ['email không hợp lệ', 'password phải có ít nhất 8 ký tự'] }),
    );
    const user = userEvent.setup();
    render(<App />);

    await fillAndSubmit(user);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('email không hợp lệ');
    expect(alert).toHaveTextContent('password phải có ít nhất 8 ký tự');
  });

  it('F-07 lỗi mạng: hiển thị thông báo không kết nối được', async () => {
    globalThis.fetch.mockRejectedValue(new TypeError('Failed to fetch'));
    const user = userEvent.setup();
    render(<App />);

    await fillAndSubmit(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Không kết nối được tới máy chủ');
  });

  it('F-08 đang gửi: nút bị khoá để tránh gửi 2 lần', async () => {
    let resolveFetch;
    globalThis.fetch.mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve;
      }),
    );
    const user = userEvent.setup();
    render(<App />);

    await fillAndSubmit(user);

    const button = await screen.findByRole('button', { name: /Đang đăng ký/ });
    expect(button).toBeDisabled();

    resolveFetch(jsonResponse(201, { userId: 'u1', email: 'a@example.com', fullName: 'A' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Đăng ký' })).toBeEnabled());
  });
});
