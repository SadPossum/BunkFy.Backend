namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFyWorkspaces(
        this IServiceCollection services,
        Action<BunkFyWorkspacesOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<BunkFyWorkspacesOptions>();
        }

        services.AddOptions<BunkFyWorkspacesOptions>()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.GlobalAuthScopeId),
                "A global Auth scope id is required.")
            .ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IAccessProfileAssignmentPolicy,
            WorkspaceAccessProfileAssignmentPolicy>());

        services.AddIntegrationEventHandler<
            OrganizationMembershipChangedIntegrationEvent,
            OrganizationMembershipStaffHandler>(
            StaffModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);
        services.AddIntegrationEventHandler<
            OrganizationMembershipChangedIntegrationEvent,
            OrganizationMembershipAccessHandler>(
            AccessControlModuleMetadata.Name,
            OrganizationsModuleMetadata.Name);

        return services;
    }

    public static IServiceCollection AddBunkFyWorkspaceAdmission(
        this IServiceCollection services,
        IConfiguration configuration,
        bool requireExplicitPolicy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        bool passwordRegistrationEnabled = ReadBoolean(
            configuration,
            "Auth:SelfRegistration:PasswordEnabled",
            defaultValue: true);
        bool externalRegistrationEnabled = ReadBoolean(
            configuration,
            "Auth:SelfRegistration:ExternalEnabled",
            defaultValue: true);
        bool selfServiceWorkspaceCreationEnabled = ReadBoolean(
            configuration,
            "Organizations:SelfServiceCreationEnabled",
            defaultValue: false);

        services.AddOptions<BunkFyWorkspaceAdmissionOptions>()
            .Configure(options =>
            {
                options.AccountRegistration = ReadEnum<BunkFyAccountRegistrationMode>(
                    configuration,
                    $"{BunkFyWorkspaceAdmissionOptions.SectionName}:AccountRegistration");
                options.WorkspaceCreation = ReadEnum<BunkFyWorkspaceCreationMode>(
                    configuration,
                    $"{BunkFyWorkspaceAdmissionOptions.SectionName}:WorkspaceCreation");
                options.RequireVerifiedEmailForWorkspaceCreation = ReadBoolean(
                    configuration,
                    $"{BunkFyWorkspaceAdmissionOptions.SectionName}:RequireVerifiedEmailForWorkspaceCreation",
                    defaultValue: true);
            })
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BunkFyWorkspaceAdmissionOptions>>(
            new BunkFyWorkspaceAdmissionOptionsValidator(
                requireExplicitPolicy,
                passwordRegistrationEnabled,
                externalRegistrationEnabled,
                selfServiceWorkspaceCreationEnabled)));
        services.Replace(ServiceDescriptor.Scoped<IOrganizationAdmissionPolicy, BunkFyWorkspaceAdmissionPolicy>());

        return services;
    }

    private static bool ReadBoolean(
        IConfiguration configuration,
        string key,
        bool defaultValue)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out bool parsed)
            ? parsed
            : throw new InvalidOperationException($"{key} must be either true or false.");
    }

    private static TEnum ReadEnum<TEnum>(IConfiguration configuration, string key)
        where TEnum : struct, Enum
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        return Enum.TryParse(value, ignoreCase: true, out TEnum parsed)
            ? parsed
            : (TEnum)Enum.ToObject(typeof(TEnum), -1);
    }
}
