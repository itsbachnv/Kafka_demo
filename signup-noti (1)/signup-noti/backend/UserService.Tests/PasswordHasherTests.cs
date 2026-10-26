using Xunit;

namespace UserService.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_is_not_plaintext_and_verifies()
    {
        var hash = _hasher.Hash("secret123");

        Assert.DoesNotContain("secret123", hash);
        Assert.True(_hasher.Verify("secret123", hash));
        Assert.False(_hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Same_password_produces_different_hashes_because_of_salt()
    {
        Assert.NotEqual(_hasher.Hash("secret123"), _hasher.Hash("secret123"));
    }
}
