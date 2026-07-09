namespace Catalog.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(CatalogItemStatusJsonConverter))]
public enum CatalogItemStatus
{
    Unknown = 0,
    Active = 1,
    Discontinued = 2
}
