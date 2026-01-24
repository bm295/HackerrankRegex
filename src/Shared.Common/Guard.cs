namespace Shared.Common;

public static class Guard
{
    public static T NotNull<T>(T? value, string name) where T : class =>
        value ?? throw new ArgumentNullException(name);

    public static string NotEmpty(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value cannot be empty", name) : value;

    public static void Against(bool condition, string message, string name)
    {
        if (condition)
        {
            throw new ArgumentException(message, name);
        }
    }
}
