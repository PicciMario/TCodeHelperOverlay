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
                x.Module?.Trim() ?? string.Empty,
                x.BusinessObject is null
                    ? null
                    : new BusinessObject(
                        x.BusinessObject.Code?.Trim() ?? string.Empty,
                        x.BusinessObject.Name?.Trim() ?? string.Empty)))
            .ToList();
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
        public string? Module { get; init; }

        [JsonPropertyName("business_object")]
        public BusinessObjectRow? BusinessObject { get; init; }
    }

    private sealed class BusinessObjectRow
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
