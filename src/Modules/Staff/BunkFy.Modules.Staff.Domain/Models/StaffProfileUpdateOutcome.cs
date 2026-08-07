namespace BunkFy.Modules.Staff.Domain.Models;

public sealed record StaffProfileUpdateOutcome(
    long PreviousVersion,
    long CurrentVersion,
    bool Changed);
