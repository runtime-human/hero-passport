namespace HeroPassport.Infrastructure.ProjectIdentity;

public sealed class ProjectIdentityException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
