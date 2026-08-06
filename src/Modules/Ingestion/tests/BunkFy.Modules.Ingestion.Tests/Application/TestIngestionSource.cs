namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using Gma.Framework.Scoping;

internal static class TestIngestionSource
{
    public static IngestionSourceMutationCoordinator Create(
        IngestionDbContext dbContext,
        IScopeContext scopeContext) => new(
            new IngestionSourceGraphLocator(dbContext),
            new IngestionSourceGraphLock(dbContext),
            TestIngestionExecution.Create(
                new AdapterConnectionRepository(dbContext),
                scopeContext: scopeContext),
            scopeContext);
}
