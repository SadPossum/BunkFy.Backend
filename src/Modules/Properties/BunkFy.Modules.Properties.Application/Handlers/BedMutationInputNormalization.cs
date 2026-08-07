namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Results;

internal static class BedMutationInputNormalization
{
    public static Result<BedLabel> NormalizeLabel(string? label) =>
        BedLabel.Create(label);

    public static Result<IReadOnlyCollection<BedLabel>> NormalizeBatch(
        IReadOnlyCollection<string>? labels)
    {
        if (labels is null || labels.Count == 0)
        {
            return Result.Failure<IReadOnlyCollection<BedLabel>>(
                PropertiesApplicationErrors.BedBatchRequired);
        }

        if (labels.Count > PropertiesContractLimits.MaximumBedsPerBatch)
        {
            return Result.Failure<IReadOnlyCollection<BedLabel>>(
                PropertiesApplicationErrors.BedBatchTooLarge);
        }

        List<BedLabel> normalized = new(labels.Count);
        foreach (string label in labels)
        {
            Result<BedLabel> result = BedLabel.Create(label);
            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyCollection<BedLabel>>(
                    result.Error);
            }

            normalized.Add(result.Value);
        }

        return normalized.Distinct().Count() == normalized.Count
            ? Result.Success<IReadOnlyCollection<BedLabel>>(normalized)
            : Result.Failure<IReadOnlyCollection<BedLabel>>(
                PropertiesDomainErrors.BedAlreadyExists);
    }
}
