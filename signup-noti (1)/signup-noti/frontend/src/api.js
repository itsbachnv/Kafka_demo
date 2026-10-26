const GENERIC_ERROR = 'Có lỗi xảy ra, vui lòng thử lại sau.';

/**
 * Gọi POST /api/users/register.
 * Trả về { ok: true, data } hoặc { ok: false, status, errors: string[] }.
 * Lỗi mạng sẽ được ném ra (throw) để component hiển thị thông báo riêng.
 */
export async function registerUser({ email, fullName, password }) {
  const res = await fetch('/api/users/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, fullName, password }),
  });

  let data = null;
  try {
    data = await res.json();
  } catch {
    // body rỗng hoặc không phải JSON
  }

  if (res.ok) return { ok: true, data };

  const errors = Array.isArray(data?.errors)
    ? data.errors
    : data?.error
      ? [data.error]
      : [GENERIC_ERROR];

  return { ok: false, status: res.status, errors };
}
