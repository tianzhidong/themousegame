#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.UI;

public partial class TaskPanel : Node
{
    // A level may keep its final reward interactive after the eight task rows finish.
    [Export] public bool AutoShowResultOnCompletion { get; set; } = true;
    private const string EmptyProgressTexturePath = "res://assets/images/ui/circle_s0.png";
    private const string FilledProgressTexturePath = "res://assets/images/ui/circle_s1.png";
    private const string ResultScenePath = "res://scenes/ui/result.tscn";
    private const string ResultOverlayLayerName = "ResultOverlayLayer";
    private const double ResultDelaySeconds = 2.0;
    private const int ResultOverlayLayer = 50;
    private const int ExistingProgressSlotCount = 5;
    private static readonly Color ResultOverlayColor = new(0.86f, 0.52f, 0.24f, 0.34f);

    private readonly List<TaskRowBinding> _rows = [];
    private readonly HashSet<string> _capacityWarnings = new(StringComparer.OrdinalIgnoreCase);
    private readonly RandomNumberGenerator _random = new();

    private TaskManager _taskManager = null!;
    private Label _activeTaskLabel = null!;
    private TextureRect _activeTaskIcon = null!;
    private TextureRect _activeTargetCount = null!;
    private Texture2D _emptyProgressTexture = null!;
    private Texture2D _filledProgressTexture = null!;
    private bool _resultShown;
    private bool _resultPending;
    private double _completionElapsedSeconds;
    private int _completionGeneration;
    private string _focusedTaskId = string.Empty;
    private AudioManager _audio = null!;
    private TextureRect? _hintSpeaker;
    private string _hintPath = string.Empty;
    private int _hintGeneration;

    public void FocusTask(string taskId)
    {
        _focusedTaskId = taskId;
        RefreshAll();
    }

    public override void _Ready()
    {
        _random.Randomize();
        Node taskbarRoot = GetParent();
        _taskManager = GetNodeOrNull<TaskManager>("/root/TaskManager");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        ProcessMode = ProcessModeEnum.Always;
        _hintSpeaker = taskbarRoot.GetNodeOrNull<TextureRect>("CanvasLayer/Taskbar/Blank4/HBoxContainer/TextureRect");
        if (_hintSpeaker != null)
        {
            _hintSpeaker.MouseFilter = Control.MouseFilterEnum.Stop;
            _hintSpeaker.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            _hintSpeaker.TooltipText = "听操作提示";
            _hintSpeaker.GuiInput += HandleHintSpeakerInput;
        }
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
        CancelHint();
        if (IsInstanceValid(_hintSpeaker)) _hintSpeaker.GuiInput -= HandleHintSpeakerInput;
        _completionGeneration++;
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

    public override void _Process(double delta)
    {
        if (_audio != null && _audio.TaskHintsBlocked()) CancelHint();
    }

    private void HandleHintSpeakerInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        _hintSpeaker?.AcceptEvent();
        _hintGeneration++;
        _audio.PlayTaskHint(_hintPath);
    }

    private void CancelHint()
    {
        _hintGeneration++;
        if (IsInstanceValid(_audio)) _audio.StopTaskHint();
    }

    private void UpdateHint(string path, bool forceAutoPlay = false)
    {
        if (_hintPath == path && !forceAutoPlay) return;
        _hintPath = path;
        CancelHint();
        // These levels narrate when their new group or activity is actually shown.
        bool levelOwnsPlayback = _taskManager.CurrentDayId is 10 or 11 or 12;
        if (path.Length > 0 && (!levelOwnsPlayback || forceAutoPlay)) PlayHintAfterDelay();
    }

    private async void PlayHintAfterDelay()
    {
        int generation = _hintGeneration;
        await ToSignal(GetTree().CreateTimer(0.8, false), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree() || generation != _hintGeneration) return;
        _audio.PlayAutomaticTaskHint(_hintPath);
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

        TaskRuntimeState? focusedTask = tasks.FirstOrDefault(task =>
            task.Definition.Id == _focusedTaskId
            && task.State is TaskRunState.Active or TaskRunState.Completed);
        if (_taskManager.CurrentDayId == 5 && _taskManager.TaskMode == "parallel"
            && focusedTask == null && !_taskManager.IsDayComplete())
        {
            _activeTaskLabel.Text = "自选维修";
            UpdateHint(string.Empty);
            TaskRuntimeState? nextTask = _taskManager.GetActiveTask();
            _activeTaskIcon.Texture = LoadTexture(nextTask?.Definition.IconPath ?? string.Empty);
            BindProgressCount(nextTask?.Progress ?? 0);
            return;
        }

        TaskRuntimeState? activeTask = focusedTask ?? _taskManager.GetActiveTask();
        if (activeTask != null)
        {
            TaskDefinition currentAction = activeTask.CurrentInteractionDefinition;
            UpdateHint(activeTask.State == TaskRunState.Active && activeTask.Progress < activeTask.Definition.TargetCount
                ? currentAction.HintAudioPath : string.Empty);
            _activeTaskLabel.Text = currentAction.Text;
            _activeTaskIcon.Texture = LoadTexture(currentAction.IconPath);
            BindProgressCount(activeTask.Progress);
            return;
        }

        if (tasks.Count > 0 && tasks.All(task => task.State == TaskRunState.Completed))
        {
            UpdateHint(string.Empty);
            _activeTaskLabel.Text = "任务完成";
            _activeTaskIcon.Texture = LoadTexture(tasks[^1].Definition.IconPath);
            BindProgressCount(tasks[^1].Progress);
            return;
        }

        _activeTaskLabel.Text = string.Empty;
        UpdateHint(string.Empty);
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
        UpdateHint(string.Empty);
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

    private void HandleAllTasksCompleted(int _dayId, double elapsedSeconds)
    {
        RefreshAll();
        if (_resultShown || _resultPending || HasResultOverlay())
        {
            return;
        }

        _completionElapsedSeconds = elapsedSeconds;
        _resultPending = true;
        if (AutoShowResultOnCompletion) ShowResultAfterDelay();
    }

    public void SetCompletionPrompt(string text, Texture2D icon, string hintAudioPath = "")
    {
        if (!_resultPending || _resultShown) return;
        _activeTaskLabel.Text = text;
        _activeTaskIcon.Texture = icon;
        UpdateHint(hintAudioPath, true);
    }

    public void ShowCompletionResult()
    {
        if (_resultPending && !_resultShown) ShowResultAfterDelay();
    }

    private void HandleDayReset(int _dayId)
    {
        _completionGeneration++;
        _resultShown = false;
        _resultPending = false;
        _completionElapsedSeconds = 0.0;
        _hintPath = string.Empty;
        CancelHint();
        RefreshAll();
    }

    private void HandleConfigurationError(string _path, string _message) => ClearUi();

    private async void ShowResultAfterDelay()
    {
        int generation = _completionGeneration;
        await ToSignal(GetTree().CreateTimer(ResultDelaySeconds), SceneTreeTimer.SignalName.Timeout);
        if (generation != _completionGeneration) return;
        if (!IsInsideTree() || !_resultPending || _resultShown || HasResultOverlay())
        {
            _resultPending = false;
            return;
        }

        ShowResult();
    }

    private void ShowResult()
    {
        CancelHint();
        Node overlayParent = GetOverlayParent();
        if (overlayParent.GetNodeOrNull<Node>(ResultOverlayLayerName) != null)
        {
            _resultPending = false;
            _resultShown = true;
            return;
        }

        PackedScene? resultScene = GD.Load<PackedScene>(ResultScenePath);
        if (resultScene == null)
        {
            GD.PushError($"TaskPanel: failed to load {ResultScenePath}.");
            _resultPending = false;
            return;
        }

        Node2D? result = resultScene.Instantiate<Node2D>();
        if (result == null)
        {
            GD.PushError("TaskPanel: failed to instantiate result.tscn.");
            _resultPending = false;
            return;
        }

        CanvasLayer resultLayer = new()
        {
            Name = ResultOverlayLayerName,
            Layer = ResultOverlayLayer
        };
        overlayParent.AddChild(resultLayer);

        ColorRect warmOverlay = new()
        {
            Name = "WarmOverlay",
            Color = ResultOverlayColor,
            Position = Vector2.Zero,
            Size = GetViewport().GetVisibleRect().Size,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        resultLayer.AddChild(warmOverlay);

        result.ZIndex = 100;
        resultLayer.AddChild(result);
        if (result.HasMethod("set_result_data"))
        {
            result.Call(
                "set_result_data",
                (float)Math.Max(_completionElapsedSeconds, 0.0),
                _random.RandfRange(80.0f, 95.0f));
        }

        _resultPending = false;
        _resultShown = true;
    }

    private bool HasResultOverlay()
    {
        return GetOverlayParent().GetNodeOrNull<Node>(ResultOverlayLayerName) != null;
    }

    private Node GetOverlayParent()
    {
        return GetTree().CurrentScene ?? GetTree().Root;
    }

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
