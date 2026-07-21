namespace BunkFy.DataGovernance;

using System.Globalization;
using System.Text;
using System.Text.Json;

public static class PersonalDataInventoryRenderer
{
    public static string RenderMarkdown(PersonalDataCatalogDocument catalogue)
    {
        PersonalDataCatalogValidator.ValidateAndThrow(catalogue);
        StringBuilder output = new();
        output.AppendLine(CultureInfo.InvariantCulture, $"# {catalogue.Module} Personal-Data Inventory v{catalogue.CatalogVersion}");
        output.AppendLine();
        output.AppendLine(CultureInfo.InvariantCulture, $"Generated from `{catalogue.CatalogId}` schema v{catalogue.SchemaVersion}.");
        output.AppendLine(CultureInfo.InvariantCulture, $"Catalogue approval: `{Kebab(catalogue.ApprovalState)}`.");
        output.AppendLine();
        output.AppendLine("Engineering metadata is not legal or country-launch approval.");
        output.AppendLine();

        RenderPolicies(output, catalogue);
        RenderFields(output, catalogue);
        RenderBindings(output, catalogue);
        return output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void RenderPolicies(StringBuilder output, PersonalDataCatalogDocument catalogue)
    {
        output.AppendLine("## Access Policies");
        output.AppendLine();
        output.AppendLine("| Id | Scope | Readers | Writers |");
        output.AppendLine("|---|---|---|---|");
        foreach (PersonalDataAccessPolicy policy in catalogue.AccessPolicies.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"| {Cell(policy.Id)} | {Cell(policy.Scope)} | {Join(policy.Readers)} | {Join(policy.Writers)} |");
        }

        output.AppendLine();
        output.AppendLine("## Retention Policies");
        output.AppendLine();
        output.AppendLine("| Id | Approval | Starts | Ends or duration | Legal hold |");
        output.AppendLine("|---|---|---|---|---|");
        foreach (PersonalDataRetentionPolicy policy in catalogue.RetentionPolicies.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            string end = policy.EndsAt ?? policy.Duration ?? string.Empty;
            output.AppendLine(CultureInfo.InvariantCulture,
                $"| {Cell(policy.Id)} | {Kebab(policy.ApprovalState)} | {Cell(policy.StartsAt)} | {Cell(end)} | {Cell(policy.LegalHoldBehavior)} |");
        }

        output.AppendLine();
        output.AppendLine("## Rights Policies");
        output.AppendLine();
        output.AppendLine("| Id | Export | Correction | Restriction | Erasure |");
        output.AppendLine("|---|---|---|---|---|");
        foreach (PersonalDataRightsPolicy policy in catalogue.RightsPolicies.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            output.AppendLine(CultureInfo.InvariantCulture,
                $"| {Cell(policy.Id)} | {Cell(policy.Export)} | {Cell(policy.Correction)} | {Cell(policy.Restriction)} | {Cell(policy.Erasure)} |");
        }

        output.AppendLine();
    }

    private static void RenderFields(StringBuilder output, PersonalDataCatalogDocument catalogue)
    {
        output.AppendLine("## Fields");
        output.AppendLine();
        output.AppendLine("| Id | Subject | Class | Sensitivity | Purposes | Sources | Owner | Context | Access | Country | Retention | Rights | Surfaces | Boundaries | Approval |");
        output.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (PersonalDataFieldDefinition field in catalogue.Fields.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            output.AppendLine(CultureInfo.InvariantCulture,
                $"| {Cell(field.Id)} | {Kebab(field.DataSubject)} | {Kebab(field.Classification)} | {Kebab(field.Sensitivity)} | " +
                $"{Join(field.Purposes)} | {Join(field.Sources)} | {Cell(field.AuthoritativeOwner)} | {Kebab(field.ControllerProcessorContext)} | " +
                $"{Cell(field.AccessPolicy)} | {Cell(field.CountryPolicyKey)} | {Cell(field.RetentionPolicy)} | {Cell(field.RightsPolicy)} | " +
                $"{Join(field.AllowedSurfaces.Select(Kebab))} | {Join(field.AllowedBoundaries.Select(Kebab))} | {Kebab(field.ApprovalState)} |");
        }

        output.AppendLine();
    }

    private static void RenderBindings(StringBuilder output, PersonalDataCatalogDocument catalogue)
    {
        output.AppendLine("## Code Bindings");
        output.AppendLine();
        output.AppendLine("| Field | Assembly | Type | Member | Surface | Effective retention |");
        output.AppendLine("|---|---|---|---|---|---|");
        foreach ((PersonalDataFieldDefinition field, PersonalDataMemberBinding binding) in catalogue.Fields
                     .SelectMany(field => field.Bindings.Select(binding => (field, binding)))
                     .OrderBy(item => item.field.Id, StringComparer.Ordinal)
                     .ThenBy(item => item.binding.Assembly, StringComparer.Ordinal)
                     .ThenBy(item => item.binding.Type, StringComparer.Ordinal)
                     .ThenBy(item => item.binding.Member, StringComparer.Ordinal)
                     .ThenBy(item => item.binding.Surface))
        {
            output.AppendLine(CultureInfo.InvariantCulture,
                $"| {Cell(field.Id)} | {Cell(binding.Assembly)} | {Cell(binding.Type)} | {Cell(binding.Member)} | " +
                $"{Kebab(binding.Surface)} | {Cell(binding.RetentionPolicy ?? field.RetentionPolicy)} |");
        }
    }

    private static string Join(IEnumerable<string> values) =>
        string.Join("<br>", values.Order(StringComparer.Ordinal).Select(Cell));

    private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string Kebab<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
}
