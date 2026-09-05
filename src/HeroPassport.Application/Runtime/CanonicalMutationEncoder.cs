using HeroPassport.Domain.Primitives;
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
        return Hash(stream);
    }

    public static byte[] HashCreateHero(string name)
    {
        using var stream = NewStream();
        WriteStringFrame(stream, 0x00, "create_hero");
        WriteStringFrame(stream, 0x01, name);
        return Hash(stream);
    }

    public static byte[] HashStartQuest(
        ProjectId projectId,
        HeroId heroId,
        string questType,
        string title,
        string goal)
    {
        using var stream = NewStream();
        WriteStringFrame(stream, 0x00, "start_quest");
        WriteStringFrame(stream, 0x01, projectId.ToString());
        WriteStringFrame(stream, 0x02, heroId.ToString());
        WriteStringFrame(stream, 0x03, questType);
        WriteStringFrame(stream, 0x04, title);
        WriteStringFrame(stream, 0x05, goal);
        return Hash(stream);
    }

    public static byte[] HashFinishQuest(
        QuestId questId,
        string result,
        string summary,
        bool testsMentioned,
        int scopeViolations,
        int userCorrections,
        string buildStatus,
        string buildEvidence,
        string testsStatus,
        string testsEvidence,
        IReadOnlyList<string> skillsUsed)
    {
        using var stream = NewStream();
        WriteStringFrame(stream, 0x00, "finish_quest");
        WriteStringFrame(stream, 0x01, questId.ToString());
        WriteStringFrame(stream, 0x02, result);
        WriteStringFrame(stream, 0x03, summary);
        WriteBoolFrame(stream, 0x04, testsMentioned);
        WriteInt32Frame(stream, 0x05, scopeViolations);
        WriteInt32Frame(stream, 0x06, userCorrections);
        WriteStringFrame(stream, 0x07, buildStatus);
        WriteStringFrame(stream, 0x08, buildEvidence);
        WriteStringFrame(stream, 0x09, testsStatus);
        WriteStringFrame(stream, 0x0A, testsEvidence);
        WriteStringListFrame(stream, 0x0B, skillsUsed);
        return Hash(stream);
    }

    private static MemoryStream NewStream()
    {
        var stream = new MemoryStream(256);
        stream.Write(Prefix);
        return stream;
    }

    private static byte[] Hash(MemoryStream stream) =>
        SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));

    private static void WriteStringFrame(Stream stream, byte tag, string value) =>
        WriteFrame(stream, tag, Encoding.UTF8.GetBytes(value));

    private static void WriteBoolFrame(Stream stream, byte tag, bool value) =>
        WriteFrame(stream, tag, [value ? (byte)0x01 : (byte)0x00]);

    private static void WriteInt32Frame(Stream stream, byte tag, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        WriteFrame(stream, tag, bytes);
    }

    private static void WriteStringListFrame(Stream stream, byte tag, IReadOnlyList<string> values)
    {
        using var value = new MemoryStream();
        value.WriteByte(checked((byte)values.Count));
        foreach (var item in values)
        {
            var bytes = Encoding.UTF8.GetBytes(item);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
            value.Write(length);
            value.Write(bytes);
        }

        WriteFrame(stream, tag, value.GetBuffer().AsSpan(0, checked((int)value.Length)));
    }

    private static void WriteFrame(Stream stream, byte tag, ReadOnlySpan<byte> value)
    {
        stream.WriteByte(tag);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)value.Length));
        stream.Write(length);
        stream.Write(value);
    }
}
