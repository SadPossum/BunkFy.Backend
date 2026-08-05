namespace BunkFy.Host.Migrations.Tests;

using BunkFy.Host.Migrations;
using Microsoft.Extensions.Hosting;

public sealed class BunkFyMigrationHostCompositionTests
{
    [Fact]
    public void Migration_composition_does_not_start_module_background_services()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            "Host=localhost;Database=bunkfy-tests;Username=test;Password=test";

        builder.AddBunkFyMigrationPersistence("default");

        Assert.DoesNotContain(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IHostedService));
    }
}
