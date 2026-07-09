namespace Integration.Tests.Support;

using System.Data.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

internal static class AuthTestContainers
{
    public static IContainer CreateNatsContainer() =>
        new ContainerBuilder("nats:2.10-alpine")
            .WithPortBinding(4222, assignRandomHostPort: true)
            .WithCommand("-js")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(4222))
            .Build();

    public static string GetNatsConnectionString(IContainer container) =>
        $"nats://localhost:{container.GetMappedPublicPort(4222)}";

    public static string UseDatabase(string connectionString, string databaseName)
    {
        DbConnectionStringBuilder builder = new()
        {
            ConnectionString = connectionString,
        };

        builder["Database"] = databaseName;
        return builder.ConnectionString;
    }
}
