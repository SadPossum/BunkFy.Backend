namespace BunkFy.Modules.Ingestion.Api;

using System.Net.Http.Headers;
using System.Security.Claims;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class IngestionModule : IModule
{
    public string Name => IngestionModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(IngestionProfiles.Default, "BunkFy.Modules.Ingestion.Api");
        builder.Services.AddOptions<IngestionApiSecurityOptions>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAccessHttpScopeResolver, IngestionPropertyAccessScopeResolver>());
        builder.Services.AddIngestionApplication();
        builder.AddIngestionPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        AuthenticationAssuranceRequirement? credentialManagementAssurance = endpoints.ServiceProvider
            .GetRequiredService<IOptions<IngestionApiSecurityOptions>>()
            .Value
            .CredentialManagementAssurance;
        this.MapAdapterTypeEndpoints(endpoints);
        this.MapParserTypeEndpoints(endpoints);
        this.MapConnectionEndpoints(endpoints, credentialManagementAssurance);
        this.MapIngressEndpoints(endpoints);
        this.MapRunEndpoints(endpoints);
        this.MapReceiptEndpoints(endpoints);
        this.MapReprocessingEndpoints(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/ingestion/properties/{propertyId:guid}/proposals")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Proposals")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            ChangeProposalStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListChangeProposalsQuery(
                    propertyId,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{proposalId:guid}", async (
            Guid propertyId,
            Guid proposalId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetChangeProposalQuery(propertyId, proposalId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.SensitiveHistoryRead,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{proposalId:guid}/accept", async (
            Guid propertyId,
            Guid proposalId,
            AcceptProposalRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(
                new AcceptChangeProposalCommand(
                    propertyId,
                    proposalId,
                    $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}",
                    request.ExpectedProposalVersion,
                    request.ExpectedReservationDetailsRevision),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ProposalsDecide,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{proposalId:guid}/reject", async (
            Guid propertyId,
            Guid proposalId,
            RejectProposalRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(
                new RejectChangeProposalCommand(
                    propertyId,
                    proposalId,
                    $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}",
                    request.Reason,
                    request.ExpectedProposalVersion),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ProposalsDecide,
                IngestionPropertyAccessScopeResolver.ResolverName);
    }

    private void MapParserTypeEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/ingestion/properties/{propertyId:guid}/parser-types")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Parser Types")
            .RequireAuthorization();

        group.MapGet("", async (IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new ListObservationParserCapabilitiesQuery(), cancellationToken)
                .ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);
    }

    private void MapAdapterTypeEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/ingestion/properties/{propertyId:guid}/adapter-types")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Adapter Types")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListAdapterTypeCapabilitiesQuery(),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);
    }

    private void MapConnectionEndpoints(
        IEndpointRouteBuilder endpoints,
        AuthenticationAssuranceRequirement? credentialManagementAssurance)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/ingestion/properties/{propertyId:guid}/connections")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Connections")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            AdapterConnectionStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new ListAdapterConnectionsQuery(
                propertyId,
                status,
                page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{connectionId:guid}", async (
            Guid propertyId,
            Guid connectionId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetAdapterConnectionQuery(propertyId, connectionId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{connectionId:guid}/health", async (
            Guid propertyId,
            Guid connectionId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetAdapterConnectionHealthQuery(propertyId, connectionId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPost("", async (
            Guid propertyId,
            CreateConnectionRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(new CreateAdapterConnectionCommand(
                propertyId,
                request.AdapterType,
                request.ExecutionMode,
                request.ConflictPolicy,
                request.ConfigurationReference,
                request.SecretReference), cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ConnectionsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPut("/{connectionId:guid}", async (
            Guid propertyId,
            Guid connectionId,
            UpdateConnectionRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(new UpdateAdapterConnectionCommand(
                propertyId,
                connectionId,
                request.ExecutionMode,
                request.ConflictPolicy,
                request.ConfigurationReference,
                ResolveSecretReferenceUpdateMode(request.SecretReference, request.ClearSecretReference),
                request.SecretReference,
                request.ExpectedVersion), cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ConnectionsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPut("/{connectionId:guid}/polling-schedule", async (
            Guid propertyId,
            Guid connectionId,
            ConfigurePollingScheduleRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(new ConfigureAdapterConnectionPollingScheduleCommand(
                propertyId,
                connectionId,
                request.IntervalSeconds,
                request.MaxAttempts,
                request.ExpectedVersion), cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ConnectionsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{connectionId:guid}/polling-schedule/clear", async (
            Guid propertyId,
            Guid connectionId,
            VersionRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(new ClearAdapterConnectionPollingScheduleCommand(
                propertyId,
                connectionId,
                request.ExpectedVersion), cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ConnectionsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{connectionId:guid}/enable", async (
            Guid propertyId,
            Guid connectionId,
            VersionRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(new SetAdapterConnectionEnabledCommand(
                propertyId, connectionId, Enabled: true, request.ExpectedVersion), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ConnectionsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{connectionId:guid}/disable", async (
            Guid propertyId,
            Guid connectionId,
            VersionRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(new SetAdapterConnectionEnabledCommand(
                propertyId, connectionId, Enabled: false, request.ExpectedVersion), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ConnectionsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{connectionId:guid}/reset-checkpoint", async (
            Guid propertyId,
            Guid connectionId,
            VersionRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(new ResetAdapterConnectionCheckpointCommand(
                propertyId, connectionId, request.ExpectedVersion), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.ConnectionsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{connectionId:guid}/credentials", async (
            Guid propertyId,
            Guid connectionId,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new ListAdapterIngressCredentialsQuery(
                propertyId,
                connectionId,
                page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.CredentialsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);

        RouteHandlerBuilder createCredential = group.MapPost("/{connectionId:guid}/credentials", async (
            Guid propertyId,
            Guid connectionId,
            CreateIngressCredentialRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(context);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            Result<CreateAdapterIngressCredentialResponse> result = await dispatcher.SendAsync(
                new CreateAdapterIngressCredentialCommand(
                    propertyId,
                    connectionId,
                    request.Label,
                    request.ExpiresAtUtc,
                    $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}"),
                cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            return result.ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.CredentialsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);
        RequireAssuranceWhenConfigured(createCredential, credentialManagementAssurance);

        RouteHandlerBuilder revokeCredential = group.MapPost("/{connectionId:guid}/credentials/{credentialId:guid}/revoke", async (
            Guid propertyId,
            Guid connectionId,
            Guid credentialId,
            VersionRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(context);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new RevokeAdapterIngressCredentialCommand(
                propertyId,
                connectionId,
                credentialId,
                request.ExpectedVersion,
                $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}"),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.CredentialsManage,
                IngestionPropertyAccessScopeResolver.ResolverName);
        RequireAssuranceWhenConfigured(revokeCredential, credentialManagementAssurance);
    }

    private static RouteHandlerBuilder RequireAssuranceWhenConfigured(
        RouteHandlerBuilder endpoint,
        AuthenticationAssuranceRequirement? requirement) =>
        requirement is null
            ? endpoint
            : endpoint.RequireAuthenticationAssurance(requirement);

    private void MapIngressEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/ingestion/adapter-ingress/connections")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Adapter Ingress");

        group.MapPost("/{connectionId:guid}/observations", async (
            Guid connectionId,
            AdapterIngressSubmissionRequest request,
            HttpContext context,
            IServiceProvider services,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryReadAdapterToken(context, out string token))
            {
                return Results.Unauthorized();
            }

            IAdapterIngressAuthenticator authenticator =
                services.GetRequiredService<IAdapterIngressAuthenticator>();
            Result<AdapterIngressIdentity> authenticated = await authenticator.AuthenticateAsync(
                connectionId, token, AdapterExecutionMode.Push, cancellationToken).ConfigureAwait(false);
            if (authenticated.IsFailure)
            {
                return Results.Unauthorized();
            }

            context.User = CreateAdapterPrincipal(authenticated.Value);
            if (!IsValidSubmission(request))
            {
                return Results.Problem(
                    title: IngestionApplicationErrors.IngressSubmissionInvalid.Code,
                    detail: IngestionApplicationErrors.IngressSubmissionInvalid.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            List<AdapterObservationResult> results = [];
            foreach (AdapterIngressObservationRequest record in request.Records)
            {
                Result<AdapterObservationResult> received = await dispatcher.SendAsync(
                    new ReceiveObservationCommand(
                        connectionId,
                        RunId: null,
                        record.OperationId,
                        record.RecordType,
                        record.ExternalRecordId,
                        record.SourceRevision,
                        record.SourceUpdatedAtUtc,
                        record.ObservedAtUtc,
                        record.ContentType,
                        record.Payload,
                        record.ContentSha256),
                    cancellationToken).ConfigureAwait(false);
                results.Add(received.IsSuccess
                    ? received.Value
                    : new AdapterObservationResult(
                        record.OperationId,
                        AdapterObservationDisposition.Rejected,
                        receiptId: null,
                        received.Error.Code));
            }

            return Results.Ok(new AdapterIngressSubmissionResponse(results));
        })
            .RequireTenantWithIndependentAuthentication()
            .WithMetadata(new RequestSizeLimitAttribute(AdapterIngressContractLimits.MaximumHttpRequestBodyBytes));

        group.MapPost("/{connectionId:guid}/remote-leases/claim", async (
            Guid connectionId,
            AdapterRemoteLeaseClaimRequest request,
            HttpContext context,
            IServiceProvider services,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            AdapterIngressIdentity? identity = await AuthenticateAdapterAsync(
                connectionId,
                AdapterExecutionMode.RemotePolling,
                context,
                services,
                cancellationToken).ConfigureAwait(false);
            if (identity is null)
            {
                return Results.Unauthorized();
            }

            context.User = CreateAdapterPrincipal(identity);
            try
            {
                return (await dispatcher.SendAsync(
                    new ClaimRemoteAdapterLeaseCommand(connectionId, identity.CredentialId, request),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
            }
            catch (OptimisticConcurrencyException)
            {
                return Results.Conflict();
            }
        })
            .RequireTenantWithIndependentAuthentication()
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024L));

        group.MapPost("/{connectionId:guid}/remote-leases/renew", async (
            Guid connectionId,
            AdapterRemoteLeaseRenewRequest request,
            HttpContext context,
            IServiceProvider services,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            AdapterIngressIdentity? identity = await AuthenticateAdapterAsync(
                connectionId,
                AdapterExecutionMode.RemotePolling,
                context,
                services,
                cancellationToken).ConfigureAwait(false);
            if (identity is null)
            {
                return Results.Unauthorized();
            }

            context.User = CreateAdapterPrincipal(identity);
            try
            {
                return (await dispatcher.SendAsync(
                    new RenewRemoteAdapterLeaseCommand(connectionId, identity.CredentialId, request),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
            }
            catch (OptimisticConcurrencyException)
            {
                return Results.Conflict();
            }
        })
            .RequireTenantWithIndependentAuthentication()
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024L));

        group.MapPost("/{connectionId:guid}/remote-leases/observations", async (
            Guid connectionId,
            AdapterRemoteObservationSubmissionRequest request,
            HttpContext context,
            IServiceProvider services,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            AdapterIngressIdentity? identity = await AuthenticateAdapterAsync(
                connectionId,
                AdapterExecutionMode.RemotePolling,
                context,
                services,
                cancellationToken).ConfigureAwait(false);
            if (identity is null)
            {
                return Results.Unauthorized();
            }

            context.User = CreateAdapterPrincipal(identity);
            if (!IsValidRemoteSubmission(request))
            {
                return Results.Problem(
                    title: IngestionApplicationErrors.IngressSubmissionInvalid.Code,
                    detail: IngestionApplicationErrors.IngressSubmissionInvalid.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            List<AdapterObservationResult> results = [];
            foreach (AdapterIngressObservationRequest record in request.Records)
            {
                Result<AdapterObservationResult> received;
                try
                {
                    received = await dispatcher.SendAsync(
                        new ReceiveObservationCommand(
                            connectionId,
                            request.Lease.RunId,
                            record.OperationId,
                            record.RecordType,
                            record.ExternalRecordId,
                            record.SourceRevision,
                            record.SourceUpdatedAtUtc,
                            record.ObservedAtUtc,
                            record.ContentType,
                            record.Payload,
                            record.ContentSha256,
                            RemoteLease: request.Lease,
                            RemoteCredentialId: identity.CredentialId),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OptimisticConcurrencyException)
                {
                    received = Result.Failure<AdapterObservationResult>(
                        BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMismatch);
                }

                results.Add(received.IsSuccess
                    ? received.Value
                    : new AdapterObservationResult(
                        record.OperationId,
                        AdapterObservationDisposition.Rejected,
                        receiptId: null,
                        received.Error.Code));
            }

            bool checkpointAccepted = false;
            string? acceptedCheckpoint = null;
            if (request.ProposedCheckpoint is not null &&
                results.All(result => result.Disposition is
                    AdapterObservationDisposition.Accepted or AdapterObservationDisposition.Duplicate))
            {
                try
                {
                    Result<Unit> checkpoint = await dispatcher.SendAsync(
                        new AdvanceConnectionCheckpointCommand(
                            connectionId,
                            request.Lease.RunId,
                            request.ProposedCheckpoint,
                            request.Lease,
                            identity.CredentialId),
                        cancellationToken).ConfigureAwait(false);
                    checkpointAccepted = checkpoint.IsSuccess;
                    acceptedCheckpoint = checkpoint.IsSuccess ? request.ProposedCheckpoint.Trim() : null;
                }
                catch (OptimisticConcurrencyException)
                {
                    checkpointAccepted = false;
                }
            }

            return Results.Ok(new AdapterRemoteObservationSubmissionResponse(
                new AdapterObservationAcknowledgement(
                    request.Lease.RunId,
                    request.Lease.LeaseId,
                    results,
                    checkpointAccepted,
                    acceptedCheckpoint)));
        })
            .RequireTenantWithIndependentAuthentication()
            .WithMetadata(new RequestSizeLimitAttribute(AdapterIngressContractLimits.MaximumHttpRequestBodyBytes));

        group.MapPost("/{connectionId:guid}/remote-leases/complete", async (
            Guid connectionId,
            AdapterRemoteRunCompletionRequest request,
            HttpContext context,
            IServiceProvider services,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            AdapterIngressIdentity? identity = await AuthenticateAdapterAsync(
                connectionId,
                AdapterExecutionMode.RemotePolling,
                context,
                services,
                cancellationToken).ConfigureAwait(false);
            if (identity is null)
            {
                return Results.Unauthorized();
            }

            context.User = CreateAdapterPrincipal(identity);
            try
            {
                return (await dispatcher.SendAsync(
                    new CompleteRemoteAdapterRunCommand(connectionId, identity.CredentialId, request),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
            }
            catch (OptimisticConcurrencyException)
            {
                return Results.Conflict();
            }
        })
            .RequireTenantWithIndependentAuthentication()
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024L));
    }

    private static async Task<AdapterIngressIdentity?> AuthenticateAdapterAsync(
        Guid connectionId,
        AdapterExecutionMode requiredMode,
        HttpContext context,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (!TryReadAdapterToken(context, out string token))
        {
            return null;
        }

        IAdapterIngressAuthenticator authenticator = services.GetRequiredService<IAdapterIngressAuthenticator>();
        Result<AdapterIngressIdentity> authenticated = await authenticator.AuthenticateAsync(
            connectionId, token, requiredMode, cancellationToken).ConfigureAwait(false);
        return authenticated.IsSuccess ? authenticated.Value : null;
    }

    private static bool TryReadAdapterToken(HttpContext context, out string token)
    {
        token = string.Empty;
        return AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out AuthenticationHeaderValue? value) &&
               string.Equals(value.Scheme, "BunkFy-Adapter", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(value.Parameter) &&
               (token = value.Parameter.Trim()).Length > 0;
    }

    private static bool IsValidSubmission(AdapterIngressSubmissionRequest? request)
    {
        if (request?.Records is null ||
            request.Records.Count is 0 or > AdapterProtocolLimits.MaximumRecordsPerSubmission ||
            request.Records.Any(record => record is null || record.Payload is null) ||
            request.Records.Select(record => record.OperationId).Distinct().Count() != request.Records.Count)
        {
            return false;
        }

        return request.Records.Sum(record => (long)record.Payload.Length) <=
               AdapterProtocolLimits.MaximumSubmissionPayloadBytes;
    }

    private static bool IsValidRemoteSubmission(AdapterRemoteObservationSubmissionRequest? request)
    {
        if (request?.Lease is null || request.Lease.RunId == Guid.Empty || request.Lease.LeaseId == Guid.Empty ||
            request.Lease.LeaseEpoch <= 0 || request.Lease.WorkerId == Guid.Empty ||
            request.ProposedCheckpoint?.Length > AdapterProtocolLimits.CheckpointMaxLength)
        {
            return false;
        }

        return IsValidSubmission(new AdapterIngressSubmissionRequest(request.Records));
    }

    private static ClaimsPrincipal CreateAdapterPrincipal(AdapterIngressIdentity identity) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, $"ingestion-adapter:{identity.CredentialId:N}"),
            new Claim(ApplicationClaimNames.ScopeId, identity.ScopeId),
            new Claim("bunkfy_adapter_connection_id", identity.ConnectionId.ToString("D"))
        ],
        authenticationType: "BunkFy-Adapter"));

    private void MapRunEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/ingestion/properties/{propertyId:guid}/runs")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Runs")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            Guid? connectionId,
            IngestionRunStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new ListIngestionRunsQuery(
                propertyId,
                connectionId,
                status,
                page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{runId:guid}", async (
            Guid propertyId,
            Guid runId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new GetIngestionRunQuery(propertyId, runId), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);
    }

    private void MapReceiptEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/ingestion/properties/{propertyId:guid}/receipts")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Receipts")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            Guid? connectionId,
            Guid? runId,
            ObservationReceiptStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new ListObservationReceiptsQuery(
                propertyId,
                connectionId,
                runId,
                status,
                page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{receiptId:guid}", async (
            Guid propertyId,
            Guid receiptId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new GetObservationReceiptQuery(propertyId, receiptId), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{receiptId:guid}/raw-payload", async (
            Guid propertyId,
            Guid receiptId,
            HttpContext httpContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<ObservationRawPayload> result = await dispatcher.QueryAsync(
                new GetObservationRawPayloadQuery(propertyId, receiptId),
                cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? RawPayloadDownload(httpContext, receiptId, result.Value)
                : Result.Failure<ObservationRawPayload>(result.Error).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.RawPayloadsRead,
                IngestionPropertyAccessScopeResolver.ResolverName);
    }

    private void MapReprocessingEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(
                "/api/ingestion/properties/{propertyId:guid}/reprocessing-attempts")
            .WithModuleName(this.Name)
            .WithTags("Ingestion Reprocessing")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            Guid? sourceReceiptId,
            ObservationReprocessingStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new ListObservationReprocessingAttemptsQuery(
                propertyId,
                sourceReceiptId,
                status,
                page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{attemptId:guid}", async (
            Guid propertyId,
            Guid attemptId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetObservationReprocessingAttemptQuery(propertyId, attemptId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                IngestionAdminPermissionCodes.Read,
                IngestionPropertyAccessScopeResolver.ResolverName);
    }

    private static IResult RawPayloadDownload(
        HttpContext context,
        Guid receiptId,
        ObservationRawPayload payload)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.Append("Content-Security-Policy", "sandbox");
        context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");
        return Results.File(
            payload.Content.ToArray(),
            "application/octet-stream",
            $"ingestion-receipt-{receiptId:N}.payload",
            enableRangeProcessing: false);
    }

    public sealed record AcceptProposalRequest(
        long ExpectedProposalVersion,
        long ExpectedReservationDetailsRevision);

    public sealed record RejectProposalRequest(long ExpectedProposalVersion, string Reason);

    public sealed record CreateConnectionRequest(
        string AdapterType,
        AdapterExecutionMode ExecutionMode,
        AdapterConflictPolicy ConflictPolicy,
        string ConfigurationReference,
        string? SecretReference);

    public sealed record UpdateConnectionRequest(
        AdapterExecutionMode ExecutionMode,
        AdapterConflictPolicy ConflictPolicy,
        string ConfigurationReference,
        string? SecretReference,
        bool ClearSecretReference,
        long ExpectedVersion);

    private static SecretReferenceUpdateMode ResolveSecretReferenceUpdateMode(
        string? secretReference,
        bool clearSecretReference) => (secretReference, clearSecretReference) switch
        {
            (not null, true) => SecretReferenceUpdateMode.Unknown,
            (not null, false) => SecretReferenceUpdateMode.Replace,
            (null, true) => SecretReferenceUpdateMode.Clear,
            _ => SecretReferenceUpdateMode.Keep
        };

    public sealed record VersionRequest(long ExpectedVersion);

    public sealed record ConfigurePollingScheduleRequest(
        int IntervalSeconds,
        int MaxAttempts,
        long ExpectedVersion);

    public sealed record CreateIngressCredentialRequest(
        string Label,
        DateTimeOffset? ExpiresAtUtc = null);

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(IngestionApplicationErrors.AdapterTypeNotRegistered.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.AdapterExecutionModeUnsupported.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.PollingIntervalBelowAdapterMinimum.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ProposalNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.ProposalStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ProposalDecisionConflict.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.ConnectionNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.IngressCredentialNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.IngressCredentialLimitReached.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.IngressCredentialsRequirePushMode.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.RemoteLeaseClaimInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.RemoteLeaseDescriptorMismatch.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.RemoteLeaseUnavailable.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.PropertyNotActive.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.ConnectionStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.RunNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.RunStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ReceiptNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.ReceiptStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.ReprocessingAttemptNotFound.Code, StatusCodes.Status404NotFound),
        new(IngestionApplicationErrors.ReprocessingAttemptStatusInvalid.Code, StatusCodes.Status400BadRequest),
        new(IngestionApplicationErrors.RawPayloadInvalid.Code, StatusCodes.Status422UnprocessableEntity),
        new(IngestionApplicationErrors.RawPayloadPurgeInProgress.Code, StatusCodes.Status409Conflict),
        new(IngestionApplicationErrors.RawPayloadUnavailable.Code, StatusCodes.Status410Gone),
        new(IngestionApplicationErrors.SecretReferenceUpdateInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.PollingScheduleRequiresPollingMode.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.PollingIntervalInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.PollingScheduleAttemptsInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialLabelInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialExpiryInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialActorInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.IngressCredentialAlreadyRevoked.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseIdentityInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseDurationInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseRequiresRemotePollingMode.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseAlreadyActive.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseNotActive.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMismatch.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseExpired.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMustBeReleased.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseEpochExhausted.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionAlreadyEnabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionAlreadyDisabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.ConnectionMustBeDisabled.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.DecisionReasonInvalid.Code, StatusCodes.Status400BadRequest));
}
