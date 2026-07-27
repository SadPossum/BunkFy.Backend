namespace BunkFy.Modules.Ingestion.Domain.Retention;

using Gma.Framework.Results;

public static class IngestionRetentionExecutionErrors
{
    public static readonly Error CoordinateInvalid = new(
        "Ingestion.RetentionExecutionCoordinateInvalid",
        "The ingestion retention execution coordinate is invalid.");
    public static readonly Error TransitionInvalid = new(
        "Ingestion.RetentionExecutionTransitionInvalid",
        "The ingestion retention execution cannot make that transition.");
    public static readonly Error ResultInvalid = new(
        "Ingestion.RetentionExecutionResultInvalid",
        "The ingestion retention execution result is invalid.");
}
