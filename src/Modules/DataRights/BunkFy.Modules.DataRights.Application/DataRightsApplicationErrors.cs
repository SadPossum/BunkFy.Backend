namespace BunkFy.Modules.DataRights.Application;

using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Results;

public static class DataRightsApplicationErrors
{
    public static readonly Error TenantRequired = new(
        "DataRights.TenantRequired",
        "A tenant scope is required.");
    public static readonly Error CaseNotFound = new(
        "DataRights.CaseNotFound",
        "The data-rights case was not found.");

    public static Error VersionConflict => DataRightsDomainErrors.VersionConflict;
    public static Error TransitionInvalid => DataRightsDomainErrors.TransitionInvalid;
    public static Error VerificationRequired => DataRightsDomainErrors.VerificationRequired;
    public static Error ControllerRoutingRequired => DataRightsDomainErrors.ControllerRoutingRequired;
}
