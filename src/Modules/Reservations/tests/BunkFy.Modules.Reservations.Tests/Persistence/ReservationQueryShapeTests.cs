namespace BunkFy.Modules.Reservations.Tests.Persistence;

using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationQueryShapeTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Aggregate_graph_explicitly_uses_split_query_execution(
        string provider)
    {
        using ReservationsDbContext dbContext = CreateRelationalContext(provider);

        string sql = dbContext.Reservations
            .WithAggregateGraph()
            .Where(reservation => reservation.Id == Guid.NewGuid())
            .ToQueryString();

        Assert.Contains("split-query", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Creation_replay_graph_excludes_guest_history(string provider)
    {
        using ReservationsDbContext dbContext = CreateRelationalContext(provider);

        string sql = dbContext.Reservations
            .WithCreationReplayGraph()
            .Where(reservation => reservation.Id == Guid.NewGuid())
            .ToQueryString();

        Assert.Contains("requested_inventory_units", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reservation_guests", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ReservationsDbContext CreateRelationalContext(string provider)
    {
        DbContextOptionsBuilder<ReservationsDbContext> builder =
            new();
        switch (provider)
        {
            case "PostgreSql":
                builder.UseNpgsql(
                    "Host=localhost;Database=bunkfy_query_shape;" +
                    "Username=query_shape;Password=not-used");
                break;
            case "SqlServer":
                builder.UseSqlServer(
                    "Server=localhost;Database=BunkFyQueryShape;" +
                    "User Id=query-shape;Password=not-used;" +
                    "TrustServerCertificate=True");
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(provider),
                    provider,
                    "Unsupported query-shape test provider.");
        }

        builder.ConfigureWarnings(warnings => warnings.Throw(
            RelationalEventId.MultipleCollectionIncludeWarning));
        return new(builder.Options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-query-shape";
    }
}
