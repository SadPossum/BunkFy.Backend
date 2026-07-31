namespace BunkFy.Modules.Workspaces.Domain.Termination;

internal static class WorkspaceTerminationFenceRules
{
    public const int ActorIdMaxLength = 200;
    public const int Sha256Length = 64;

    public static string NormalizeActor(string? actorId)
    {
        string normalized = actorId?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= ActorIdMaxLength
            ? normalized
            : string.Empty;
    }

    public static bool IsSha256(string? value) =>
        value is { Length: Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
