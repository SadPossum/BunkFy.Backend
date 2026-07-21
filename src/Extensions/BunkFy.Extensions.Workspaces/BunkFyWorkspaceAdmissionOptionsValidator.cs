namespace BunkFy.Extensions.Workspaces;

using Microsoft.Extensions.Options;

internal sealed class BunkFyWorkspaceAdmissionOptionsValidator(
    bool requireExplicitPolicy,
    bool passwordRegistrationEnabled,
    bool externalRegistrationEnabled,
    bool selfServiceWorkspaceCreationEnabled)
    : IValidateOptions<BunkFyWorkspaceAdmissionOptions>
{
    public ValidateOptionsResult Validate(string? name, BunkFyWorkspaceAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!Enum.IsDefined(options.AccountRegistration))
        {
            failures.Add($"{BunkFyWorkspaceAdmissionOptions.SectionName}:AccountRegistration is invalid.");
        }
        else if (requireExplicitPolicy && options.AccountRegistration == BunkFyAccountRegistrationMode.Unspecified)
        {
            failures.Add($"{BunkFyWorkspaceAdmissionOptions.SectionName}:AccountRegistration must be explicit in production.");
        }

        if (!Enum.IsDefined(options.WorkspaceCreation))
        {
            failures.Add($"{BunkFyWorkspaceAdmissionOptions.SectionName}:WorkspaceCreation is invalid.");
        }
        else if (requireExplicitPolicy && options.WorkspaceCreation == BunkFyWorkspaceCreationMode.Unspecified)
        {
            failures.Add($"{BunkFyWorkspaceAdmissionOptions.SectionName}:WorkspaceCreation must be explicit in production.");
        }

        bool accountRegistrationEnabled = passwordRegistrationEnabled || externalRegistrationEnabled;
        if (options.AccountRegistration == BunkFyAccountRegistrationMode.Public && !accountRegistrationEnabled)
        {
            failures.Add("Public account registration requires at least one Auth self-registration method.");
        }
        else if (options.AccountRegistration == BunkFyAccountRegistrationMode.Disabled && accountRegistrationEnabled)
        {
            failures.Add("Disabled account registration requires all Auth self-registration methods to be disabled.");
        }

        if (options.WorkspaceCreation == BunkFyWorkspaceCreationMode.SelfService && !selfServiceWorkspaceCreationEnabled)
        {
            failures.Add("Self-service workspace creation requires Organizations self-service creation to be enabled.");
        }
        else if (options.WorkspaceCreation == BunkFyWorkspaceCreationMode.Disabled && selfServiceWorkspaceCreationEnabled)
        {
            failures.Add("Disabled workspace creation requires Organizations self-service creation to be disabled.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
