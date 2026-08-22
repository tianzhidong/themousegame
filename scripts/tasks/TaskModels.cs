#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewGameProject.Tasks;

public enum TaskRunState
{
    Locked,
    Pending,
    Active,
    Completed
}

public sealed class DayTaskConfig
{
    [JsonPropertyName("day_id")]
    [JsonConverter(typeof(FlexibleInt32JsonConverter))]
    public int DayId { get; set; }

    [JsonPropertyName("day_title")]
    public string DayTitle { get; set; } = string.Empty;

    [JsonPropertyName("task_mode")]
    public string TaskMode { get; set; } = "sequence";

    [JsonPropertyName("time_limit_seconds")]
    [JsonConverter(typeof(FlexibleInt32JsonConverter))]
    public int TimeLimitSeconds { get; set; } = 900;

    [JsonPropertyName("tasks")]
    public List<TaskDefinition> Tasks { get; set; } = [];
}

public sealed class TaskDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("icon_path")]
    public string IconPath { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("target_type")]
    public string TargetType { get; set; } = string.Empty;

    [JsonPropertyName("target_color")]
    public string TargetColor { get; set; } = string.Empty;

    [JsonPropertyName("target_tag")]
    public string TargetTag { get; set; } = string.Empty;

    [JsonPropertyName("target_ids")]
    public List<string> TargetIds { get; set; } = [];

    [JsonPropertyName("target_count")]
    [JsonConverter(typeof(FlexibleInt32JsonConverter))]
    public int TargetCount { get; set; }

    [JsonPropertyName("progress_style")]
    public string ProgressStyle { get; set; } = "dots";

    [JsonPropertyName("progress_mode")]
    public string ProgressMode { get; set; } = "per_action";

    [JsonPropertyName("order")]
    [JsonConverter(typeof(FlexibleInt32JsonConverter))]
    public int Order { get; set; }

    [JsonPropertyName("hint_image_path")]
    public string HintImagePath { get; set; } = string.Empty;

    [JsonPropertyName("hint_audio_path")]
    public string HintAudioPath { get; set; } = string.Empty;
}

public sealed class TaskRuntimeState
{
    public TaskRuntimeState(TaskDefinition definition)
    {
        Definition = definition;
    }

    public TaskDefinition Definition { get; }

    public TaskRunState State { get; internal set; } = TaskRunState.Pending;

    public int Progress { get; internal set; }

    internal HashSet<string> CountedObjectIds { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class InteractionReport
{
    public string Operation { get; init; } = string.Empty;

    public string ObjectId { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public string ObjectColor { get; init; } = string.Empty;

    public HashSet<string> ObjectTags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public long Timestamp { get; init; }
}

public sealed class FlexibleInt32JsonConverter : JsonConverter<int>
{
    public override int Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out int integerValue))
            {
                return integerValue;
            }

            double numberValue = reader.GetDouble();
            if (double.IsFinite(numberValue)
                && Math.Truncate(numberValue) == numberValue
                && numberValue >= int.MinValue
                && numberValue <= int.MaxValue)
            {
                return (int)numberValue;
            }
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? text = reader.GetString();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integerValue))
            {
                return integerValue;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double numberValue)
                && double.IsFinite(numberValue)
                && Math.Truncate(numberValue) == numberValue
                && numberValue >= int.MinValue
                && numberValue <= int.MaxValue)
            {
                return (int)numberValue;
            }
        }

        throw new JsonException("Expected an integer or an integer-valued number.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        int value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
