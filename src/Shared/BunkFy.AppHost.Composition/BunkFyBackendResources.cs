namespace BunkFy.AppHost.Composition;

using Aspire.Hosting.ApplicationModel;

public sealed record BunkFyBackendResources(IResourceBuilder<ProjectResource> Api);
