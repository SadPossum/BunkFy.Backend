namespace BunkFy.Modules.Staff.Domain.Aggregates;

using BunkFy.Modules.Staff.Domain.ValueObjects;

public sealed partial class StaffMember
{
    public bool MatchesCreation(StaffProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return string.Equals(
                this.DisplayName,
                profile.DisplayName,
                StringComparison.Ordinal) &&
            string.Equals(
                this.LegalName,
                profile.LegalName,
                StringComparison.Ordinal) &&
            string.Equals(
                this.WorkEmail,
                profile.WorkEmail,
                StringComparison.Ordinal) &&
            string.Equals(
                this.WorkPhone,
                profile.WorkPhone,
                StringComparison.Ordinal) &&
            string.Equals(
                this.EmployeeNumber,
                profile.EmployeeNumber,
                StringComparison.Ordinal) &&
            string.Equals(
                this.JobTitle,
                profile.JobTitle,
                StringComparison.Ordinal) &&
            string.Equals(
                this.Department,
                profile.Department,
                StringComparison.Ordinal) &&
            string.Equals(
                this.AuthSubjectId,
                profile.AuthSubjectId,
                StringComparison.Ordinal);
    }
}
