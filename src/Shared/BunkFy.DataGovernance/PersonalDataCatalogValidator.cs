namespace BunkFy.DataGovernance;

using System.Globalization;

public static class PersonalDataCatalogValidator
{
    public static IReadOnlyList<string> Validate(
        PersonalDataCatalogDocument catalogue,
        PersonalDataCatalogValidationMode mode = PersonalDataCatalogValidationMode.Engineering)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        List<string> errors = [];

        if (catalogue.SchemaVersion != 1)
        {
            errors.Add("SchemaVersion must be 1.");
        }

        ValidateKey(catalogue.CatalogId, "CatalogId", errors);
        ValidateKey(catalogue.Module, "Module", errors);
        if (catalogue.CatalogVersion <= 0)
        {
            errors.Add("CatalogVersion must be greater than zero.");
        }

        ValidateEnum(catalogue.ApprovalState, "ApprovalState", errors);
        if (mode == PersonalDataCatalogValidationMode.Production &&
            catalogue.ApprovalState != PersonalDataPolicyApprovalState.Approved)
        {
            errors.Add("The catalogue must be approved before production activation.");
        }

        Dictionary<string, PersonalDataAccessPolicy> accessPolicies = IndexPolicies(
            catalogue.AccessPolicies,
            policy => policy.Id,
            "AccessPolicies",
            errors);
        Dictionary<string, PersonalDataRetentionPolicy> retentionPolicies = IndexPolicies(
            catalogue.RetentionPolicies,
            policy => policy.Id,
            "RetentionPolicies",
            errors);
        Dictionary<string, PersonalDataRightsPolicy> rightsPolicies = IndexPolicies(
            catalogue.RightsPolicies,
            policy => policy.Id,
            "RightsPolicies",
            errors);

        foreach (PersonalDataAccessPolicy policy in catalogue.AccessPolicies ?? [])
        {
            ValidateKey(policy.Id, $"AccessPolicies[{policy.Id}].Id", errors);
            ValidateKey(policy.Scope, $"AccessPolicies[{policy.Id}].Scope", errors);
            ValidateValues(policy.Readers, $"AccessPolicies[{policy.Id}].Readers", errors);
            ValidateValues(policy.Writers, $"AccessPolicies[{policy.Id}].Writers", errors);
        }

        foreach (PersonalDataRetentionPolicy policy in catalogue.RetentionPolicies ?? [])
        {
            ValidateKey(policy.Id, $"RetentionPolicies[{policy.Id}].Id", errors);
            ValidateEnum(policy.ApprovalState, $"RetentionPolicies[{policy.Id}].ApprovalState", errors);
            ValidateKey(policy.StartsAt, $"RetentionPolicies[{policy.Id}].StartsAt", errors);
            ValidateKey(policy.LegalHoldBehavior, $"RetentionPolicies[{policy.Id}].LegalHoldBehavior", errors);
            bool hasEnd = !string.IsNullOrWhiteSpace(policy.EndsAt);
            bool hasDuration = !string.IsNullOrWhiteSpace(policy.Duration);
            if (hasEnd == hasDuration)
            {
                errors.Add($"RetentionPolicies[{policy.Id}] must declare exactly one of EndsAt or Duration.");
            }

            if (hasEnd)
            {
                ValidateKey(policy.EndsAt!, $"RetentionPolicies[{policy.Id}].EndsAt", errors);
            }

            if (hasDuration)
            {
                if (!TimeSpan.TryParse(policy.Duration, CultureInfo.InvariantCulture, out TimeSpan duration))
                {
                    errors.Add($"RetentionPolicies[{policy.Id}].Duration must be an invariant TimeSpan value.");
                }
                else if (duration <= TimeSpan.Zero)
                {
                    errors.Add($"RetentionPolicies[{policy.Id}].Duration must be greater than zero.");
                }
            }

            if (mode == PersonalDataCatalogValidationMode.Production &&
                policy.ApprovalState != PersonalDataPolicyApprovalState.Approved)
            {
                errors.Add($"RetentionPolicies[{policy.Id}] must be approved before production activation.");
            }
        }

        foreach (PersonalDataRightsPolicy policy in catalogue.RightsPolicies ?? [])
        {
            ValidateKey(policy.Id, $"RightsPolicies[{policy.Id}].Id", errors);
            ValidateKey(policy.Export, $"RightsPolicies[{policy.Id}].Export", errors);
            ValidateKey(policy.Correction, $"RightsPolicies[{policy.Id}].Correction", errors);
            ValidateKey(policy.Restriction, $"RightsPolicies[{policy.Id}].Restriction", errors);
            ValidateKey(policy.Erasure, $"RightsPolicies[{policy.Id}].Erasure", errors);
        }

        HashSet<string> fieldIds = new(StringComparer.Ordinal);
        HashSet<string> bindingKeys = new(StringComparer.Ordinal);
        if (catalogue.Fields is null || catalogue.Fields.Length == 0)
        {
            errors.Add("Fields must contain at least one definition.");
        }

        foreach (PersonalDataFieldDefinition field in catalogue.Fields ?? [])
        {
            string fieldPath = $"Fields[{field.Id}]";
            ValidateKey(field.Id, $"{fieldPath}.Id", errors);
            if (!fieldIds.Add(field.Id ?? string.Empty))
            {
                errors.Add($"Duplicate field identifier '{field.Id}'.");
            }

            ValidateEnum(field.DataSubject, $"{fieldPath}.DataSubject", errors);
            ValidateEnum(field.Classification, $"{fieldPath}.Classification", errors);
            ValidateEnum(field.Sensitivity, $"{fieldPath}.Sensitivity", errors);
            ValidateValues(field.Purposes, $"{fieldPath}.Purposes", errors, keysOnly: true);
            ValidateValues(field.Sources, $"{fieldPath}.Sources", errors, keysOnly: true);
            ValidateKey(field.AuthoritativeOwner, $"{fieldPath}.AuthoritativeOwner", errors);
            ValidateEnum(field.ControllerProcessorContext, $"{fieldPath}.ControllerProcessorContext", errors);
            ValidateReference(field.AccessPolicy, accessPolicies, $"{fieldPath}.AccessPolicy", errors);
            ValidateKey(field.CountryPolicyKey, $"{fieldPath}.CountryPolicyKey", errors);
            ValidateReference(field.RetentionPolicy, retentionPolicies, $"{fieldPath}.RetentionPolicy", errors);
            ValidateReference(field.RightsPolicy, rightsPolicies, $"{fieldPath}.RightsPolicy", errors);
            ValidateEnumValues(field.AllowedSurfaces, $"{fieldPath}.AllowedSurfaces", errors);
            ValidateEnumValues(field.AllowedBoundaries, $"{fieldPath}.AllowedBoundaries", errors);
            ValidateEnum(field.ApprovalState, $"{fieldPath}.ApprovalState", errors);
            if (mode == PersonalDataCatalogValidationMode.Production &&
                field.ApprovalState != PersonalDataPolicyApprovalState.Approved)
            {
                errors.Add($"{fieldPath} must be approved before production activation.");
            }

            if (field.Bindings is null || field.Bindings.Length == 0)
            {
                errors.Add($"{fieldPath}.Bindings must contain at least one member binding.");
                continue;
            }

            HashSet<PersonalDataSurface> allowedSurfaces = [.. field.AllowedSurfaces ?? []];
            HashSet<PersonalDataSurface> boundSurfaces = [];
            foreach (PersonalDataMemberBinding binding in field.Bindings)
            {
                ValidateText(binding.Assembly, $"{fieldPath}.Bindings.Assembly", errors);
                ValidateText(binding.Type, $"{fieldPath}.Bindings.Type", errors);
                ValidateText(binding.Member, $"{fieldPath}.Bindings.Member", errors);
                ValidateEnum(binding.Surface, $"{fieldPath}.Bindings.Surface", errors);
                if (binding.RetentionPolicy is not null)
                {
                    ValidateReference(
                        binding.RetentionPolicy,
                        retentionPolicies,
                        $"{fieldPath}.Bindings.RetentionPolicy",
                        errors);
                }

                if (!allowedSurfaces.Contains(binding.Surface))
                {
                    errors.Add(
                        $"Binding '{binding.Type}.{binding.Member}' uses surface '{binding.Surface}' " +
                        $"which is not allowed by field '{field.Id}'.");
                }

                boundSurfaces.Add(binding.Surface);

                string bindingKey = string.Join('|', binding.Assembly, binding.Type, binding.Member, binding.Surface);
                if (!bindingKeys.Add(bindingKey))
                {
                    errors.Add($"Duplicate member binding '{bindingKey}'.");
                }
            }

            foreach (PersonalDataSurface allowedSurface in allowedSurfaces.Where(
                         surface => !boundSurfaces.Contains(surface)))
            {
                errors.Add(
                    $"Field '{field.Id}' allows surface '{allowedSurface}' without a concrete member binding.");
            }
        }

        return errors;
    }

    public static void ValidateAndThrow(
        PersonalDataCatalogDocument catalogue,
        PersonalDataCatalogValidationMode mode = PersonalDataCatalogValidationMode.Engineering)
    {
        IReadOnlyList<string> errors = Validate(catalogue, mode);
        if (errors.Count > 0)
        {
            throw new PersonalDataCatalogValidationException(errors);
        }
    }

    private static Dictionary<string, T> IndexPolicies<T>(
        T[]? policies,
        Func<T, string> id,
        string name,
        List<string> errors)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        if (policies is null || policies.Length == 0)
        {
            errors.Add($"{name} must contain at least one policy.");
            return result;
        }

        foreach (T policy in policies)
        {
            string policyId = id(policy) ?? string.Empty;
            if (!result.TryAdd(policyId, policy))
            {
                errors.Add($"Duplicate policy identifier '{policyId}' in {name}.");
            }
        }

        return result;
    }

    private static void ValidateReference<T>(
        string? value,
        IReadOnlyDictionary<string, T> policies,
        string path,
        List<string> errors)
    {
        ValidateKey(value, path, errors);
        if (!string.IsNullOrWhiteSpace(value) && !policies.ContainsKey(value))
        {
            errors.Add($"{path} references unknown policy '{value}'.");
        }
    }

    private static void ValidateEnum<T>(T value, string path, List<string> errors)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value) || Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0)
        {
            errors.Add($"{path} must be a supported non-default value.");
        }
    }

    private static void ValidateEnumValues<T>(T[]? values, string path, List<string> errors)
        where T : struct, Enum
    {
        if (values is null || values.Length == 0)
        {
            errors.Add($"{path} must contain at least one value.");
            return;
        }

        HashSet<T> unique = [];
        foreach (T value in values)
        {
            ValidateEnum(value, path, errors);
            if (!unique.Add(value))
            {
                errors.Add($"{path} contains duplicate value '{value}'.");
            }
        }
    }

    private static void ValidateValues(
        string[]? values,
        string path,
        List<string> errors,
        bool keysOnly = false)
    {
        if (values is null || values.Length == 0)
        {
            errors.Add($"{path} must contain at least one value.");
            return;
        }

        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (keysOnly)
            {
                ValidateKey(value, path, errors);
            }
            else
            {
                ValidateText(value, path, errors);
            }

            if (!unique.Add(value ?? string.Empty))
            {
                errors.Add($"{path} contains duplicate value '{value}'.");
            }
        }
    }

    private static void ValidateKey(string? value, string path, List<string> errors)
    {
        ValidateText(value, path, errors);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!char.IsAsciiLetterOrDigit(value[0]) || value.Any(character =>
                character is >= 'A' and <= 'Z' ||
                (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_')))
        {
            errors.Add(
                $"{path} must start with a lowercase ASCII letter or digit and contain only lowercase ASCII letters, " +
                "digits, '.', '-', or '_'.");
        }
    }

    private static void ValidateText(string? value, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Any(char.IsControl))
        {
            errors.Add($"{path} must be non-empty, trimmed text without control characters.");
        }
    }
}

public sealed class PersonalDataCatalogValidationException(IReadOnlyList<string> errors)
    : Exception("The personal-data catalogue is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
