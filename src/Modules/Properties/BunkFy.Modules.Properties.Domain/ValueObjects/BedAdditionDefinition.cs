namespace BunkFy.Modules.Properties.Domain.ValueObjects;

public sealed record BedAdditionDefinition(
    Guid BedId,
    string Label,
    Guid EventId);
