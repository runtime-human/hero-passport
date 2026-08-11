using HeroPassport.Domain.Primitives;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace HeroPassport.Infrastructure.ProjectIdentity;

public sealed class ProjectIdentityResolver
{
    public const string IdentityVersion = "project-identity/1";
    private const int GitOutputLimit = 8 * 1024;
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] GitEnvironmentOverrides =
    [
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_COMMON_DIR",
        "GIT_INDEX_FILE",
        "GIT_OBJECT_DIRECTORY",
        "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_CEILING_DIRECTORIES",
    ];

    public async Task<ResolvedProjectIdentity> ResolveAsync(
        string? explicitProjectRoot,
        string currentDirectory,
        ReadOnlyMemory<byte> installationSalt,
        CancellationToken cancellationToken = default)
    {
        if (installationSalt.Length != 32)
        {
            throw new ArgumentException("Project identity salt must be exactly 32 bytes.", nameof(installationSalt));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);
        var isExplicit = explicitProjectRoot is not null;
        if (isExplicit && string.IsNullOrWhiteSpace(explicitProjectRoot))
        {
            throw new ProjectIdentityException("HP310", "Project binding is invalid.");
        }

        var bindingStart = NormalizeExistingDirectory(isExplicit ? explicitProjectRoot! : currentDirectory);
        var gitMarkerPresent = HasGitMarkerInAncestors(bindingStart);

        var insideWorkTree = await RunGitAsync(bindingStart, cancellationToken, "rev-parse", "--is-inside-work-tree").ConfigureAwait(false);
        if (!insideWorkTree.Started)
        {
            if (gitMarkerPresent)
            {
                throw new ProjectIdentityException("HP312", "Git is required to resolve this repository binding.");
            }

            return ResolveStandalone(bindingStart, installationSalt.Span);
        }

        if (insideWorkTree.ExitCode != 0)
        {
            if (gitMarkerPresent)
            {
                throw new ProjectIdentityException("HP311", "Git repository metadata is unavailable or unsafe.");
            }

            return ResolveStandalone(bindingStart, installationSalt.Span);
        }

        var bare = await RequireGitSuccessAsync(bindingStart, cancellationToken, "--is-bare-repository").ConfigureAwait(false);
        if (string.Equals(bare, "true", StringComparison.Ordinal))
        {
            throw new ProjectIdentityException("HP313", "Bare Git repositories are unsupported.");
        }

        if (!string.Equals(insideWorkTree.StandardOutput.Trim(), "true", StringComparison.Ordinal))
        {
            return ResolveStandalone(bindingStart, installationSalt.Span);
        }

        var topLevel = await RequireGitSuccessAsync(bindingStart, cancellationToken, "--path-format=absolute", "--show-toplevel").ConfigureAwait(false);
        var commonDirectory = await RequireGitSuccessAsync(bindingStart, cancellationToken, "--path-format=absolute", "--git-common-dir").ConfigureAwait(false);
        var prefix = isExplicit
            ? await RequireGitSuccessAsync(bindingStart, cancellationToken, "--show-prefix").ConfigureAwait(false)
            : string.Empty;

        _ = await RequireGitSuccessAsync(bindingStart, cancellationToken, "--show-superproject-working-tree").ConfigureAwait(false);

        var anchor = Path.TrimEndingDirectorySeparator(Path.GetFullPath(commonDirectory));
        var scope = isExplicit ? NormalizeScope(prefix) : ".";
        var displayName = NormalizeDisplayName(Path.GetFileName(Path.TrimEndingDirectorySeparator(topLevel)));
        var fingerprint = CreateFingerprint(installationSalt.Span, $"{IdentityVersion}\0git\0{anchor}\0{scope}");

        return new ResolvedProjectIdentity("git", scope, displayName, fingerprint, IdentityVersion);
    }

    private static ResolvedProjectIdentity ResolveStandalone(string bindingStart, ReadOnlySpan<byte> installationSalt)
    {
        var resolved = ResolveFinalDirectoryLink(bindingStart);
        var displayName = NormalizeDisplayName(Path.GetFileName(Path.TrimEndingDirectorySeparator(resolved)));
        var fingerprint = CreateFingerprint(installationSalt, $"{IdentityVersion}\0standalone\0{resolved}\0.");
        return new ResolvedProjectIdentity("standalone", ".", displayName, fingerprint, IdentityVersion);
    }

    private static string NormalizeExistingDirectory(string path)
    {
        try
        {
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (!Directory.Exists(fullPath))
            {
                throw new ProjectIdentityException("HP310", "Project binding is invalid.");
            }

            return fullPath;
        }
        catch (ProjectIdentityException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ProjectIdentityException("HP310", "Project binding is invalid.");
        }
    }

    private static string ResolveFinalDirectoryLink(string path)
    {
        var info = new DirectoryInfo(path);
        var target = info.ResolveLinkTarget(returnFinalTarget: true);
        return target is null
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
    }

    private static string NormalizeScope(string prefix)
    {
        var normalized = prefix.Trim().Replace('\\', '/').Trim('/');
        return normalized.Length == 0 ? "." : normalized;
    }

    private static string NormalizeDisplayName(string value) => SafeTextV1.Normalize(value, 1, 120);

    private static bool HasGitMarkerInAncestors(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            var marker = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string> RequireGitSuccessAsync(
        string bindingStart,
        CancellationToken cancellationToken,
        params string[] revParseArguments)
    {
        var arguments = new string[revParseArguments.Length + 1];
        arguments[0] = "rev-parse";
        Array.Copy(revParseArguments, 0, arguments, 1, revParseArguments.Length);
        var result = await RunGitAsync(bindingStart, cancellationToken, arguments).ConfigureAwait(false);
        if (!result.Started || result.ExitCode != 0)
        {
            throw new ProjectIdentityException("HP311", "Git repository metadata is unavailable or unsafe.");
        }

        return result.StandardOutput.Trim();
    }

    private static async Task<GitResult> RunGitAsync(
        string bindingStart,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = CreateGitStartInfo(bindingStart, arguments),
        };

        try
        {
            if (!process.Start())
            {
                return GitResult.NotStarted;
            }
        }
        catch (Win32Exception)
        {
            return GitResult.NotStarted;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(GitTimeout);
        var token = timeoutSource.Token;

        try
        {
            var stdoutTask = ReadBoundedAsync(process.StandardOutput, token);
            var stderrTask = ReadBoundedAsync(process.StandardError, token);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return new GitResult(true, process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new ProjectIdentityException("HP311", "Git repository metadata query timed out.");
        }
        catch (ProjectIdentityException)
        {
            TryKill(process);
            throw;
        }
    }

    private static ProcessStartInfo CreateGitStartInfo(string bindingStart, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(bindingStart);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var variable in GitEnvironmentOverrides)
        {
            startInfo.Environment.Remove(variable);
        }

        return startInfo;
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        var builder = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return builder.ToString();
            }

            if (builder.Length + read > GitOutputLimit)
            {
                throw new ProjectIdentityException("HP311", "Git repository metadata output exceeded the safe limit.");
            }

            builder.Append(buffer, 0, read);
        }
    }

    private static string CreateFingerprint(ReadOnlySpan<byte> salt, string canonicalMaterial)
    {
        var material = Encoding.UTF8.GetBytes(canonicalMaterial);
        var input = new byte[salt.Length + 1 + material.Length];
        salt.CopyTo(input);
        material.CopyTo(input.AsSpan(salt.Length + 1));
        var hash = SHA256.HashData(input);
        const string alphabet = "0123456789abcdef";
        return string.Create(hash.Length * 2, hash, static (characters, bytes) =>
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = alphabet[bytes[index] >> 4];
                characters[(index * 2) + 1] = alphabet[bytes[index] & 0x0F];
            }
        });
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record GitResult(bool Started, int ExitCode, string StandardOutput, string StandardError)
    {
        public static GitResult NotStarted { get; } = new(false, -1, string.Empty, string.Empty);
    }
}
