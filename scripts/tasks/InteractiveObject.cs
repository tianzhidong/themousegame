#nullable enable annotations

using Godot;
using System;

namespace NewGameProject.Tasks;

[GlobalClass]
public partial class InteractiveObject : Area2D
{
    [Signal]
    public delegate void InteractionAcceptedEventHandler(string operation, string objectId);

    [Export]
    public string ObjectId { get; set; } = string.Empty;

    [Export]
    public string ObjectType { get; set; } = string.Empty;

    [Export]
    public string ObjectColor { get; set; } = string.Empty;

    [Export]
    public string[] ObjectTags { get; set; } = [];

    [Export(PropertyHint.Enum, "move,click,drag,double_click,wheel")]
    public string Operation { get; set; } = "click";

    [Export]
    public bool CanReceiveMouseInput { get; set; }

    private TaskManager? _taskManager;
    private AudioManager? _audioManager;
    private bool _dragReported;

    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(ObjectId))
        {
            ObjectId = Name.ToString().ToLowerInvariant();
        }

        _taskManager = GetNodeOrNull<TaskManager>("/root/TaskManager");
        _audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        if (_taskManager == null)
        {
            GD.PushError($"InteractiveObject '{GetPath()}': required TaskManager autoload was not found.");
            SetInputAvailability(false);
            return;
        }

        _taskManager.OnTaskListLoaded += HandleTaskStateChanged;
        _taskManager.OnTaskActivated += HandleTaskActivated;
        _taskManager.OnTaskCompleted += HandleTaskCompleted;
        _taskManager.OnDayReset += HandleTaskStateChanged;
        MouseEntered += HandleMouseEntered;

        _taskManager.EnsureSelectedDayLoaded();
        RefreshInputAvailability();
    }

    public override void _ExitTree()
    {
        if (_taskManager != null && GodotObject.IsInstanceValid(_taskManager))
        {
            _taskManager.OnTaskListLoaded -= HandleTaskStateChanged;
            _taskManager.OnTaskActivated -= HandleTaskActivated;
            _taskManager.OnTaskCompleted -= HandleTaskCompleted;
            _taskManager.OnDayReset -= HandleTaskStateChanged;
        }

        MouseEntered -= HandleMouseEntered;
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (!CanReceiveMouseInput || _taskManager == null)
        {
            return;
        }

        if (@event is InputEventMouseButton buttonEvent)
        {
            HandleMouseButton(viewport, buttonEvent);
            return;
        }

        if (@event is InputEventMouseMotion motionEvent
            && IsOperation("drag")
            && !_dragReported
            && (motionEvent.ButtonMask & MouseButtonMask.Left) != 0)
        {
            _dragReported = SubmitInteraction("drag");
            if (_dragReported)
            {
                viewport.SetInputAsHandled();
            }
        }
    }

    public void RefreshInputAvailability()
    {
        if (_taskManager == null)
        {
            SetInputAvailability(false);
            return;
        }

        SetInputAvailability(_taskManager.CanInteract(BuildReport(Operation)));
    }

    private void HandleMouseButton(Viewport viewport, InputEventMouseButton mouseEvent)
    {
        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (!mouseEvent.Pressed)
            {
                _dragReported = false;
                return;
            }

            bool accepted = false;
            if (IsOperation("double_click") && mouseEvent.DoubleClick)
            {
                accepted = SubmitInteraction("double_click");
            }
            else if (IsOperation("click") && !mouseEvent.DoubleClick)
            {
                accepted = SubmitInteraction("click");
            }

            if (accepted)
            {
                _audioManager?.PlayUiClick();
                viewport.SetInputAsHandled();
            }
            return;
        }

        if (mouseEvent.Pressed
            && IsOperation("wheel")
            && mouseEvent.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            if (SubmitInteraction("wheel"))
            {
                viewport.SetInputAsHandled();
            }
        }
    }

    private void HandleMouseEntered()
    {
        if (CanReceiveMouseInput && IsOperation("move"))
        {
            SubmitInteraction("move");
        }
    }

    private bool SubmitInteraction(string operation)
    {
        if (_taskManager == null || !_taskManager.TryHandleInteraction(BuildReport(operation)))
        {
            return false;
        }

        EmitSignal(SignalName.InteractionAccepted, operation, ObjectId);
        RefreshInputAvailability();
        return true;
    }

    private Godot.Collections.Dictionary BuildReport(string operation)
    {
        Godot.Collections.Array<string> tags = [];
        foreach (string tag in ObjectTags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag.Trim());
            }
        }

        return new Godot.Collections.Dictionary
        {
            ["operation"] = operation,
            ["object_id"] = ObjectId,
            ["object_type"] = ObjectType,
            ["object_color"] = ObjectColor,
            ["object_tags"] = tags,
            ["timestamp"] = (long)Time.GetTicksMsec()
        };
    }

    private void SetInputAvailability(bool canReceiveInput)
    {
        CanReceiveMouseInput = canReceiveInput;
        InputPickable = canReceiveInput;
    }

    private bool IsOperation(string operation)
    {
        return string.Equals(Operation, operation, StringComparison.OrdinalIgnoreCase);
    }

    private void HandleTaskStateChanged(int _dayId) => RefreshInputAvailability();

    private void HandleTaskActivated(string _taskId) => RefreshInputAvailability();

    private void HandleTaskCompleted(string _taskId) => RefreshInputAvailability();
}
