namespace BunkFy.Host.Api;

using BunkFy.Modules.Stations.Api;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>PIN inputs are write-only; they never belong in generated response examples.</summary>
public sealed class StationPinSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Type != typeof(StationOwnPinRequest) && context.Type != typeof(StationUnlockRequest) &&
            context.Type != typeof(StationRedeemSetupRequest))
        { return; }
        if (schema is OpenApiSchema request && request.Properties?.TryGetValue("pin", out IOpenApiSchema? field) == true &&
            field is OpenApiSchema pin)
        {
            pin.WriteOnly = true;
            pin.Format = "password";
        }
    }
}
