namespace Integration.Tests;

using BunkFy.Host.AdminApi.Security;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Administration;
using Microsoft.AspNetCore.Http;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BunkFyAdminApiResourceScopeResolverTests
{
    private readonly BunkFyAdminApiResourceScopeResolver resolver = new();

    [Fact]
    public void Route_without_property_remains_tenant_scoped()
    {
        DefaultHttpContext context = new();

        bool resolved = this.resolver.TryResolve(context, out AdminResourceScope? scope);

        Assert.True(resolved);
        Assert.Null(scope);
    }

    [Fact]
    public void Property_route_becomes_an_exact_normalized_resource_scope()
    {
        Guid propertyId = Guid.Parse("9E11C484-2999-44D5-B293-2C0962FDFF76");
        DefaultHttpContext context = new();
        context.Request.RouteValues["propertyId"] = propertyId;

        bool resolved = this.resolver.TryResolve(context, out AdminResourceScope? scope);

        Assert.True(resolved);
        AdminResourceScopeSegment segment = Assert.Single(Assert.IsType<AdminResourceScope>(scope).Segments);
        Assert.Equal(WorkspaceAccessScopes.PropertySegmentName, segment.Name);
        Assert.Equal("9e11c484-2999-44d5-b293-2c0962fdff76", segment.Value);
    }

    [Fact]
    public void Invalid_property_route_fails_closed()
    {
        DefaultHttpContext context = new();
        context.Request.RouteValues["propertyId"] = "not-a-guid";

        bool resolved = this.resolver.TryResolve(context, out AdminResourceScope? scope);

        Assert.False(resolved);
        Assert.Null(scope);
    }

    [Fact]
    public void Empty_property_route_fails_closed()
    {
        DefaultHttpContext context = new();
        context.Request.RouteValues["propertyId"] = Guid.Empty;

        bool resolved = this.resolver.TryResolve(context, out AdminResourceScope? scope);

        Assert.False(resolved);
        Assert.Null(scope);
    }
}
