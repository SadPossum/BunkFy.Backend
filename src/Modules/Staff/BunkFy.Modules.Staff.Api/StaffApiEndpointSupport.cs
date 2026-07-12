namespace BunkFy.Modules.Staff.Api;

using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Results;
using Microsoft.AspNetCore.Http;
using BunkFy.Modules.Staff.Application;

internal static class StaffApiEndpointSupport
{
    public static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(StaffApplicationErrors.StaffMemberNotFound.Code, StatusCodes.Status404NotFound),
        new(StaffApplicationErrors.PropertyUnavailable.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.EmployeeNumberConflict.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.AuthSubjectConflict.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.StaffSuspended.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.StaffDeparted.Code, StatusCodes.Status409Conflict));

    public static string ResolveActor(HttpContext context, IAccessHttpSubjectResolver resolver)
    {
        AccessSubject? subject = resolver.ResolveSubject(context);
        return subject is null
            ? "authenticated:unknown"
            : $"{AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}";
    }
}
