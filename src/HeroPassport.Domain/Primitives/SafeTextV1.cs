using System.Text;

namespace HeroPassport.Domain.Primitives;

public static class SafeTextV1
{
    public static string Normalize(string value, int minimumScalars, int maximumScalars)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (minimumScalars < 0 || maximumScalars < minimumScalars)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScalars));
        }

        ValidateUtf16(value);
        var normalized = value.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var rune in normalized.EnumerateRunes())
        {
            var scalar = rune.Value;
            var whitespace = Rune.IsWhiteSpace(rune);

            if (IsRejectedBidiControl(scalar) || (IsC0OrC1Control(scalar) && !whitespace))
            {
                throw new ArgumentException("Text contains a disallowed control character.", nameof(value));
            }

            if (whitespace)
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(rune.ToString());
        }

        var result = builder.ToString();
        var scalarCount = result.EnumerateRunes().Count();
        if (scalarCount < minimumScalars || scalarCount > maximumScalars)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Text must contain between {minimumScalars} and {maximumScalars} Unicode scalars after normalization.");
        }

        return result;
    }

    private static void ValidateUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var codeUnit = value[index];
            if (char.IsHighSurrogate(codeUnit))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw new ArgumentException("Text contains an unpaired UTF-16 surrogate.", nameof(value));
                }

                index++;
                continue;
            }

            if (char.IsLowSurrogate(codeUnit))
            {
                throw new ArgumentException("Text contains an unpaired UTF-16 surrogate.", nameof(value));
            }
        }
    }

    private static bool IsC0OrC1Control(int scalar) => scalar <= 0x1F || scalar is >= 0x7F and <= 0x9F;

    private static bool IsRejectedBidiControl(int scalar) =>
        scalar is 0x061C or 0x200E or 0x200F or
        (>= 0x202A and <= 0x202E) or
        (>= 0x2066 and <= 0x2069);
}
