namespace HeroPassport.Domain.Primitives;

public static class JsonSafeInteger
{
    public const long Maximum = 9_007_199_254_740_991L;

    public static long Require(long value)
    {
        if (value < 0 || value > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between 0 and {Maximum}.");
        }

        return value;
    }
}
