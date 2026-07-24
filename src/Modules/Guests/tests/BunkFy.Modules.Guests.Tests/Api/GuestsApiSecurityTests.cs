namespace BunkFy.Modules.Guests.Tests.Api;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Api;
using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestsApiSecurityTests
{
    [Fact]
    public async Task Data_rights_correction_requires_execute_at_guest_property_scope()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new GuestsModule().MapEndpoints(app);

        RouteEndpoint endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>(), candidate =>
                string.Equals(
                    candidate.RoutePattern.RawText?.Trim('/'),
                    "api/guests/properties/{propertyId:guid}/data-rights-corrections",
                    StringComparison.Ordinal) &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                    HttpMethods.Post,
                    StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission =
            Assert.Single(endpoint.Metadata.OfType<AccessPermissionMetadata>());
        Assert.Equal(DataRightsAdminPermissionCodes.Execute, permission.Permission.Value);
        Assert.Equal("guests-property", permission.ScopeResolverName);

        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);
        Assert.Equal(typeof(GuestDataRightsCorrectionReceiptDto), response.Type);
    }
}
