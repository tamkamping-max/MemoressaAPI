using System.Text.Json;

namespace Memoressa.Application.Common;

public static class PhotoPrivacyIdsJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IReadOnlyList<Guid> ReadGuids(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<Guid>>(json, Options);
            return parsed?.Where(id => id != Guid.Empty).Distinct().ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string WriteGuids(IReadOnlyList<Guid> ids)
    {
        var distinct = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        return JsonSerializer.Serialize(distinct, Options);
    }
}
