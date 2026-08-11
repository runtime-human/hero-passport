namespace HeroPassport.Application.Runtime;

public sealed class HeroPassportException : Exception
{
    public HeroPassportException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
