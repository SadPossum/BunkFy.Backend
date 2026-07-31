namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionNotificationSourceLinkResolver(
    IngestionDbContext dbContext)
    : IIngestionNotificationSourceLinkResolver
{
    public Task<IngestionNotificationSourceLink?> ResolveAsync(
        string scopeId,
        Guid propertyId,
        Guid connectionId,
        Guid operationId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        string normalizedScopeId =
            ScopeIds.Normalize(scopeId, nameof(scopeId));
        Require(propertyId, nameof(propertyId));
        Require(connectionId, nameof(connectionId));
        Require(operationId, nameof(operationId));
        Require(receiptId, nameof(receiptId));

        return (
                from dispatch in dbContext.ReservationDispatches
                    .AsNoTracking()
                join link in dbContext.ReservationSourceLinks
                        .AsNoTracking()
                    on new
                    {
                        dispatch.ScopeId,
                        Id = dispatch.SourceLinkId
                    }
                    equals new
                    {
                        link.ScopeId,
                        link.Id
                    }
                where
                    dispatch.ScopeId == normalizedScopeId &&
                    dispatch.Id == operationId &&
                    dispatch.PropertyId == propertyId &&
                    dispatch.ConnectionId == connectionId &&
                    dispatch.ReceiptId == receiptId &&
                    link.PropertyId == propertyId &&
                    link.ConnectionId == connectionId
                select new IngestionNotificationSourceLink(link.Id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static void Require(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                $"{parameterName} must be non-empty.",
                parameterName);
        }
    }
}
