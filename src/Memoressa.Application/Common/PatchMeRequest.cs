using System.Text.Json;

namespace Memoressa.Application.Common;

/// <summary>Partial PATCH /auth/me — tracks which JSON properties were sent.</summary>
public sealed class PatchMeRequest
{
    private readonly HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

    public Guid? SelfFamilyMemberId { get; private set; }
    public string? Nickname { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTime? BirthDate { get; private set; }
    public string? ProfileCityId { get; private set; }

    public bool IsSet(string jsonName) => _set.Contains(jsonName);

    public static PatchMeRequest FromJson(JsonElement json)
    {
        var request = new PatchMeRequest();
        if (json.ValueKind != JsonValueKind.Object)
        {
            return request;
        }

        foreach (var property in json.EnumerateObject())
        {
            request._set.Add(property.Name);
            switch (property.Name.ToLowerInvariant())
            {
                case "selffamilymemberid":
                    request.SelfFamilyMemberId = ReadNullableGuid(property.Value);
                    break;
                case "nickname":
                    request.Nickname = ReadNullableString(property.Value);
                    break;
                case "avatarurl":
                    request.AvatarUrl = ReadNullableString(property.Value);
                    break;
                case "birthdate":
                    request.BirthDate = ReadNullableDateTime(property.Value);
                    break;
                case "profilecityid":
                    request.ProfileCityId = ReadNullableString(property.Value);
                    break;
            }
        }

        return request;
    }

    private static string? ReadNullableString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => null
        };

    private static Guid? ReadNullableGuid(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String when Guid.TryParse(value.GetString(), out var id) => id,
            _ => null
        };

    private static DateTime? ReadNullableDateTime(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String
            && DateTime.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
