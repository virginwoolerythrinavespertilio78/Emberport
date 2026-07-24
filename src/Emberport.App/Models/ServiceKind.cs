namespace Emberport.Models;

public enum ServiceKind
{
    Php,
    Apache,
    MySql,
    Redis,
}

public static class ServiceKindExtensions
{
    public static string ToDisplayName(this ServiceKind kind) => kind switch
    {
        ServiceKind.Php => "PHP",
        ServiceKind.Apache => "Apache",
        ServiceKind.MySql => "MySQL",
        ServiceKind.Redis => "Redis",
        _ => kind.ToString(),
    };
}