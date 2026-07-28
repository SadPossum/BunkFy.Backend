namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Modules.Ingestion.Domain.Errors;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ObservationAdapterProvenanceTests
{
    [Fact]
    public void Credential_provenance_requires_a_customer_owner()
    {
        var result = ObservationAdapterProvenance.Create(
            Guid.NewGuid(),
            "fake.http",
            1,
            1,
            "booking",
            customerOwner: null);

        Assert.Equal(IngestionDomainErrors.ReceiptProvenanceInvalid, result.Error);
    }

    [Fact]
    public void Local_provenance_normalizes_stable_adapter_metadata()
    {
        var result = ObservationAdapterProvenance.Create(
            credentialId: null,
            " Fake.Http ",
            1,
            2,
            " Booking ",
            customerOwner: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("fake.http", result.Value.AdapterType);
        Assert.Equal(1, result.Value.AdapterProtocolVersion);
        Assert.Equal(2, result.Value.ConfigurationSchemaVersion);
        Assert.Equal("booking", result.Value.SourceSystem);
        Assert.Null(result.Value.CustomerOwner);
    }
}
