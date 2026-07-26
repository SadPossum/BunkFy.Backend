namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Linq.Expressions;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionAnonymisationBarrierRepository(
    IngestionDbContext dbContext)
    : IIngestionAnonymisationBarrierRepository
{
    public async Task<bool> IsBlockedAsync(
        Guid sourceLinkId,
        IReadOnlyCollection<IngestionAnonymisationFingerprintValue>
            fingerprints,
        CancellationToken cancellationToken)
    {
        if (sourceLinkId == Guid.Empty)
        {
            throw new ArgumentException(
                "A source-link id is required.",
                nameof(sourceLinkId));
        }

        if (await dbContext.AnonymisationTombstones
                .AsNoTracking()
                .AnyAsync(
                    tombstone => tombstone.Id == sourceLinkId,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (fingerprints is null || fingerprints.Count == 0)
        {
            return false;
        }

        Expression<Func<IngestionAnonymisationFingerprint, bool>>
            predicate = CreateExactMatchPredicate(
                fingerprints.Distinct());
        return await dbContext.AnonymisationFingerprints
            .AsNoTracking()
            .AnyAsync(predicate, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Expression<Func<
        IngestionAnonymisationFingerprint,
        bool>> CreateExactMatchPredicate(
        IEnumerable<IngestionAnonymisationFingerprintValue> candidates)
    {
        ParameterExpression fingerprint = Expression.Parameter(
            typeof(IngestionAnonymisationFingerprint),
            "fingerprint");
        Expression matches = Expression.Constant(false);
        foreach (IngestionAnonymisationFingerprintValue candidate in
                 candidates)
        {
            Expression exact = Expression.AndAlso(
                Expression.Equal(
                    Expression.Property(
                        fingerprint,
                        nameof(IngestionAnonymisationFingerprint.Purpose)),
                    Expression.Constant(candidate.Purpose)),
                Expression.AndAlso(
                    Expression.Equal(
                        Expression.Property(
                            fingerprint,
                            nameof(IngestionAnonymisationFingerprint
                                .KeyVersion)),
                        Expression.Constant(candidate.KeyVersion)),
                    Expression.Equal(
                        Expression.Property(
                            fingerprint,
                            nameof(IngestionAnonymisationFingerprint.Sha256)),
                        Expression.Constant(candidate.Sha256))));
            matches = Expression.OrElse(matches, exact);
        }

        return Expression.Lambda<Func<
            IngestionAnonymisationFingerprint,
            bool>>(matches, fingerprint);
    }
}
