namespace BunkFy.DataGovernance;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class PersonalDataCatalogJson
{
    public const int MaximumDocumentBytes = 1024 * 1024;
    public const int MaximumDocumentDepth = 32;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static PersonalDataCatalogDocument Parse(
        ReadOnlySpan<byte> utf8Json,
        PersonalDataCatalogValidationMode mode = PersonalDataCatalogValidationMode.Engineering)
    {
        if (utf8Json.Length is 0 or > MaximumDocumentBytes)
        {
            throw new InvalidDataException(
                $"The personal-data catalogue must contain between 1 and {MaximumDocumentBytes} UTF-8 bytes.");
        }

        JsonDocumentOptions documentOptions = new()
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaximumDocumentDepth
        };

        try
        {
            using JsonDocument json = JsonDocument.Parse(utf8Json.ToArray(), documentOptions);
            RejectDuplicateProperties(json.RootElement, "$", depth: 0);
            PersonalDataCatalogDocument? catalogue = json.RootElement.Deserialize<PersonalDataCatalogDocument>(
                SerializerOptions);
            if (catalogue is null)
            {
                throw new InvalidDataException("The personal-data catalogue document is empty.");
            }

            PersonalDataCatalogValidator.ValidateAndThrow(catalogue, mode);
            return catalogue;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The personal-data catalogue is not valid strict JSON.", exception);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = false,
            MaxDepth = MaximumDocumentDepth,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false));
        return options;
    }

    private static void RejectDuplicateProperties(JsonElement element, string path, int depth)
    {
        if (depth > MaximumDocumentDepth)
        {
            throw new InvalidDataException("The personal-data catalogue exceeds the supported nesting depth.");
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate JSON property '{path}.{property.Name}'.");
                }

                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}", depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item, $"{path}[{index}]", depth + 1);
                index++;
            }
        }
    }
}
