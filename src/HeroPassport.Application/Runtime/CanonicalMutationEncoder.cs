using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HeroPassport.Application.Runtime;

public static class CanonicalMutationEncoder
{
    private static ReadOnlySpan<byte> Prefix => [0x48, 0x50, 0x4D, 0x41, 0x01];

    public static byte[] HashBootstrap(
        string locale,
        string heroName,
        string presentationStyle,
        bool autoStartQuest,
        bool autoFinishQuest)
    {
        using var stream = NewStream();
        WriteStringFrame(stream, 0x00, "bootstrap");
        WriteStringFrame(stream, 0x01, locale);
        WriteStringFrame(stream, 0x02, heroName);
        WriteStringFrame(stream, 0x03, presentationStyle);
        WriteBoolFrame(stream, 0x04, autoStartQuest);
        WriteBoolFrame(stream, 0x05, autoFinishQuest);
        return SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    public static byte[] HashCreateHero(string name)
    {
        using var stream = NewStream();
        WriteStringFrame(stream, 0x00, "create_hero");
        WriteStringFrame(stream, 0x01, name);
        return SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    private static MemoryStream NewStream()
    {
        var stream = new MemoryStream(128);
        stream.Write(Prefix);
        return stream;
    }

    private static void WriteStringFrame(Stream stream, byte tag, string value) =>
        WriteFrame(stream, tag, Encoding.UTF8.GetBytes(value));

    private static void WriteBoolFrame(Stream stream, byte tag, bool value) =>
        WriteFrame(stream, tag, [value ? (byte)0x01 : (byte)0x00]);

    private static void WriteFrame(Stream stream, byte tag, ReadOnlySpan<byte> value)
    {
        stream.WriteByte(tag);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)value.Length));
        stream.Write(length);
        stream.Write(value);
    }
}
