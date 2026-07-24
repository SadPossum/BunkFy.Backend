namespace BunkFy.Modules.DataRights.Api;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Http;

internal static class DataRightsEndpointSupport
{
    public static ApiErrorStatusCodeMap ErrorStatusCodes { get; } =
        ApiErrorStatusCodeMap.Create([
            new(DataRightsApplicationErrors.CaseNotFound.Code, StatusCodes.Status404NotFound),
            new(DataRightsApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.TransitionInvalid.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.VerificationRequired.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.ControllerRoutingRequired.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.DiscoveryCriteriaInvalid.Code, StatusCodes.Status400BadRequest),
            new(DataRightsApplicationErrors.DiscoveryScopeUnavailable.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.SubjectOwnerUnavailable.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.SubjectNotFound.Code, StatusCodes.Status404NotFound),
            new(DataRightsApplicationErrors.SubjectStale.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.SubjectCoordinateInvalid.Code, StatusCodes.Status400BadRequest),
            new(DataRightsApplicationErrors.SubjectAlreadySelected.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.SubjectNotSelected.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.SubjectSelectionLimitReached.Code, StatusCodes.Status409Conflict),
            new(DataRightsApplicationErrors.SubjectSelectionRequired.Code, StatusCodes.Status409Conflict)
        ]);

    public static async Task<IResult> DispatchAsync<TCommand>(
        HttpContext context,
        IAccessHttpSubjectResolver subjectResolver,
        Func<string, TCommand> commandFactory,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
        where TCommand : ICommand<DataRightsCaseDto>
    {
        string? actor = ResolveActor(context, subjectResolver);
        return actor is null
            ? Results.Unauthorized()
            : (await dispatcher.SendAsync(
                commandFactory(actor),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
    }

    public static string? ResolveActor(
        HttpContext context,
        IAccessHttpSubjectResolver subjectResolver)
    {
        AccessSubject? subject = subjectResolver.ResolveSubject(context);
        return subject is null
            ? null
            : $"{AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}";
    }
}

internal static class DataRightsSensitiveResponseHeaders
{
    public static void Apply(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "0";
    }
}
