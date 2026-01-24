namespace Shared.Common;

public sealed record Error(string Code, string Message, string? Details = null)
{
    public override string ToString() => $"{Code}: {Message}{(Details is null ? string.Empty : $" ({Details})")}";
}
