namespace FriendShip.Domain.Moderation;

public class ErrorLogEntry
{
    public long Id { get; private set; }
    public DateTime At { get; private set; }
    public string Level { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public string? Exception { get; private set; }

    private ErrorLogEntry() { }

    public static ErrorLogEntry Create(DateTime at, string level, string category, string message, string? exception) => new()
    {
        At = at,
        Level = Trim(level, 20)!,
        Category = Trim(category, 200)!,
        Message = Trim(message, 2000)!,
        Exception = Trim(exception, 8000)
    };

    private static string? Trim(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
