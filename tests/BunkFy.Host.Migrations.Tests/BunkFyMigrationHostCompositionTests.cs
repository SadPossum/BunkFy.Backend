namespace BunkFy.Host.Migrations.Tests;

using BunkFy.Host.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void Enum_database_defaults_declare_explicit_zero_sentinels()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            "Host=localhost;Database=bunkfy-tests;Username=test;Password=test";
        builder.AddBunkFyMigrationPersistence("default");
        Type[] dbContextTypes = builder.Services
            .Where(descriptor =>
                typeof(DbContext).IsAssignableFrom(descriptor.ServiceType))
            .Select(descriptor => descriptor.ServiceType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        using IHost host = builder.Build();
        List<string> defaultedEnumProperties = [];
        List<string> invalidSentinels = [];

        foreach (Type dbContextType in dbContextTypes)
        {
            using IServiceScope scope = host.Services.CreateScope();
            DbContext dbContext = (DbContext)scope.ServiceProvider
                .GetRequiredService(dbContextType);
            IModel model = dbContext.GetService<IDesignTimeModel>().Model;

            foreach (IProperty property in model.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties()))
            {
                Type enumType = Nullable.GetUnderlyingType(property.ClrType) ??
                    property.ClrType;
                if (!enumType.IsEnum)
                {
                    continue;
                }

                IConventionProperty conventionProperty =
                    Assert.IsType<IConventionProperty>(
                        property,
                        exactMatch: false);
                if (conventionProperty.GetDefaultValueConfigurationSource() is null)
                {
                    continue;
                }

                string propertyName =
                    $"{dbContextType.Name}.{property.DeclaringType.Name}.{property.Name}";
                defaultedEnumProperties.Add(propertyName);
                if (conventionProperty.GetSentinelConfigurationSource() is null)
                {
                    invalidSentinels.Add(
                        $"{propertyName} has no explicit sentinel");
                }
                else if (!Equals(
                             Activator.CreateInstance(enumType),
                             property.Sentinel))
                {
                    invalidSentinels.Add(
                        $"{propertyName} does not use its zero enum value as the sentinel");
                }
            }
        }

        Assert.NotEmpty(defaultedEnumProperties);
        Assert.True(
            invalidSentinels.Count == 0,
            "Invalid enum database-default sentinels:" + Environment.NewLine +
            string.Join(Environment.NewLine, invalidSentinels));
    }
}
