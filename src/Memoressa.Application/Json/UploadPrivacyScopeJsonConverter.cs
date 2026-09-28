using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Json;

public sealed class UploadPrivacyScopeJsonConverter : JsonConverter<UploadPrivacyScope>
{
    private static readonly Dictionary<string, UploadPrivacyScope> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["onlySelf"] = UploadPrivacyScope.OnlySelf,
            ["only_self"] = UploadPrivacyScope.OnlySelf,
            ["self"] = UploadPrivacyScope.OnlySelf,
            ["personal"] = UploadPrivacyScope.OnlySelf,
            ["private"] = UploadPrivacyScope.OnlySelf,
            ["family"] = UploadPrivacyScope.Family,
            ["friends"] = UploadPrivacyScope.Friends,
            ["friendsAndFamily"] = UploadPrivacyScope.FriendsAndFamily,
            ["friends_and_family"] = UploadPrivacyScope.FriendsAndFamily,
            ["friendsFamily"] = UploadPrivacyScope.FriendsAndFamily,
            ["custom"] = UploadPrivacyScope.Custom,
            ["specificMembers"] = UploadPrivacyScope.Custom,
            ["specific_members"] = UploadPrivacyScope.Custom
        };

    public override UploadPrivacyScope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
            case JsonTokenType.None:
                return UploadPrivacyScope.Family;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var numeric) && Enum.IsDefined(typeof(UploadPrivacyScope), numeric))
                {
                    return (UploadPrivacyScope)numeric;
                }

                break;
            case JsonTokenType.String:
                var raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return UploadPrivacyScope.Family;
                }

                if (Aliases.TryGetValue(raw.Trim(), out var mapped))
                {
                    return mapped;
                }

                if (Enum.TryParse<UploadPrivacyScope>(raw, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }

                break;
        }

        throw new JsonException(
            "Invalid privacyScope. Allowed: onlySelf, family, friends, friendsAndFamily, custom (aliases: private, specificMembers).");
    }

    public override void Write(Utf8JsonWriter writer, UploadPrivacyScope value, JsonSerializerOptions options)
    {
        var name = value switch
        {
            UploadPrivacyScope.OnlySelf => "onlySelf",
            UploadPrivacyScope.Family => "family",
            UploadPrivacyScope.Friends => "friends",
            UploadPrivacyScope.FriendsAndFamily => "friendsAndFamily",
            UploadPrivacyScope.Custom => "custom",
            _ => value.ToString()
        };
        writer.WriteStringValue(name);
    }
}
