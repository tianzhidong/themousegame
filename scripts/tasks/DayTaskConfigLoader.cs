#nullable enable annotations

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FileAccess = Godot.FileAccess;

namespace NewGameProject.Tasks;

public sealed class DayTaskConfigLoader
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "move",
        "click",
        "drag",
        "double_click",
        "wheel"
    };

    private static readonly HashSet<string> SupportedProgressStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        "petals",
        "dots",
        "bar"
    };

    private static readonly HashSet<string> SupportedProgressModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "per_action",
        "distinct_object"
    };

    private static readonly HashSet<string> SupportedTaskModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "sequence",
        "parallel"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string GetDayConfigPath(int dayId)
    {
        return $"res://data/days/day_{dayId:00}.json";
    }

    public bool TryLoad(int expectedDayId, out DayTaskConfig config, out IReadOnlyList<string> errors)
    {
        string path = GetDayConfigPath(expectedDayId);
        List<string> validationErrors = [];
        config = new DayTaskConfig { DayId = expectedDayId };

        if (!FileAccess.FileExists(path))
        {
            validationErrors.Add($"{path}: file does not exist.");
            errors = validationErrors;
            return false;
        }

        string jsonText;
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read))
        {
            if (file == null)
            {
                validationErrors.Add($"{path}: failed to open file. Godot error: {FileAccess.GetOpenError()}.");
                errors = validationErrors;
                return false;
            }

            jsonText = file.GetAsText();
        }

        DayTaskConfig? parsedConfig;
        try
        {
            parsedConfig = JsonSerializer.Deserialize<DayTaskConfig>(jsonText, SerializerOptions);
        }
        catch (JsonException exception)
        {
            validationErrors.Add($"{path}: JSON parse error at line {exception.LineNumber}, byte {exception.BytePositionInLine}: {exception.Message}");
            errors = validationErrors;
            return false;
        }

        if (parsedConfig == null)
        {
            validationErrors.Add($"{path}: JSON root is empty.");
            errors = validationErrors;
            return false;
        }

        Normalize(parsedConfig);
        Validate(path, expectedDayId, parsedConfig, validationErrors);
        if (validationErrors.Count > 0)
        {
            errors = validationErrors;
            return false;
        }

        parsedConfig.Tasks.Sort((left, right) => left.Order.CompareTo(right.Order));
        config = parsedConfig;
        errors = Array.Empty<string>();
        return true;
    }

    private static void Normalize(DayTaskConfig config)
    {
        config.DayTitle ??= string.Empty;
        config.TaskMode ??= string.Empty;
        config.Tasks ??= [];

        foreach (TaskDefinition task in config.Tasks)
        {
            task.Id ??= string.Empty;
            task.Text ??= string.Empty;
            task.IconPath ??= string.Empty;
            task.Operation ??= string.Empty;
            task.TargetType ??= string.Empty;
            task.TargetColor ??= string.Empty;
            task.TargetTag ??= string.Empty;
            task.TargetIds ??= [];
            task.ProgressStyle ??= string.Empty;
            task.ProgressMode ??= string.Empty;
            task.HintImagePath ??= string.Empty;
            task.HintAudioPath ??= string.Empty;
            task.Steps ??= [];
            foreach (TaskDefinition step in task.Steps)
            {
                step.Text ??= string.Empty;
                step.IconPath ??= string.Empty;
                step.Operation ??= string.Empty;
                step.TargetType ??= string.Empty;
                step.TargetColor ??= string.Empty;
                step.TargetTag ??= string.Empty;
                step.TargetIds ??= [];
                step.Steps ??= [];
                step.UnorderedGroup = step.UnorderedGroup?.Trim() ?? string.Empty;
            }
        }
    }

    private static void Validate(
        string path,
        int expectedDayId,
        DayTaskConfig config,
        ICollection<string> errors)
    {
        if (config.DayId != expectedDayId)
        {
            errors.Add($"{path}: field 'day_id' is {config.DayId}, expected {expectedDayId}.");
        }

        if (!SupportedTaskModes.Contains(config.TaskMode))
        {
            errors.Add($"{path}: field 'task_mode' must be 'sequence' or 'parallel'.");
        }

        if (config.TimeLimitSeconds <= 0)
        {
            errors.Add($"{path}: field 'time_limit_seconds' must be greater than 0.");
        }

        if (config.Tasks.Count == 0)
        {
            errors.Add($"{path}: field 'tasks' must contain at least one executable task.");
            return;
        }

        HashSet<string> taskIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int> taskOrders = [];
        for (int index = 0; index < config.Tasks.Count; index++)
        {
            TaskDefinition task = config.Tasks[index];
            string fieldPrefix = $"tasks[{index}]";

            if (string.IsNullOrWhiteSpace(task.Id))
            {
                errors.Add($"{path}: field '{fieldPrefix}.id' must not be empty.");
            }
            else if (!taskIds.Add(task.Id))
            {
                errors.Add($"{path}: duplicate task id '{task.Id}' in field '{fieldPrefix}.id'.");
            }

            if (string.IsNullOrWhiteSpace(task.Text))
            {
                errors.Add($"{path}: field '{fieldPrefix}.text' must not be empty.");
            }

            if (task.TargetCount <= 0)
            {
                errors.Add($"{path}: field '{fieldPrefix}.target_count' must be greater than 0.");
            }

            if (!SupportedOperations.Contains(task.Operation))
            {
                errors.Add($"{path}: field '{fieldPrefix}.operation' has unsupported value '{task.Operation}'.");
            }

            if (!SupportedProgressStyles.Contains(task.ProgressStyle))
            {
                errors.Add($"{path}: field '{fieldPrefix}.progress_style' has unsupported value '{task.ProgressStyle}'.");
            }

            if (!SupportedProgressModes.Contains(task.ProgressMode))
            {
                errors.Add($"{path}: field '{fieldPrefix}.progress_mode' has unsupported value '{task.ProgressMode}'.");
            }

            if (task.Order <= 0)
            {
                errors.Add($"{path}: field '{fieldPrefix}.order' must be greater than 0.");
            }
            else if (!taskOrders.Add(task.Order))
            {
                errors.Add($"{path}: duplicate task order '{task.Order}' in field '{fieldPrefix}.order'.");
            }

            if (string.IsNullOrWhiteSpace(task.IconPath))
            {
                errors.Add($"{path}: field '{fieldPrefix}.icon_path' must not be empty.");
            }
            else
            {
                ValidateResourcePath(path, $"{fieldPrefix}.icon_path", task.IconPath, errors);
            }

            HashSet<string> targetIds = new(StringComparer.OrdinalIgnoreCase);
            for (int targetIdIndex = 0; targetIdIndex < task.TargetIds.Count; targetIdIndex++)
            {
                string targetId = task.TargetIds[targetIdIndex]?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(targetId))
                {
                    errors.Add($"{path}: field '{fieldPrefix}.target_ids[{targetIdIndex}]' must not be empty.");
                }
                else if (!targetIds.Add(targetId))
                {
                    errors.Add($"{path}: duplicate object id '{targetId}' in field '{fieldPrefix}.target_ids'.");
                }
            }

            ValidateResourcePath(path, $"{fieldPrefix}.hint_image_path", task.HintImagePath, errors);
            ValidateResourcePath(path, $"{fieldPrefix}.hint_audio_path", task.HintAudioPath, errors);
            ValidateSteps(path, fieldPrefix, config.TaskMode, task, errors);
        }

        int[] actualOrders = taskOrders.OrderBy(order => order).ToArray();
        for (int expectedOrder = 1; expectedOrder <= config.Tasks.Count; expectedOrder++)
        {
            if (expectedOrder > actualOrders.Length || actualOrders[expectedOrder - 1] != expectedOrder)
            {
                errors.Add($"{path}: task order sequence must contain every value from 1 to {config.Tasks.Count}; missing '{expectedOrder}'.");
                break;
            }
        }
    }

    private static void ValidateSteps(string path, string prefix, string mode,
        TaskDefinition task, ICollection<string> errors)
    {
        if (task.Steps.Count == 0) return;
        if (mode != "sequence" || task.ProgressMode != "per_action")
            errors.Add($"{path}: '{prefix}.steps' requires sequence mode and per_action progress.");
        if (task.Steps.Count != task.TargetCount)
            errors.Add($"{path}: '{prefix}.steps' count must equal target_count.");
        HashSet<string> seenGroups = new(StringComparer.Ordinal);
        for (int index = 0; index < task.Steps.Count; index++)
        {
            TaskDefinition step = task.Steps[index];
            string field = $"{prefix}.steps[{index}]";
            if (!string.IsNullOrEmpty(step.UnorderedGroup)
                && (index == 0 || task.Steps[index - 1].UnorderedGroup != step.UnorderedGroup)
                && !seenGroups.Add(step.UnorderedGroup))
                errors.Add($"{path}: '{field}.unordered_group' must use consecutive steps.");
            if (step.Steps.Count != 0 || step.TargetCount != 1)
                errors.Add($"{path}: '{field}' must be one action without nested steps.");
            if (string.IsNullOrWhiteSpace(step.Text) || string.IsNullOrWhiteSpace(step.IconPath)
                || !SupportedOperations.Contains(step.Operation))
                errors.Add($"{path}: '{field}' needs text, icon_path and a supported operation.");
            if (step.TargetIds.Any(string.IsNullOrWhiteSpace)
                || step.TargetIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != step.TargetIds.Count)
                errors.Add($"{path}: '{field}.target_ids' contains empty or duplicate IDs.");
            ValidateResourcePath(path, $"{field}.icon_path", step.IconPath, errors);
            ValidateResourcePath(path, $"{field}.hint_image_path", step.HintImagePath, errors);
            ValidateResourcePath(path, $"{field}.hint_audio_path", step.HintAudioPath, errors);
        }
    }

    private static void ValidateResourcePath(
        string configPath,
        string fieldName,
        string resourcePath,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return;
        }

        if (!resourcePath.StartsWith("res://", StringComparison.Ordinal)
            || !ResourceLoader.Exists(resourcePath))
        {
            errors.Add($"{configPath}: field '{fieldName}' references missing resource '{resourcePath}'.");
        }
    }
}
