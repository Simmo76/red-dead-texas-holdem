using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using CinematicPoker.Engine.Coaching;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Web.Coaching
{
    /// <summary>Cards persist as "As" / "Th" codes. Rank and suit enums do not round-trip on their own.</summary>
    public sealed class CardJsonConverter : JsonConverter<Card>
    {
        public override Card Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("A card must be stored as a code such as \"As\".");
            return Card.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, Card value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(BestFiveCards.Code(value));
        }
    }
}
