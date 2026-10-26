using System.Collections.Concurrent;

namespace UserService;

public interface IUserRepository
{
    /// <summary>Thêm user; trả false nếu email đã tồn tại (thao tác nguyên tử).</summary>
    Task<bool> TryAddAsync(User user);
}

/// <summary>Lưu trong bộ nhớ, chỉ dùng cho bản demo. Thay bằng PostgreSQL khi lên production.</summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _byEmail = new();

    public Task<bool> TryAddAsync(User user) => Task.FromResult(_byEmail.TryAdd(user.Email, user));
}
