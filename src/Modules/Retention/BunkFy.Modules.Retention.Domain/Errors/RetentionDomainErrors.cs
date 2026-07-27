namespace BunkFy.Modules.Retention.Domain.Errors;

using Gma.Framework.Results;

public static class RetentionDomainErrors
{
    public static readonly Error CoordinateInvalid = new(
        "Retention.CoordinateInvalid",
        "The retention execution coordinate is invalid.");

    public static readonly Error AttemptInvalid = new(
        "Retention.AttemptInvalid",
        "The retention execution attempt is invalid.");

    public static readonly Error TransitionInvalid = new(
        "Retention.TransitionInvalid",
        "The retention execution transition is invalid.");

    public static readonly Error CompletionInvalid = new(
        "Retention.CompletionInvalid",
        "The retention execution result is invalid.");
}
