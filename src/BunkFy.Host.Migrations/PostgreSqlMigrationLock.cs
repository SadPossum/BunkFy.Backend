namespace BunkFy.Host.Migrations;

using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

public sealed class PostgreSqlMigrationLock : IAsyncDisposable
{
    // Product-wide and stable across releases; PostgreSQL scopes it to the target database.
    public const long MigrationLockKey = 4777556522770389321L;
    private readonly DbConnection connection;
    private bool disposed;

    private PostgreSqlMigrationLock(DbConnection connection)
    {
        this.connection = connection;
        this.DatabaseTargetSha256 = ComputeDatabaseTargetSha256(
            connection.DataSource,
            connection.Database);
    }

    public string DatabaseTargetSha256 { get; }

    public static async Task<PostgreSqlMigrationLock> AcquireAsync(
        DbContext lockContext,
        TimeSpan acquireTimeout,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lockContext);
        if (!string.Equals(
                lockContext.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The BunkFy migration lock requires the PostgreSQL provider.");
        }

        DbConnection connection = lockContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Closed)
        {
            throw new InvalidOperationException(
                "The migration lock context connection must be closed before acquisition.");
        }

        using CancellationTokenSource timeout = new(acquireTimeout);
        using CancellationTokenSource linked = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await connection.OpenAsync(linked.Token).ConfigureAwait(false);
            while (true)
            {
                if (await TryAcquireAsync(connection, acquireTimeout, linked.Token)
                        .ConfigureAwait(false))
                {
                    try
                    {
                        return new PostgreSqlMigrationLock(connection);
                    }
                    catch
                    {
                        await ReleaseAsync(connection).ConfigureAwait(false);
                        throw;
                    }
                }

                await Task.Delay(retryDelay, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            await connection.CloseAsync().ConfigureAwait(false);
            throw new TimeoutException(
                "Timed out while waiting for the BunkFy PostgreSQL migration lock.");
        }
        catch
        {
            await connection.CloseAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static string ComputeDatabaseTargetSha256(
        string dataSource,
        string database)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        string canonical = string.Concat(
            "bunkfy-postgresql-target-v1\n",
            dataSource.Trim().ToLowerInvariant(),
            "\n",
            database.Trim(),
            "\n");
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        if (this.connection.State != ConnectionState.Closed)
        {
            try
            {
                // Pooled logical close may retain the physical PostgreSQL session.
                await ReleaseAsync(this.connection).ConfigureAwait(false);
            }
            finally
            {
                await this.connection.CloseAsync().ConfigureAwait(false);
            }
        }

        GC.SuppressFinalize(this);
    }

    private static async Task<bool> TryAcquireAsync(
        DbConnection connection,
        TimeSpan acquireTimeout,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT pg_try_advisory_lock(@bunkfy_migration_lock_key);";
        command.CommandTimeout = Math.Max(
            1,
            (int)Math.Ceiling(acquireTimeout.TotalSeconds));
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "bunkfy_migration_lock_key";
        parameter.Value = MigrationLockKey;
        command.Parameters.Add(parameter);

        object? result = await command.ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);
        return result is true;
    }

    private static async Task ReleaseAsync(DbConnection connection)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT pg_advisory_unlock(@bunkfy_migration_lock_key);";
        command.CommandTimeout = 5;
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "bunkfy_migration_lock_key";
        parameter.Value = MigrationLockKey;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }
}
