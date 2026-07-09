namespace Properties.Tests;

using Properties.Contracts;
using Gma.Framework.Authorization;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesProfileTests
{
    [Fact]
    public void Default_profile_documents_properties_runtime_dependencies()
    {
        ModuleProfileDescriptor profile = PropertiesProfiles.Default;

        Assert.Equal(PropertiesModuleMetadata.Name, profile.ModuleName);
        Assert.Equal(PropertiesProfiles.DefaultName, profile.ProfileName);
        Assert.Contains(profile.Provides, feature => feature.Id == PropertiesCompositionFeatures.PhysicalSetup);
        Assert.Contains(profile.Requires, feature => feature.Id == TenancyCompositionFeatures.Context);
        Assert.Contains(profile.Requires, feature => feature.Id == MessagingCompositionFeatures.Outbox);
    }

    [Fact]
    public void Descriptor_exposes_profile_permissions_and_events()
    {
        ModuleProfileDescriptor profile = Assert.Single(PropertiesModuleMetadata.Descriptor.GetCompositionProfiles());

        Assert.Equal(PropertiesProfiles.DefaultName, profile.ProfileName);
        Assert.Equal(4, PropertiesModuleMetadata.Descriptor.GetPermissions().Count);
        Assert.Equal(8, PropertiesModuleMetadata.Descriptor.GetPublishedEvents().Count);
    }
}
