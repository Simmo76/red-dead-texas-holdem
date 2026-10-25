using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using CinematicPoker.Engine.Coaching;

namespace CinematicPoker.Web.Coaching
{
    /// <summary>
    /// Local history file. Cards go through <see cref="CardJsonConverter"/>
    /// so a reload does not turn every card into the zero club.
    /// </summary>
    public static class CoachJson
    {
        public static readonly JsonSerializerOptions Options = Create();

        private static JsonSerializerOptions Create()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new CardJsonConverter());
            return options;
        }
    }

    public sealed class CoachHistoryFile
    {
        public int Version { get; set; } = CoachSchema.Version;
        public List<CompletedHandRecord> Hands { get; set; } = new List<CompletedHandRecord>();
    }
}
