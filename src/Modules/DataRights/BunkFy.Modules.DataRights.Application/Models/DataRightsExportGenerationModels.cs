namespace BunkFy.Modules.DataRights.Application.Models;

using BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsExportGenerationStart(
    bool DispatchRequired,
    Guid ArtifactId,
    string TenantId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    long DecisionRevision,
    IReadOnlyCollection<DataRightsSubjectCoordinate> SelectedSubjects,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record DataRightsExportGenerationRequest(
    Guid ArtifactId,
    string TenantId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    long DecisionRevision,
    IReadOnlyCollection<DataRightsSubjectCoordinate> SelectedSubjects,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record DataRightsExportAssemblyResult(
    int SubjectCount,
    int RecordCount);

public sealed record DataRightsProtectedExportArtifact(
    string StorageKey,
    long EncryptedByteLength,
    string PlaintextSha256,
    int EncryptionKeyVersion,
    int FormatVersion,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed class DataRightsExportGenerationException : Exception
{
    public DataRightsExportGenerationException(string code)
        : base(Normalize(code))
        => this.Code = Normalize(code);

    public DataRightsExportGenerationException(string code, Exception innerException)
        : base(Normalize(code), innerException)
        => this.Code = Normalize(code);

    public string Code { get; }

    private static string Normalize(string code)
    {
        string normalized = code?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or >
            BunkFy.Modules.DataRights.Domain.Aggregates
                .DataRightsExportArtifact.FailureCodeMaxLength ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "A bounded machine-readable export failure code is required.",
                nameof(code));
        }

        return normalized;
    }
}
