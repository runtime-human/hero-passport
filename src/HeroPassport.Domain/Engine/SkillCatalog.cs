namespace HeroPassport.Domain.Engine;

public static class SkillCatalog
{
    private static readonly string[] OrderedKeys =
    [
        "coding",
        "testing_awareness",
        "scope_control",
        "documentation",
        "tool_use",
        "planning",
        "research",
        "debugging",
        "review",
        "maintenance",
    ];

    public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(OrderedKeys);

    public static bool Contains(string? skillKey)
    {
        if (skillKey is null)
        {
            return false;
        }

        foreach (var candidate in OrderedKeys)
        {
            if (string.Equals(candidate, skillKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
