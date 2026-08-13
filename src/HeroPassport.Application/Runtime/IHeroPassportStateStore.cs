namespace HeroPassport.Application.Runtime;

public interface IHeroPassportStateStore
{
    Task<BootstrapResult> BootstrapAsync(
        BootstrapStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
