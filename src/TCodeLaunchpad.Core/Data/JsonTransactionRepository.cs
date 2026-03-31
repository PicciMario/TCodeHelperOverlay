using System.Text.Json;
using System.Text.Json.Serialization;
using TCodeLaunchpad.Core.Models;

namespace TCodeLaunchpad.Core.Data;

public sealed class JsonTransactionRepository : ITransactionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public IReadOnlyList<TCodeEntry> Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JSON data file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        var rows = JsonSerializer.Deserialize<List<Row>>(json, SerializerOptions) ?? new List<Row>();

        return rows
            .Where(static x => !string.IsNullOrWhiteSpace(x.Code))
            .Select(static x => new TCodeEntry(
                x.Code!.Trim(),
                x.Descr?.Trim() ?? string.Empty,
                x.Keywords?.Trim() ?? string.Empty,
                x.LongDescr?.Trim() ?? string.Empty,
                ParseModule(x.Module),
                ParseBusinessObject(x.BusinessObject)))
            .ToList();
    }

    private static ModuleInfo? ParseModule(JsonElement moduleElement)
    {
        if (moduleElement.ValueKind == JsonValueKind.Null || moduleElement.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (moduleElement.ValueKind == JsonValueKind.String)
        {
            var value = moduleElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // Legacy compatibility: old schema stored module as a single string.
            return new ModuleInfo(value, value);
        }

        if (moduleElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var code = moduleElement.TryGetProperty("code", out var codeElement)
            ? codeElement.GetString()?.Trim() ?? string.Empty
            : string.Empty;

        var name = moduleElement.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()?.Trim() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new ModuleInfo(code, name);
    }

    private static BusinessObject? ParseBusinessObject(JsonElement businessObjectElement)
    {
        if (businessObjectElement.ValueKind == JsonValueKind.Null || businessObjectElement.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (businessObjectElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var code = businessObjectElement.TryGetProperty("code", out var codeElement)
            ? codeElement.GetString()?.Trim() ?? string.Empty
            : string.Empty;

        var name = businessObjectElement.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()?.Trim() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new BusinessObject(code, name);
    }

    private sealed class Row
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("descr")]
        public string? Descr { get; init; }

        [JsonPropertyName("keywords")]
        public string? Keywords { get; init; }

        [JsonPropertyName("long_descr")]
        public string? LongDescr { get; init; }

        [JsonPropertyName("module")]
        public JsonElement Module { get; init; }

        [JsonPropertyName("business_object")]
        public JsonElement BusinessObject { get; init; }
    }
}
