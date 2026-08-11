namespace BunkFy.Extensions.ReservationGuestRecords.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationGuestRecordEndpointTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("GET")]
    public async Task Endpoints_require_both_permissions_at_one_property_scope(
        string method)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        AddEndpointServices(builder.Services);
        await using WebApplication app = builder.Build();

        app.MapBunkFyReservationGuestRecordEndpoints();

        RouteEndpoint endpoint = Assert.Single(
            ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(dataSource => dataSource.Endpoints)
                .OfType<RouteEndpoint>(),
            candidate => candidate.Metadata
                .GetMetadata<HttpMethodMetadata>()?
                .HttpMethods.Contains(method, StringComparer.Ordinal) == true);
        AccessPermissionSetMetadata permissions = Assert.IsType<
            AccessPermissionSetMetadata>(endpoint.Metadata.GetMetadata<
                AccessPermissionSetMetadata>());

        Assert.Equal(
            [
                GuestsAdminPermissionCodes.Create,
                ReservationsAdminPermissionCodes.ManageGuests
            ],
            permissions.Requirements.Select(requirement =>
                requirement.Permission.Value));
        Assert.All(permissions.Requirements, requirement =>
        {
            Assert.True(requirement.RequireScope);
            Assert.Equal(
                ReservationGuestRecordPropertyAccessScopeResolver.ResolverName,
                requirement.ScopeResolverName);
        });
    }

    [Fact]
    public async Task Mutation_publishes_terminal_and_accepted_response_contracts()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        AddEndpointServices(builder.Services);
        await using WebApplication app = builder.Build();
        app.MapBunkFyReservationGuestRecordEndpoints();

        RouteEndpoint endpoint = Assert.Single(
            ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(dataSource => dataSource.Endpoints)
                .OfType<RouteEndpoint>(),
            candidate => candidate.Metadata
                .GetMetadata<HttpMethodMetadata>()?
                .HttpMethods.Contains(HttpMethods.Post, StringComparer.Ordinal) ==
                true);
        IProducesResponseTypeMetadata[] responses = endpoint.Metadata
            .OfType<IProducesResponseTypeMetadata>()
            .Where(response => response.Type ==
                typeof(ReservationGuestRecordLinkProcessDto))
            .OrderBy(response => response.StatusCode)
            .ToArray();

        Assert.Equal(
            [StatusCodes.Status200OK, StatusCodes.Status202Accepted],
            responses.Select(response => response.StatusCode));
    }

    [Fact]
    public void Public_write_contract_does_not_accept_server_owned_coordinates()
    {
        string[] members = typeof(ReservationGuestRecordWriteRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("ActorId", members);
        Assert.DoesNotContain("PropertyId", members);
        Assert.DoesNotContain("ReservationId", members);
        Assert.DoesNotContain("CreationConfirmationId", members);
    }

    [Theory]
    [InlineData("ReservationGuestRecords.ProcessStateInvalid")]
    [InlineData("ReservationGuestRecords.GuestIdentityMismatch")]
    public void Internal_workflow_contract_failures_are_server_errors(
        string errorCode) =>
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            ReservationGuestRecordEndpoints.ErrorStatusCodes.GetStatusCode(
                new Gma.Framework.Results.Error(errorCode, "Contract failure.")));

    private static void AddEndpointServices(IServiceCollection services)
    {
        services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        services.AddSingleton<ReservationGuestRecordWorkflow>(_ => null!);
    }
}
