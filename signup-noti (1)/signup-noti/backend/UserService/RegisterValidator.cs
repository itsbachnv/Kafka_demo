using System.Text.RegularExpressions;

namespace UserService;

public static class RegisterValidator
{
    private static readonly Regex EmailRegex =
        new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

    public static List<string> Validate(RegisterRequest? request)
    {
        var errors = new List<string>();

        if (request?.Email is null || !EmailRegex.IsMatch(request.Email.Trim()))
            errors.Add("email không hợp lệ");

        if (string.IsNullOrWhiteSpace(request?.FullName))
            errors.Add("fullName không được rỗng");

        if (request?.Password is null || request.Password.Length < 8)
            errors.Add("password phải có ít nhất 8 ký tự");

        return errors;
    }
}
