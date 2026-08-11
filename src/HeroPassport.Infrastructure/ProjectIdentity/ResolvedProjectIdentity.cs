namespace HeroPassport.Infrastructure.ProjectIdentity;

public sealed record ResolvedProjectIdentity(
    string Kind,
    string Scope,
    string DisplayName,
    string WorkspaceFingerprint,
    string IdentityVersion);
