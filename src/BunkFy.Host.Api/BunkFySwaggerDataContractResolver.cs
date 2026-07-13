namespace BunkFy.Host.Api;

using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Modules.Properties.Contracts;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

internal sealed class BunkFySwaggerDataContractResolver : ISerializerDataContractResolver
{
    private readonly JsonSerializerDataContractResolver inner;

    public BunkFySwaggerDataContractResolver(IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions)
    {
        JsonSerializerOptions serializerOptions = new(jsonOptions.Value.SerializerOptions);
        serializerOptions.Converters.Insert(
            0,
            new JsonStringEnumConverter<PropertyStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        serializerOptions.Converters.Insert(
            0,
            new JsonStringEnumConverter<RoomStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        serializerOptions.Converters.Insert(
            0,
            new JsonStringEnumConverter<BedStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        this.inner = new JsonSerializerDataContractResolver(serializerOptions);
    }

    public DataContract GetDataContractForType(Type type) => this.inner.GetDataContractForType(type);
}
