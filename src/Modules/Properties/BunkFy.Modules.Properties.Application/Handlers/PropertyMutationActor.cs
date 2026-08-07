namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;

internal readonly record struct PropertyMutationActor(string? Value)
{
    public static Result<PropertyMutationActor> Required(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return IsValid(normalized, required: true)
            ? Result.Success(new PropertyMutationActor(normalized))
            : Result.Failure<PropertyMutationActor>(
                PropertiesDomainErrors.ActorIdInvalid);
    }

    public static Result<PropertyMutationActor> Optional(string? value)
    {
        string? normalized = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
        return normalized is null || IsValid(normalized, required: false)
            ? Result.Success(new PropertyMutationActor(normalized))
            : Result.Failure<PropertyMutationActor>(
                PropertiesDomainErrors.ActorIdInvalid);
    }

    private static bool IsValid(string value, bool required) =>
        (!required || value.Length > 0) &&
        value.Length <= PropertiesContractLimits.ActorIdMaxLength &&
        !value.Any(char.IsControl);
}
