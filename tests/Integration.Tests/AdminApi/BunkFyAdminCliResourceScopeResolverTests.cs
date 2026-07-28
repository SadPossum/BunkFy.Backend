namespace Integration.Tests;

using BunkFy.Host.AdminCli.Security;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Administration;
using System.CommandLine;
using System.CommandLine.Parsing;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BunkFyAdminCliResourceScopeResolverTests
{
    private readonly BunkFyAdminCliResourceScopeResolver resolver = new();

    [Fact]
    public void Command_without_property_remains_tenant_scoped()
    {
        ParseResult parseResult = new RootCommand("admin").Parse([]);

        bool resolved = this.resolver.TryResolve(parseResult, out AdminResourceScope? scope);

        Assert.True(resolved);
        Assert.Null(scope);
    }

    [Fact]
    public void Property_option_becomes_an_exact_normalized_resource_scope()
    {
        Option<Guid> propertyOption = new("--property-id") { Required = true };
        Command command = new("get") { propertyOption };
        RootCommand root = new("admin") { command };
        ParseResult parseResult = root.Parse(
            ["get", "--property-id", "9E11C484-2999-44D5-B293-2C0962FDFF76"]);
        Assert.Empty(parseResult.Errors);

        bool resolved = this.resolver.TryResolve(parseResult, out AdminResourceScope? scope);

        Assert.True(resolved);
        AdminResourceScopeSegment segment = Assert.Single(Assert.IsType<AdminResourceScope>(scope).Segments);
        Assert.Equal(WorkspaceAccessScopes.PropertySegmentName, segment.Name);
        Assert.Equal("9e11c484-2999-44d5-b293-2c0962fdff76", segment.Value);
    }

    [Fact]
    public void Empty_property_option_fails_closed()
    {
        Option<Guid> propertyOption = new("--property-id") { Required = true };
        Command command = new("get") { propertyOption };
        RootCommand root = new("admin") { command };
        ParseResult parseResult = root.Parse(
            ["get", "--property-id", "00000000-0000-0000-0000-000000000000"]);
        Assert.Empty(parseResult.Errors);

        bool resolved = this.resolver.TryResolve(parseResult, out AdminResourceScope? scope);

        Assert.False(resolved);
        Assert.Null(scope);
    }
}
