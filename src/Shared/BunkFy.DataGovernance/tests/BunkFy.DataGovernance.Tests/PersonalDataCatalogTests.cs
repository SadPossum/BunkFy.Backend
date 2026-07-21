namespace BunkFy.DataGovernance.Tests;

using System.Text;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PersonalDataCatalogTests
{
    [Fact]
    public void Strict_parser_accepts_a_complete_engineering_catalogue()
    {
        PersonalDataCatalogDocument catalogue = Parse(ValidJson);

        Assert.Equal("guests.personal-data", catalogue.CatalogId);
        Assert.Single(catalogue.Fields);
    }

    [Theory]
    [InlineData("\"unexpected\":true,")]
    [InlineData("\"catalogId\":\"duplicate\",")]
    public void Strict_parser_rejects_unknown_or_duplicate_properties(string injection)
    {
        string json = ValidJson.Replace("\"catalogId\":", injection + "\"catalogId\":", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Parse(json));
    }

    [Fact]
    public void Strict_parser_rejects_oversized_or_invalid_utf8_documents()
    {
        byte[] oversized = new byte[PersonalDataCatalogJson.MaximumDocumentBytes + 1];
        byte[] invalidUtf8 = [0xC3, 0x28];

        Assert.Throws<InvalidDataException>(() => PersonalDataCatalogJson.Parse(oversized));
        Assert.Throws<InvalidDataException>(() => PersonalDataCatalogJson.Parse(invalidUtf8));
    }

    [Fact]
    public void Strict_parser_rejects_malformed_documents()
    {
        Assert.Throws<InvalidDataException>(() => Parse("{\"schemaVersion\":"));
    }

    [Fact]
    public void Validator_rejects_duplicate_field_and_policy_identifiers()
    {
        PersonalDataCatalogDocument catalogue = Parse(ValidJson);
        PersonalDataCatalogDocument duplicate = catalogue with
        {
            AccessPolicies = [catalogue.AccessPolicies[0], catalogue.AccessPolicies[0]],
            Fields = [catalogue.Fields[0], catalogue.Fields[0]]
        };

        IReadOnlyList<string> errors = PersonalDataCatalogValidator.Validate(duplicate);

        Assert.Contains(errors, error => error.Contains("Duplicate policy identifier", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Duplicate field identifier", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_rejects_noncanonical_key_casing()
    {
        string json = ValidJson.Replace(
            "\"catalogId\": \"guests.personal-data\"",
            "\"catalogId\": \"Guests.personal-data\"",
            StringComparison.Ordinal);

        PersonalDataCatalogValidationException exception = Assert.Throws<PersonalDataCatalogValidationException>(
            () => Parse(json));

        Assert.Contains(exception.Errors, error => error.Contains("lowercase ASCII", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_rejects_unknown_policy_and_disallowed_binding_surface()
    {
        string json = ValidJson
            .Replace("\"accessPolicy\": \"guest-records\"", "\"accessPolicy\": \"missing\"", StringComparison.Ordinal)
            .Replace("\"surface\": \"persistence\"", "\"surface\": \"notification\"", StringComparison.Ordinal);

        PersonalDataCatalogValidationException exception = Assert.Throws<PersonalDataCatalogValidationException>(
            () => Parse(json));

        Assert.Contains(exception.Errors, error => error.Contains("unknown policy", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("not allowed", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_mode_rejects_unapproved_catalogue_fields_and_retention()
    {
        PersonalDataCatalogValidationException exception = Assert.Throws<PersonalDataCatalogValidationException>(
            () => PersonalDataCatalogJson.Parse(
                Encoding.UTF8.GetBytes(ValidJson),
                PersonalDataCatalogValidationMode.Production));

        Assert.Contains(exception.Errors, error => error.Contains("catalogue must be approved", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("RetentionPolicies", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("Fields", StringComparison.Ordinal));
    }

    [Fact]
    public void Inventory_rendering_is_deterministic_and_resolves_policy_metadata()
    {
        PersonalDataCatalogDocument catalogue = Parse(ValidJson);

        string first = PersonalDataInventoryRenderer.RenderMarkdown(catalogue);
        string second = PersonalDataInventoryRenderer.RenderMarkdown(catalogue);

        Assert.Equal(first, second);
        Assert.Contains("## Access Policies", first, StringComparison.Ordinal);
        Assert.Contains("guest.profile.display-name", first, StringComparison.Ordinal);
        Assert.Contains("GuestProfile", first, StringComparison.Ordinal);
    }

    private static PersonalDataCatalogDocument Parse(string json) =>
        PersonalDataCatalogJson.Parse(Encoding.UTF8.GetBytes(json));

    private const string ValidJson = /*lang=json,strict*/ """
        {
          "schemaVersion": 1,
          "catalogId": "guests.personal-data",
          "catalogVersion": 1,
          "module": "guests",
          "approvalState": "engineering-default",
          "accessPolicies": [
            {
              "id": "guest-records",
              "scope": "tenant-property-authorized",
              "readers": ["permission:guests.read"],
              "writers": ["permission:guests.manage"]
            }
          ],
          "retentionPolicies": [
            {
              "id": "guest-profile-lifecycle",
              "approvalState": "engineering-default",
              "startsAt": "guest-profile-created",
              "endsAt": "erasure-or-tenant-termination",
              "legalHoldBehavior": "pause-approved-erasure"
            }
          ],
          "rightsPolicies": [
            {
              "id": "guest-profile-editable",
              "export": "include",
              "correction": "replace",
              "restriction": "suppress-operational-use",
              "erasure": "anonymize"
            }
          ],
          "fields": [
            {
              "id": "guest.profile.display-name",
              "dataSubject": "guest",
              "classification": "direct-identifier",
              "sensitivity": "standard",
              "purposes": ["guest-identification"],
              "sources": ["staff-entry"],
              "authoritativeOwner": "guests",
              "controllerProcessorContext": "customer-controller-bunk-fy-processor",
              "accessPolicy": "guest-records",
              "countryPolicyKey": "guest.profile.identity",
              "retentionPolicy": "guest-profile-lifecycle",
              "rightsPolicy": "guest-profile-editable",
              "allowedSurfaces": ["persistence", "api-response"],
              "allowedBoundaries": ["intra-module", "customer-api"],
              "approvalState": "engineering-default",
              "bindings": [
                {
                  "assembly": "BunkFy.Modules.Guests.Domain",
                  "type": "BunkFy.Modules.Guests.Domain.Aggregates.GuestProfile",
                  "member": "DisplayName",
                  "surface": "persistence"
                }
              ]
            }
          ]
        }
        """;
}
