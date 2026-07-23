namespace BunkFy.Modules.DataRights.Domain.Models;

[Flags]
public enum DataRightsCaseOperation
{
    None = 0,
    AccessExport = 1,
    Correction = 2,
    Restriction = 4,
    Erasure = 8,
    Anonymisation = 16
}
