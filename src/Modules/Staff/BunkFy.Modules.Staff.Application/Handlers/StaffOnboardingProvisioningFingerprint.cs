namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Domain.ValueObjects;

internal static class StaffOnboardingProvisioningFingerprint
{
    public static string Compute(StaffProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return StaffMutationFingerprint.Compute(
            "staff-onboarding-provision-v1",
            profile.AuthSubjectId,
            profile.DisplayName,
            profile.LegalName,
            profile.WorkEmail,
            profile.WorkPhone,
            profile.EmployeeNumber,
            profile.JobTitle,
            profile.Department);
    }
}
