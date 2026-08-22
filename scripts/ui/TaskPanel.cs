#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.UI;

public partial class TaskPanel : Node
{
    private const string EmptyProgressTexturePath = "res://assets/images/ui/circle_s0.png";
    private const string FilledProgressTexturePath = "res://assets/images/ui/circle_s1.png";
    private const int ExistingProgressSlotCount = 5;

    private readonly List<TaskRowBinding> _rows = [];
    private readonly HashSet<string> _capacityWarnings = new(StringComparer.OrdinalIgnoreCase);

    private TaskManager _taskManager = null!;
    private Label _activeTaskLabel = null!;
    private TextureRect _activeTaskIcon = null!;
    private TextureRect _activeTargetCount = null!;
    private Texture2D _emptyProgressTexture = null!;
    private Texture2D _filledProgressTexture = null!;

    public override void _Ready()
    {
        Node taskbarRoot = GetParent();
        _taskManager = GetNodeOrNull<TaskManager>("/root/TaskManager");
        _activeTaskLabel = taskbarRoot.GetNodeOrNull<Label>("CanvasLayer/Taskbar/Blank4/HBoxContainer/Label");
        _activeTaskIcon = taskbarRoot.GetNodeOrNull<TextureRect>("CanvasLayer/Taskbar/Blank4/HBoxContainer/TextureRect2");
        _activeTargetCount = taskbarRoot.GetNodeOrNull<TextureRect>("CanvasLayer/Taskbar/TextureRect");
        VBoxContainer? taskList = taskbarRoot.GetNodeOrNull<VBoxContainer>("CanvasLayer/Taskbar/VBoxContainer");

        _emptyProgressTexture = GD.Load<Texture2D>(EmptyProgressTexturePath);
        _filledProgressTexture = GD.Load<Texture2D>(FilledProgressTexturePath);

        if (taskList != null)
        {
            BuildExistingRowBindings(taskList);
        }

        if (_taskManager == null
            || _activeTaskLabel == null
            || _activeTaskIcon == null
            || _activeTargetCount == null
            || taskList == null
            || _emptyProgressTexture == null
            || _filledProgressTexture == null)
        {
            GD.PushError("TaskPanel: required existing taskbar nodes or progress textures are missing. The panel will stay empty.");
            ClearUi();
            return;
        }

        if (_rows.Count == 0)
        {
            GD.PushError("TaskPanel: no existing task rows were found under CanvasLayer/Taskbar/VBoxContainer.");
            ClearUi();
            return;
        }

        _taskManager.OnTaskListLoaded += HandleTaskListLoaded;
        _taskManager.OnTaskActivated += HandleTaskActivated;
        _taskManager.OnTaskProgressChanged += HandleTaskProgressChanged;
        _taskManager.OnTaskCompleted += HandleTaskCompleted;
        _taskManager.OnAllTasksCompleted += HandleAllTasksCompleted;
        _taskManager.OnDayReset += HandleDayReset;
        _taskManager.OnConfigurationError += HandleConfigurationError;

        _taskManager.EnsureSelectedDayLoaded();
        RefreshAll();
    }

    public override void _ExitTree()
    {
        if (!IsInstanceValid(_taskManager))
        {
            return;
        }

        _taskManager.OnTaskListLoaded -= HandleTaskListLoaded;
        _taskManager.OnTaskActivated -= HandleTaskActivated;
        _taskManager.OnTaskProgressChanged -= HandleTaskProgressChanged;
        _taskManager.OnTaskCompleted -= HandleTaskCompleted;
        _taskManager.OnAllTasksCompleted -= HandleAllTasksCompleted;
        _taskManager.OnDayReset -= HandleDayReset;
        _taskManager.OnConfigurationError -= HandleConfigurationError;
    }

    private void BuildExistingRowBindings(VBoxContainer taskList)
    {
        _rows.Clear();
        foreach (Node child in taskList.GetChildren())
        {
            if (child is not TextureRect rowRoot)
            {
                continue;
            }

            HBoxContainer? rowContent = rowRoot.GetChildren().OfType<HBoxContainer>().FirstOrDefault();
            if (rowContent == null)
            {
                continue;
            }

            TextureRect? icon = rowContent.GetChildren().OfType<TextureRect>().FirstOrDefault();
            HBoxContainer? progressContainer = rowContent.GetChildren().OfType<HBoxContainer>().FirstOrDefault();
            if (icon == null || progressContainer == null)
            {
                continue;
            }

            List<TextureRect> progressSlots = progressContainer
                .GetChildren()
                .OfType<TextureRect>()
                .Take(ExistingProgressSlotCount)
                .ToList();

            if (progressSlots.Count == 0)
            {
                continue;
            }

            _rows.Add(new TaskRowBinding(rowRoot, icon, progressSlots));
        }
    }

    private void RefreshAll()
    {
        if (_taskManager == null || _rows.Count == 0)
        {
            return;
        }

        IReadOnlyList<TaskRuntimeState> tasks = _taskManager.Tasks;
        if (tasks.Count > _rows.Count)
        {
            GD.PushError($"TaskPanel: day {_taskManager.CurrentDayId} contains {tasks.Count} tasks, but the existing UI has only {_rows.Count} rows. The UI was cleared to avoid partial content.");
            ClearUi();
            return;
        }

        for (int index = 0; index < _rows.Count; index++)
        {
            if (index >= tasks.Count)
            {
                ClearRow(_rows[index]);
                continue;
            }

            BindRow(_rows[index], tasks[index]);
        }

        TaskRuntimeState? activeTask = _taskManager.GetActiveTask();
        if (activeTask != null)
        {
            _activeTaskLabel.Text = activeTask.Definition.Text;
            _activeTaskIcon.Texture = LoadTexture(activeTask.Definition.IconPath);
            BindProgressCount(activeTask.Progress);
            return;
        }

        if (tasks.Count > 0 && tasks.All(task => task.State == TaskRunState.Completed))
        {
            _activeTaskLabel.Text = "任务完成";
            _activeTaskIcon.Texture = LoadTexture(tasks[^1].Definition.IconPath);
            BindProgressCount(tasks[^1].Progress);
            return;
        }

        _activeTaskLabel.Text = string.Empty;
        _activeTaskIcon.Texture = null;
        _activeTargetCount.Texture = null;
    }

    private void BindRow(TaskRowBinding row, TaskRuntimeState task)
    {
        row.Root.SetMeta("task_panel_visible", true);
        row.Root.Visible = true;
        row.Icon.Visible = true;
        row.Icon.Texture = LoadTexture(task.Definition.IconPath);

        int visibleSlotCount = Math.Min(task.Definition.TargetCount, row.ProgressSlots.Count);
        if (task.Definition.TargetCount > row.ProgressSlots.Count
            && _capacityWarnings.Add(task.Definition.Id))
        {
            GD.PushWarning($"TaskPanel: task '{task.Definition.Id}' target_count is {task.Definition.TargetCount}, but the existing row has only {row.ProgressSlots.Count} progress slots.");
        }

        int displayedProgress = task.State == TaskRunState.Completed
            ? task.Definition.TargetCount
            : task.Progress;

        for (int index = 0; index < row.ProgressSlots.Count; index++)
        {
            TextureRect slot = row.ProgressSlots[index];
            slot.Visible = index < visibleSlotCount;
            if (!slot.Visible)
            {
                continue;
            }

            slot.Texture = index < displayedProgress
                ? _filledProgressTexture
                : _emptyProgressTexture;
        }
    }

    private void BindProgressCount(int progress)
    {
        int supportedCount = Math.Clamp(progress, 0, ExistingProgressSlotCount);
        string path = $"res://assets/images/ui/num{supportedCount}.png";
        _activeTargetCount.Texture = LoadTexture(path);
    }

    private void ClearUi()
    {
        if (_activeTaskLabel != null)
        {
            _activeTaskLabel.Text = string.Empty;
        }

        if (_activeTaskIcon != null)
        {
            _activeTaskIcon.Texture = null;
        }

        if (_activeTargetCount != null)
        {
            _activeTargetCount.Texture = null;
        }

        foreach (TaskRowBinding row in _rows)
        {
            ClearRow(row);
        }
    }

    private static void ClearRow(TaskRowBinding row)
    {
        row.Root.SetMeta("task_panel_visible", false);
        row.Root.Visible = false;
        row.Icon.Texture = null;
        foreach (TextureRect slot in row.ProgressSlots)
        {
            slot.Visible = false;
        }
    }

    private static Texture2D? LoadTexture(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : GD.Load<Texture2D>(path);
    }

    private void HandleTaskListLoaded(int _dayId) => RefreshAll();

    private void HandleTaskActivated(string _taskId) => RefreshAll();

    private void HandleTaskProgressChanged(string _taskId, int _progress, int _targetCount) => RefreshAll();

    private void HandleTaskCompleted(string _taskId) => RefreshAll();

    private void HandleAllTasksCompleted(int _dayId, double _elapsedSeconds) => RefreshAll();

    private void HandleDayReset(int _dayId) => RefreshAll();

    private void HandleConfigurationError(string _path, string _message) => ClearUi();

    private sealed class TaskRowBinding(
        TextureRect root,
        TextureRect icon,
        List<TextureRect> progressSlots)
    {
        public TextureRect Root { get; } = root;

        public TextureRect Icon { get; } = icon;

        public List<TextureRect> ProgressSlots { get; } = progressSlots;
    }
}
