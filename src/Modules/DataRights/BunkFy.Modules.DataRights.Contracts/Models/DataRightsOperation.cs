namespace BunkFy.Modules.DataRights.Contracts;

[Flags]
public enum DataRightsOperation
{
    None = 0,
    AccessExport = 1,
    Correction = 2,
    Restriction = 4,
    Erasure = 8,
    Anonymisation = 16
}
