using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HeroPassport.Infrastructure.Persistence;

internal sealed class SqliteConnectionPolicyInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SqliteConnectionPolicy.Apply(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        return SqliteConnectionPolicy.ApplyAsync(connection, cancellationToken);
    }
}
