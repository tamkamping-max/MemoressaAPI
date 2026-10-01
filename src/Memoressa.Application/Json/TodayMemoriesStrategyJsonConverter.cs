using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Application.Common;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Json;

public sealed class TodayMemoriesStrategyJsonConverter : JsonConverter<TodayMemoriesStrategy>
{
    public override TodayMemoriesStrategy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
        {
            return Enum.IsDefined(typeof(TodayMemoriesStrategy), numeric)
                ? (TodayMemoriesStrategy)numeric
                : TodayMemoriesStrategy.Empty;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return TodayMemoriesPresentation.StrategyFromSlug(reader.GetString());
        }

        return TodayMemoriesStrategy.Empty;
    }

    public override void Write(Utf8JsonWriter writer, TodayMemoriesStrategy value, JsonSerializerOptions options) =>
        writer.WriteStringValue(TodayMemoriesPresentation.StrategySlug(value));
}
