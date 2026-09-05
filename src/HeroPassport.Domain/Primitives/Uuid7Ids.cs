namespace HeroPassport.Domain.Primitives;

public readonly record struct HeroId(Guid Value)
{
    public static HeroId New() => new(Uuid7Value.New());
    public static HeroId Parse(string value) => new(Uuid7Value.Parse(value));
    public override string ToString() => Uuid7Value.Format(Value);
}

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Uuid7Value.New());
    public static ProjectId Parse(string value) => new(Uuid7Value.Parse(value));
    public override string ToString() => Uuid7Value.Format(Value);
}

public readonly record struct QuestId(Guid Value)
{
    public static QuestId New() => new(Uuid7Value.New());
    public static QuestId Parse(string value) => new(Uuid7Value.Parse(value));
    public override string ToString() => Uuid7Value.Format(Value);
}

public readonly record struct QuestReportId(Guid Value)
{
    public static QuestReportId New() => new(Uuid7Value.New());
    public static QuestReportId Parse(string value) => new(Uuid7Value.Parse(value));
    public override string ToString() => Uuid7Value.Format(Value);
}

public readonly record struct XpEventId(Guid Value)
{
    public static XpEventId New() => new(Uuid7Value.New());
    public static XpEventId Parse(string value) => new(Uuid7Value.Parse(value));
    public override string ToString() => Uuid7Value.Format(Value);
}

public readonly record struct MutationRequestId(Guid Value)
{
    public static MutationRequestId New() => new(Uuid7Value.New());
    public static MutationRequestId Parse(string value) => new(Uuid7Value.Parse(value));
    public override string ToString() => Uuid7Value.Format(Value);
}

internal static class Uuid7Value
{
    public static Guid New() => Guid.CreateVersion7();

    public static Guid Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Guid.TryParseExact(value, "D", out var parsed) ||
            !string.Equals(Format(parsed), value, StringComparison.Ordinal) ||
            value.Length != 36 ||
            value[14] != '7' ||
            value[19] is not ('8' or '9' or 'a' or 'b'))
        {
            throw new FormatException("Value must be a lowercase canonical UUIDv7.");
        }

        return parsed;
    }

    public static string Format(Guid value) => value.ToString("D");
}
