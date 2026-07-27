namespace BunkFy.Modules.DataRights.Application.Models;

public sealed record DataRightsExportDownload(
    Stream Content,
    long ContentLength,
    string FileName);
