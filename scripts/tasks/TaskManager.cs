#nullable enable annotations

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.Tasks;

public partial class TaskManager : Node
{
    [Signal]
    public delegate void OnTaskListLoadedEventHandler(int dayId);

    [Signal]
    public delegate void OnTaskActivatedEventHandler(string taskId);

    [Signal]
    public delegate void OnTaskProgressChangedEventHandler(string taskId, int progress, int targetCount);

    [Signal]
    public delegate void OnTaskCompletedEventHandler(string taskId);

    [Signal]
    public delegate void OnAllTasksCompletedEventHandler(int dayId, double elapsedSeconds);

    [Signal]
    public delegate void OnDayResetEventHandler(int dayId);

    [Signal]
    public delegate void OnConfigurationErrorEventHandler(string configPath, string message);

    private readonly DayTaskConfigLoader _loader = new();
    private readonly InteractionGate _interactionGate = new();
    private readonly List<TaskRuntimeState> _tasks = [];
    private readonly HashSet<string> _parallelCountedObjectIds = new(StringComparer.OrdinalIgnoreCase);

    private DayTaskConfig? _config;
    private ulong _dayStartedAtMsec;
    private bool _allTasksCompleted;

    public int CurrentDayId { get; private set; }

    public string DayTitle => _config?.DayTitle ?? string.Empty;

    public string TaskMode => _config?.TaskMode ?? string.Empty;

    public int TimeLimitSeconds => _config?.TimeLimitSeconds ?? 0;

    public IReadOnlyList<TaskRuntimeState> Tasks => _tasks;

    public double LastCompletionSeconds { get; private set; }

    public bool LoadSelectedDay()
    {
        return LoadDay(ReadSelectedDay());
    }

    public bool EnsureSelectedDayLoaded()
    {
        int selectedDay = ReadSelectedDay();
        if (_config != null && CurrentDayId == selectedDay)
        {
            return true;
        }

        return LoadDay(selectedDay);
    }

    public bool LoadDay(int dayId)
    {
        int safeDayId = Math.Clamp(dayId, 1, 15);
        if (!_loader.TryLoad(safeDayId, out DayTaskConfig config, out IReadOnlyList<string> errors))
        {
            EnterSafeEmptyState(safeDayId, errors);
            return false;
        }

        CurrentDayId = safeDayId;
        _config = config;
        RebuildRuntimeState();
        return true;
    }

    public void ResetDay()
    {
        if (_config == null)
        {
            EnsureSelectedDayLoaded();
            return;
        }

        RebuildRuntimeState();
        EmitSignal(SignalName.OnDayReset, CurrentDayId);
    }

    public bool CanInteract(Godot.Collections.Dictionary interactionData)
    {
        if (!TryCreateInteractionReport(interactionData, out InteractionReport report))
        {
            return false;
        }

        TaskRuntimeState? task = FindFirstMatchingActiveTask(report);
        return task != null && !WasDistinctObjectAlreadyCounted(task, report.ObjectId);
    }

    public bool TryHandleInteraction(Godot.Collections.Dictionary interactionData)
    {
        if (_config == null || _allTasksCompleted)
        {
            return false;
        }

        if (!TryCreateInteractionReport(interactionData, out InteractionReport report))
        {
            return false;
        }

        TaskRuntimeState? task = FindFirstMatchingActiveTask(report);
        if (task == null)
        {
            return false;
        }

        if (WasDistinctObjectAlreadyCounted(task, report.ObjectId))
        {
            // The highest-priority matching task claims the event even when this object was counted before.
            return false;
        }

        if (string.Equals(task.Definition.ProgressMode, "distinct_object", StringComparison.OrdinalIgnoreCase))
        {
            task.CountedObjectIds.Add(report.ObjectId);
            if (IsParallelMode())
            {
                _parallelCountedObjectIds.Add(report.ObjectId);
            }
        }

        int previousProgress = task.Progress;
        task.Progress = Math.Min(task.Progress + 1, task.Definition.TargetCount);
        if (task.Progress == previousProgress)
        {
            return false;
        }

        EmitSignal(
            SignalName.OnTaskProgressChanged,
            task.Definition.Id,
            task.Progress,
            task.Definition.TargetCount);

        if (task.Progress >= task.Definition.TargetCount)
        {
            CompleteTask(task);
        }

        return true;
    }

    public TaskRuntimeState? GetActiveTask()
    {
        return _tasks.FirstOrDefault(task => task.State == TaskRunState.Active);
    }

    public string GetActiveTaskId()
    {
        return GetActiveTask()?.Definition.Id ?? string.Empty;
    }

    public int GetTaskProgress(string taskId)
    {
        return FindTaskById(taskId)?.Progress ?? 0;
    }

    public int GetTaskTargetCount(string taskId)
    {
        return FindTaskById(taskId)?.Definition.TargetCount ?? 0;
    }

    public string GetTaskText(string taskId)
    {
        return FindTaskById(taskId)?.Definition.Text ?? string.Empty;
    }

    public string GetTaskTargetColor(string taskId)
    {
        return FindTaskById(taskId)?.Definition.TargetColor ?? string.Empty;
    }

    public string GetTaskState(string taskId)
    {
        return FindTaskById(taskId)?.State.ToString() ?? string.Empty;
    }

    public bool IsDayComplete()
    {
        return _allTasksCompleted;
    }

    public double GetLastCompletionSeconds()
    {
        return LastCompletionSeconds;
    }

    private void RebuildRuntimeState()
    {
        _tasks.Clear();
        _parallelCountedObjectIds.Clear();
        _allTasksCompleted = false;
        LastCompletionSeconds = 0.0;
        _dayStartedAtMsec = Time.GetTicksMsec();

        if (_config == null)
        {
            EmitSignal(SignalName.OnTaskListLoaded, CurrentDayId);
            return;
        }

        foreach (TaskDefinition definition in _config.Tasks.OrderBy(task => task.Order))
        {
            _tasks.Add(new TaskRuntimeState(definition));
        }

        if (IsParallelMode())
        {
            foreach (TaskRuntimeState task in _tasks)
            {
                task.State = TaskRunState.Active;
            }
        }
        else if (_tasks.Count > 0)
        {
            _tasks[0].State = TaskRunState.Active;
        }

        EmitSignal(SignalName.OnTaskListLoaded, CurrentDayId);
        foreach (TaskRuntimeState task in _tasks.Where(task => task.State == TaskRunState.Active))
        {
            EmitSignal(SignalName.OnTaskActivated, task.Definition.Id);
        }
    }

    private void CompleteTask(TaskRuntimeState task)
    {
        if (task.State != TaskRunState.Active)
        {
            return;
        }

        task.State = TaskRunState.Completed;
        task.Progress = task.Definition.TargetCount;
        EmitSignal(SignalName.OnTaskCompleted, task.Definition.Id);

        if (!IsParallelMode())
        {
            TaskRuntimeState? nextTask = _tasks.FirstOrDefault(candidate => candidate.State == TaskRunState.Pending);
            if (nextTask != null)
            {
                nextTask.State = TaskRunState.Active;
                EmitSignal(SignalName.OnTaskActivated, nextTask.Definition.Id);
            }
        }

        if (_tasks.Count > 0 && _tasks.All(candidate => candidate.State == TaskRunState.Completed))
        {
            CompleteDay();
        }
    }

    private void CompleteDay()
    {
        if (_allTasksCompleted)
        {
            return;
        }

        _allTasksCompleted = true;
        LastCompletionSeconds = (Time.GetTicksMsec() - _dayStartedAtMsec) / 1000.0;
        EmitSignal(SignalName.OnAllTasksCompleted, CurrentDayId, LastCompletionSeconds);
    }

    private TaskRuntimeState? FindFirstMatchingActiveTask(InteractionReport report)
    {
        foreach (TaskRuntimeState task in _tasks)
        {
            if (task.State == TaskRunState.Active
                && _interactionGate.Matches(task.Definition, report))
            {
                return task;
            }
        }

        return null;
    }

    private TaskRuntimeState? FindTaskById(string taskId)
    {
        return _tasks.FirstOrDefault(task => string.Equals(
            task.Definition.Id,
            taskId,
            StringComparison.OrdinalIgnoreCase));
    }

    private bool WasDistinctObjectAlreadyCounted(TaskRuntimeState task, string objectId)
    {
        if (!string.Equals(task.Definition.ProgressMode, "distinct_object", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return task.CountedObjectIds.Contains(objectId)
            || (IsParallelMode() && _parallelCountedObjectIds.Contains(objectId));
    }

    private bool IsParallelMode()
    {
        return string.Equals(TaskMode, "parallel", StringComparison.OrdinalIgnoreCase);
    }

    private void EnterSafeEmptyState(int dayId, IReadOnlyList<string> errors)
    {
        CurrentDayId = dayId;
        _config = null;
        _tasks.Clear();
        _parallelCountedObjectIds.Clear();
        _allTasksCompleted = false;
        LastCompletionSeconds = 0.0;
        _dayStartedAtMsec = 0;

        string path = DayTaskConfigLoader.GetDayConfigPath(dayId);
        foreach (string error in errors)
        {
            GD.PushError(error);
            EmitSignal(SignalName.OnConfigurationError, path, error);
        }

        EmitSignal(SignalName.OnTaskListLoaded, CurrentDayId);
    }

    private int ReadSelectedDay()
    {
        Node? gameState = GetNodeOrNull<Node>("/root/GameState");
        if (gameState == null)
        {
            GD.PushError("TaskManager: required autoload '/root/GameState' was not found. Falling back to day 1.");
            return 1;
        }

        Variant selectedDay = gameState.Get("selected_day");
        if (selectedDay.VariantType != Variant.Type.Int)
        {
            GD.PushError("TaskManager: GameState.selected_day is missing or is not an integer. Falling back to day 1.");
            return 1;
        }

        return Math.Clamp(selectedDay.AsInt32(), 1, 15);
    }

    private static bool TryCreateInteractionReport(
        Godot.Collections.Dictionary data,
        out InteractionReport report)
    {
        string operation = ReadString(data, "operation");
        string objectId = ReadString(data, "object_id");
        string objectType = ReadString(data, "object_type");
        string objectColor = ReadString(data, "object_color");

        if (string.IsNullOrWhiteSpace(operation)
            || string.IsNullOrWhiteSpace(objectId)
            || string.IsNullOrWhiteSpace(objectType))
        {
            report = new InteractionReport();
            return false;
        }

        report = new InteractionReport
        {
            Operation = operation,
            ObjectId = objectId,
            ObjectType = objectType,
            ObjectColor = objectColor,
            ObjectTags = ReadTags(data),
            Timestamp = ReadInt64(data, "timestamp")
        };
        return true;
    }

    private static string ReadString(Godot.Collections.Dictionary data, string key)
    {
        if (!data.ContainsKey(key))
        {
            return string.Empty;
        }

        Variant value = data[key];
        return value.VariantType == Variant.Type.Nil ? string.Empty : value.AsString().Trim();
    }

    private static long ReadInt64(Godot.Collections.Dictionary data, string key)
    {
        if (!data.ContainsKey(key))
        {
            return 0;
        }

        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : 0;
    }

    private static HashSet<string> ReadTags(Godot.Collections.Dictionary data)
    {
        HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);
        if (!data.ContainsKey("object_tags"))
        {
            return tags;
        }

        Variant value = data["object_tags"];
        if (value.VariantType == Variant.Type.PackedStringArray)
        {
            foreach (string tag in value.AsStringArray())
            {
                AddTag(tags, tag);
            }
        }
        else if (value.VariantType == Variant.Type.Array)
        {
            foreach (Variant item in value.AsGodotArray())
            {
                AddTag(tags, item.AsString());
            }
        }
        else if (value.VariantType == Variant.Type.String)
        {
            foreach (string tag in value.AsString().Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                AddTag(tags, tag);
            }
        }

        return tags;
    }

    private static void AddTag(ISet<string> tags, string tag)
    {
        string normalized = tag.Trim();
        if (!string.IsNullOrEmpty(normalized))
        {
            tags.Add(normalized);
        }
    }
}
