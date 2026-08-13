namespace HeroPassport.Application.Runtime;

public sealed class HeroPassportException : InvalidOperationException
{
    public HeroPassportException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
