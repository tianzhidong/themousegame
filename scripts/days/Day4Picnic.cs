#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using System;
using System.Collections.Generic;

namespace NewGameProject.Days;

public partial class Day4Picnic : Node2D
{
    private const string AssetRoot = "res://assets/images/objects/day4/";
    private const string AudioRoot = "res://assets/audio/voice/day4/";
    private static readonly Dictionary<string, string> PlacementSoundFiles = new()
    {
        ["plate"] = "ceramic_clink.mp3",
        ["drink"] = "drink_place.mp3",
        ["toy"] = "toy_place.mp3",
        ["flower"] = "leaf_rustle.mp3",
        ["clothes"] = "cloth_rustle.mp3"
    };
    private static readonly Dictionary<string, string[]> GroupAssets = new()
    {
        ["drag_fruits"] = ["fruit_apple", "fruit_pear", "fruit_orange"],
        ["drag_bakery_items"] = ["bakery_croissant", "bakery_cupcake", "bakery_cookie"],
        ["drag_plates"] = ["tableware_plate", "tableware_bowl", "tableware_mug"],
        ["drag_drinks"] = ["drink_milk_carton", "drink_orange_juice", "drink_berry_juice"],
        ["drag_toys"] = ["toy_beach_ball", "toy_car", "toy_airplane"],
        ["drag_flowers"] = ["flower_red", "flower_blue", "flower_yellow"],
        ["drag_vegetables"] = ["vegetable_carrot", "vegetable_tomato", "vegetable_broccoli"],
        ["drag_clothes"] = ["clothing_tshirt", "clothing_sun_hat", "clothing_jacket"]
    };

    // Picnic contents cluster around eating places; toys and clothing stay near the edges.
    // Coordinates follow the mat polygon (left-to-right and back-to-front), not a screen grid.
    private static readonly Dictionary<string, Vector2[]> GroupPositions = new()
    {
        ["drag_fruits"] = [new(0.38f, 0.34f), new(0.46f, 0.27f), new(0.49f, 0.43f), new(0.37f, 0.50f), new(0.56f, 0.32f)],
        ["drag_bakery_items"] = [new(0.58f, 0.52f), new(0.66f, 0.40f), new(0.70f, 0.54f), new(0.53f, 0.61f), new(0.63f, 0.68f)],
        ["drag_plates"] = [new(0.28f, 0.34f), new(0.32f, 0.69f), new(0.45f, 0.65f), new(0.77f, 0.43f), new(0.77f, 0.64f)],
        ["drag_drinks"] = [new(0.76f, 0.18f), new(0.85f, 0.25f), new(0.86f, 0.42f), new(0.69f, 0.16f), new(0.91f, 0.57f)],
        ["drag_toys"] = [new(0.10f, 0.78f), new(0.18f, 0.84f), new(0.27f, 0.87f), new(0.35f, 0.86f), new(0.12f, 0.92f)],
        ["drag_flowers"] = [new(0.20f, 0.12f), new(0.32f, 0.13f), new(0.44f, 0.12f), new(0.55f, 0.13f), new(0.12f, 0.28f)],
        ["drag_vegetables"] = [new(0.14f, 0.45f), new(0.22f, 0.50f), new(0.16f, 0.65f), new(0.24f, 0.68f), new(0.25f, 0.22f)],
        ["drag_clothes"] = [new(0.56f, 0.85f), new(0.66f, 0.88f), new(0.75f, 0.82f), new(0.84f, 0.87f), new(0.91f, 0.76f)]
    };

    [Export(PropertyHint.Range, "50,110,1")]
    public float ItemSize { get; set; } = 72.0f;

    [Export(PropertyHint.Range, "1,3,0.05")]
    public float ClothingSizeMultiplier { get; set; } = 1.9f;

    [Export(PropertyHint.Range, "0,0.35,0.01")]
    public float FruitSizeVariation { get; set; } = 0.22f;

    [Export(PropertyHint.Range, "0,45,1")]
    public float FruitRotationDegrees { get; set; } = 22.0f;

    [Export(PropertyHint.Range, "20,65,1")]
    public float SnapDistance { get; set; } = 48.0f;

    [Export(PropertyHint.Range, "0.15,0.6,0.01")]
    public float TargetOpacity { get; set; } = 0.32f;

    [Export(PropertyHint.Range, "0.1,0.5,0.01")]
    public float MoveDuration { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0.3,2,0.1")]
    public float GroupDelay { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "1,1.3,0.01")]
    public float BasketSpread { get; set; } = 1.16f;

    [Export(PropertyHint.Range, "0,32,1")]
    public float BasketItemInset { get; set; } = 18.0f;

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<string, List<PicnicItem>> _groups = new();
    private readonly List<CollisionPolygon2D> _baskets = [];
    private readonly List<Sprite2D> _basketFronts = [];
    private readonly List<Rect2> _targetBounds = [];
    private readonly Dictionary<string, AudioStream> _placementSounds = new();
    private AudioStreamPlayer _musicPlayer = null!;
    private TaskManager _taskManager = null!;
    private AudioManager _audio = null!;
    private CollisionPolygon2D _mat = null!;
    private PicnicItem? _dragged;
    private Vector2 _grabOffset;
    private string _activeGroupId = string.Empty;
    private string _pendingGroupId = string.Empty;
    private double _groupCountdown;
    private bool _animating;

    public override void _Ready()
    {
        _random.Randomize();
        _taskManager = GetNode<TaskManager>("/root/TaskManager");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        foreach ((string objectType, string filename) in PlacementSoundFiles)
        {
            if (!ResourceLoader.Exists(AudioRoot + filename)) continue;
            AudioStream? sound = GD.Load<AudioStream>(AudioRoot + filename);
            if (sound != null) _placementSounds[objectType] = sound;
        }
        AudioStreamMP3 music = (AudioStreamMP3)GD.Load<AudioStreamMP3>(AudioRoot + "day4_bgm.mp3").Duplicate();
        music.Loop = true;
        _musicPlayer = new AudioStreamPlayer { Name = "Day4MusicPlayer", Bus = "Music", Stream = music };
        AddChild(_musicPlayer);
        _audio.SetBackgroundMusicPaused(true);
        _musicPlayer.Play();
        _mat = GetNode<CollisionPolygon2D>("mat/CollisionPolygon2D");
        for (int index = 1; index <= 3; index++)
        {
            _baskets.Add(GetNode<CollisionPolygon2D>($"basket/basket{index}"));
            _basketFronts.Add(GetNode<Sprite2D>($"basket/basket{index}/Front"));
        }

        // Reload on entry so new scene objects and task progress always agree, including F6.
        GetNode<Node>("/root/GameState").Call("set_selected_day", 4);
        if (!_taskManager.LoadDay(4) || !CreateItems())
        {
            SetProcessUnhandledInput(false);
            return;
        }

        _taskManager.OnTaskActivated += HandleTaskActivated;
        ShowGroup(_taskManager.GetActiveTaskId());
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_musicPlayer)) _musicPlayer.Stop();
        if (IsInstanceValid(_audio)) _audio.SetBackgroundMusicPaused(false);
        if (IsInstanceValid(_taskManager))
        {
            _taskManager.OnTaskActivated -= HandleTaskActivated;
        }
    }

    public override void _Process(double delta)
    {
        if (HasBlockingOverlay())
        {
            if (_dragged != null)
            {
                ReturnToBasket(_dragged);
                _dragged = null;
            }
            return;
        }

        // Losing focus or releasing outside the window must not leave an item held.
        if (_dragged != null && (!GetWindow().HasFocus() || !Input.IsMouseButtonPressed(MouseButton.Left)))
        {
            ReturnToBasket(_dragged);
            _dragged = null;
        }

        if (_pendingGroupId.Length == 0 || _animating)
        {
            return;
        }
        _groupCountdown -= delta;
        if (_groupCountdown <= 0.0)
        {
            string nextGroup = _pendingGroupId;
            _pendingGroupId = string.Empty;
            ShowGroup(nextGroup);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_animating || _pendingGroupId.Length > 0 || HasBlockingOverlay()
            || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            return;
        }
        if (!_groups.TryGetValue(_activeGroupId, out List<PicnicItem>? items))
        {
            return;
        }

        Vector2 mouse = GetGlobalMousePosition();
        // Opaque wicker in front of an item must not act as a draggable item surface.
        foreach (Sprite2D front in _basketFronts)
        {
            if (front.Visible && front.IsPixelOpaque(front.ToLocal(mouse))) return;
        }
        // Pick frontmost basket contents first if their edges overlap.
        for (int index = items.Count - 1; index >= 0; index--)
        {
            PicnicItem item = items[index];
            if (item.Placed || !item.Sprite.Visible || !ContainsOpaquePoint(item, mouse))
            {
                continue;
            }
            _dragged = item;
            _grabOffset = item.Sprite.GlobalPosition - mouse;
            item.Sprite.ZIndex = 100;
            _audio.PlayUiClick();
            GetViewport().SetInputAsHandled();
            break;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_dragged == null || HasBlockingOverlay())
        {
            return;
        }
        if (@event is InputEventMouseMotion)
        {
            _dragged.Sprite.GlobalPosition = GetGlobalMousePosition() + _grabOffset;
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            PicnicItem item = _dragged;
            _dragged = null;
            item.Sprite.GlobalPosition = GetGlobalMousePosition() + _grabOffset;
            bool onMat = Geometry2D.IsPointInPolygon(_mat.ToLocal(item.Sprite.GlobalPosition), _mat.Polygon);
            PicnicItem? matchingTarget = onMat ? FindMatchingTarget(item) : null;
            if (matchingTarget != null)
            {
                // Copies of the same object can use either matching outline.
                (item.Target, matchingTarget.Target) = (matchingTarget.Target, item.Target);
                AnimateItem(item, item.Target.GlobalPosition, true);
            }
            else
            {
                ReturnToBasket(item);
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private bool CreateItems()
    {
        if (_taskManager.Tasks.Count != 8 || _mat.Polygon.Length < 3)
        {
            GD.PushError("Day4Picnic: expected 8 task groups and a valid mat polygon.");
            return false;
        }
        foreach (TaskRuntimeState task in _taskManager.Tasks)
        {
            if (!GroupAssets.TryGetValue(task.Definition.Id, out string[]? assets)
                || task.Definition.TargetCount != 5 || task.Definition.Operation != "drag")
            {
                GD.PushError($"Day4Picnic: unsupported group configuration '{task.Definition.Id}'.");
                return false;
            }
            List<PicnicItem> items = [];
            _groups.Add(task.Definition.Id, items);
            for (int itemIndex = 0; itemIndex < task.Definition.TargetCount; itemIndex++)
            {
                string asset = assets[itemIndex % assets.Length];
                string objectId = $"{asset}_{itemIndex + 1:00}";
                Texture2D? texture = GD.Load<Texture2D>($"{AssetRoot}{asset}.png");
                if (texture == null)
                {
                    GD.PushError($"Day4Picnic: missing asset {asset}.png.");
                    return false;
                }
                Image pixels = texture.GetImage();
                Rect2I visibleBounds = GetVisibleBounds(pixels);
                bool isFruit = asset.StartsWith("fruit_");
                float sizeFactor = isFruit
                    ? _random.RandfRange(1.0f - FruitSizeVariation, 1.0f + FruitSizeVariation)
                    : _random.RandfRange(0.92f, 1.06f);
                float scale = ItemSize * sizeFactor * GetAssetSizeFactor(asset) / Math.Max(texture.GetWidth(), texture.GetHeight());
                float rotationLimit = asset.StartsWith("drink_") || asset == "tableware_mug" ? 3.0f : 9.0f;
                if (isFruit) rotationLimit = FruitRotationDegrees;
                float rotation = Mathf.DegToRad(_random.RandfRange(-rotationLimit, rotationLimit));
                Sprite2D sprite = new()
                {
                    Name = objectId, Texture = texture, Scale = Vector2.One * scale,
                    Rotation = rotation, ZIndex = 2, Visible = false,
                    SelfModulate = new Color(_random.RandfRange(0.94f, 1.0f), _random.RandfRange(0.94f, 1.0f), 1.0f)
                };
                Sprite2D target = new()
                {
                    Name = $"{objectId}_target", Texture = texture, Scale = sprite.Scale,
                    Rotation = rotation, ZIndex = 0, Visible = false,
                    SelfModulate = new Color(1, 1, 1, TargetOpacity)
                };
                AddChild(target);
                AddChild(sprite);
                items.Add(new PicnicItem(task.Definition, objectId, asset, sprite, target, pixels, visibleBounds));
            }
        }
        if (!ArrangeTargets()) return false;
        ArrangeBasketContents();
        return true;
    }

    private bool ArrangeTargets()
    {
        // Retry the entire layout at a shared size so one crowded zone cannot hide every group.
        // Sprite scales stay unchanged until a complete layout succeeds.
        for (int retry = 0; retry <= 6; retry++)
        {
            _targetBounds.Clear();
            float layoutScale = 1.0f - retry * 0.05f;
            bool fits = true;
            foreach (TaskRuntimeState task in _taskManager.Tasks)
            {
                List<PicnicItem> items = _groups[task.Definition.Id];
                for (int index = 0; index < items.Count; index++)
                {
                    PicnicItem item = items[index];
                    item.Target.Scale = item.Sprite.Scale * layoutScale;
                    Vector2 preferred = GetPolygonPosition(_mat, GroupPositions[task.Definition.Id][index]);
                    if (!PlaceTarget(item.Target, item.VisibleBounds, preferred))
                    {
                        fits = false;
                        break;
                    }
                }
                if (!fits) break;
            }
            if (!fits) continue;
            foreach (List<PicnicItem> items in _groups.Values)
            {
                foreach (PicnicItem item in items) item.Sprite.Scale = item.Target.Scale;
            }
            return true;
        }
        GD.PushError("Day4Picnic: cannot arrange all targets inside the mat polygon.");
        return false;
    }

    private float GetAssetSizeFactor(string asset)
    {
        if (asset == "tableware_plate") return 1.18f;
        if (asset == "clothing_sun_hat") return ClothingSizeMultiplier * 0.85f;
        if (asset == "clothing_jacket") return ClothingSizeMultiplier * 1.10f;
        if (asset.StartsWith("clothing_")) return ClothingSizeMultiplier;
        if (asset.StartsWith("fruit_") || asset == "bakery_cookie") return 0.88f;
        return 1.0f;
    }

    private bool PlaceTarget(Sprite2D target, Rect2I visibleBounds, Vector2 preferred)
    {
        // Scatter within each practical zone, keeping all future targets clear as well.
        Vector2 start = preferred + new Vector2(_random.RandfRange(-5, 5), _random.RandfRange(-4, 4));
        float phase = _random.RandfRange(-Mathf.Pi, Mathf.Pi);
        for (int attempt = 0; attempt < 97; attempt++)
        {
            float radius = attempt == 0 ? 0.0f : ((attempt - 1) / 16 + 1) * 12.0f;
            float angle = phase + attempt * Mathf.Tau / 16.0f;
            if (TryReserveTarget(target, visibleBounds, start + Vector2.FromAngle(angle) * radius)) return true;
        }

        // A local 72-pixel search can miss usable space. Search the whole mat,
        // preferring the closest free spot to preserve each object's practical zone.
        List<Vector2> candidates = [];
        for (int row = 1; row < 40; row++)
        {
            for (int column = 1; column < 80; column++)
            {
                candidates.Add(GetPolygonPosition(_mat, new Vector2(column / 80.0f, row / 40.0f)));
            }
        }
        candidates.Sort((a, b) => a.DistanceSquaredTo(preferred).CompareTo(b.DistanceSquaredTo(preferred)));
        foreach (Vector2 candidate in candidates)
        {
            if (TryReserveTarget(target, visibleBounds, candidate)) return true;
        }
        return false;
    }

    private bool TryReserveTarget(Sprite2D target, Rect2I visibleBounds, Vector2 position)
    {
        target.GlobalPosition = position;
        Vector2[] corners = GetVisibleCorners(target, visibleBounds);
        Rect2 bounds = new(corners[0], Vector2.Zero);
        foreach (Vector2 corner in corners)
        {
            if (!Geometry2D.IsPointInPolygon(_mat.ToLocal(corner), _mat.Polygon)) return false;
            bounds = bounds.Expand(corner);
        }
        if (_targetBounds.Exists(other => other.Grow(3.0f).Intersects(bounds))) return false;
        _targetBounds.Add(bounds);
        return true;
    }

    private static Vector2[] GetVisibleCorners(Sprite2D sprite, Rect2I bounds)
    {
        Vector2 offset = sprite.Texture.GetSize() * 0.5f;
        return
        [
            sprite.ToGlobal((Vector2)bounds.Position - offset),
            sprite.ToGlobal(new Vector2(bounds.End.X, bounds.Position.Y) - offset),
            sprite.ToGlobal((Vector2)bounds.End - offset),
            sprite.ToGlobal(new Vector2(bounds.Position.X, bounds.End.Y) - offset)
        ];
    }

    private static Vector2 GetPolygonPosition(CollisionPolygon2D region, Vector2 location)
    {
        Vector2[] polygon = region.Polygon;
        float minY = polygon[0].Y;
        float maxY = minY;
        foreach (Vector2 point in polygon)
        {
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }
        float y = Mathf.Lerp(minY, maxY, location.Y);
        List<float> crossings = [];
        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 a = polygon[index];
            Vector2 b = polygon[(index + 1) % polygon.Length];
            if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
            {
                crossings.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            }
        }
        crossings.Sort();
        // Follow the actual polygon's interior span, including the basket openings.
        float left = crossings[0];
        float right = crossings[1];
        for (int index = 2; index + 1 < crossings.Count; index += 2)
        {
            if (crossings[index + 1] - crossings[index] > right - left)
            {
                left = crossings[index];
                right = crossings[index + 1];
            }
        }
        float x = Mathf.Lerp(left, right, location.X);
        return region.ToGlobal(new Vector2(x, y));
    }

    private void ArrangeBasketContents()
    {
        // Prefer open space among random candidates, without a row or column grid.
        // Earlier tasks keep their frontmost layers and positions when activated.
        int groupCount = _taskManager.Tasks.Count;
        List<Vector2>[] basketAnchors = [[], [], []];
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            List<PicnicItem> items = _groups[_taskManager.Tasks[groupIndex].Definition.Id];
            // Two items in each broad left basket, one in the smaller bottom basket.
            int[] basketOrder = [0, 0, 1, 1, 2];
            for (int index = basketOrder.Length - 1; index > 0; index--)
            {
                int other = _random.RandiRange(0, index);
                (basketOrder[index], basketOrder[other]) = (basketOrder[other], basketOrder[index]);
            }
            for (int index = 0; index < items.Count; index++)
            {
                PicnicItem item = items[index];
                int basketIndex = basketOrder[index];
                CollisionPolygon2D basket = _baskets[basketIndex];
                Vector2 anchor = ChooseBasketAnchor(basket, basketAnchors[basketIndex]);
                item.Sprite.GlobalPosition = anchor;
                // Align the visible bottom, not the transparent 200 x 200 canvas, with the basket opening.
                Vector2 visibleBottom = new(item.VisibleBounds.Position.X + item.VisibleBounds.Size.X * 0.5f,
                    item.VisibleBounds.End.Y);
                visibleBottom -= item.Sprite.Texture.GetSize() * 0.5f;
                item.Sprite.GlobalPosition += anchor - item.Sprite.ToGlobal(visibleBottom);
                item.HomePosition = item.Sprite.GlobalPosition;
                item.HomeScale = item.Sprite.Scale;
                item.HomeRotation = item.Sprite.Rotation;
                item.HomeZIndex = 2 + (groupCount - 1 - groupIndex) * items.Count + index;
                item.Sprite.ZIndex = item.HomeZIndex;
                if (item.Asset.StartsWith("fruit_"))
                {
                    // Basket weave is not a lighting cue: keep fruit colors natural here.
                    item.Sprite.SetMeta("skip_background_brightness", true);
                    item.Sprite.SelfModulate = Colors.White;
                }
                item.Sprite.Visible = true;
            }
        }
    }

    private Vector2 ChooseBasketAnchor(CollisionPolygon2D basket, List<Vector2> occupied)
    {
        Vector2 center = GetPolygonPosition(basket, new Vector2(0.5f, 0.5f));
        Vector2 best = center;
        float bestSpacing = -1.0f;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            Vector2 candidate = GetPolygonPosition(basket, new Vector2(
                _random.RandfRange(0.12f, 0.88f), _random.RandfRange(0.22f, 0.82f)));
            candidate = center + (candidate - center) * BasketSpread;
            // Keep the lower edge slightly behind the near rim.
            candidate += Vector2.Down * BasketItemInset;
            float nearest = float.MaxValue;
            foreach (Vector2 other in occupied)
            {
                nearest = Math.Min(nearest, candidate.DistanceSquaredTo(other));
            }
            if (nearest <= bestSpacing) continue;
            best = candidate;
            bestSpacing = nearest;
            if (occupied.Count == 0) break;
        }
        occupied.Add(best);
        return best;
    }

    private void ShowGroup(string taskId)
    {
        if (!_groups.TryGetValue(taskId, out List<PicnicItem>? items))
        {
            return;
        }
        _activeGroupId = taskId;
        foreach (PicnicItem item in items)
        {
            item.Target.Visible = !item.Placed;
        }
    }

    private void ReturnToBasket(PicnicItem item) => AnimateItem(item, item.HomePosition, false);

    private PicnicItem? FindMatchingTarget(PicnicItem item)
    {
        PicnicItem? closest = null;
        float closestDistance = SnapDistance;
        foreach (PicnicItem candidate in _groups[_activeGroupId])
        {
            if (candidate.Placed || !candidate.Target.Visible || candidate.Asset != item.Asset) continue;
            float distance = item.Sprite.GlobalPosition.DistanceTo(candidate.Target.GlobalPosition);
            if (distance > closestDistance) continue;
            closestDistance = distance;
            closest = candidate;
        }
        return closest;
    }

    private void AnimateItem(PicnicItem item, Vector2 destination, bool accept)
    {
        _animating = true;
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.SetParallel();
        tween.TweenProperty(item.Sprite, "global_position", destination, MoveDuration);
        tween.TweenProperty(item.Sprite, "scale", accept ? item.Target.Scale : item.HomeScale, MoveDuration);
        tween.TweenProperty(item.Sprite, "rotation", accept ? item.Target.Rotation : item.HomeRotation, MoveDuration);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            item.Sprite.ZIndex = accept ? 1 : item.HomeZIndex;
            if (accept)
            {
                Godot.Collections.Dictionary report = new()
                {
                    ["operation"] = "drag", ["object_id"] = item.Id,
                    ["object_type"] = item.Definition.TargetType,
                    ["object_color"] = item.Definition.TargetColor,
                    ["object_tags"] = new string[] { item.Definition.TargetTag },
                    ["timestamp"] = (long)Time.GetTicksMsec()
                };
                if (_taskManager.TryHandleInteraction(report))
                {
                    item.Placed = true;
                    item.Target.Hide();
                    if (item.Asset.StartsWith("fruit_"))
                    {
                        item.Sprite.SelfModulate = item.OriginalModulate;
                        item.Sprite.SetMeta("skip_background_brightness", false);
                    }
                    if (_placementSounds.TryGetValue(item.Definition.TargetType, out AudioStream? sound))
                        _audio.PlayEffect(sound);
                    else
                        _audio.PlaySoftInteractionEffect();
                }
                else
                {
                    ReturnToBasket(item);
                    return;
                }
            }
            _animating = false;
        }));
    }

    private void HandleTaskActivated(string taskId)
    {
        _pendingGroupId = taskId;
        _groupCountdown = GroupDelay;
    }

    private bool HasBlockingOverlay()
    {
        foreach (Node child in GetChildren())
        {
            if (child is CanvasLayer { Layer: >= 50 })
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsOpaquePoint(PicnicItem item, Vector2 globalPoint)
    {
        Vector2 pixel = item.Sprite.ToLocal(globalPoint) + item.Sprite.Texture.GetSize() * 0.5f;
        int x = (int)Math.Floor(pixel.X);
        int y = (int)Math.Floor(pixel.Y);
        return x >= 0 && y >= 0 && x < item.Pixels.GetWidth() && y < item.Pixels.GetHeight()
            && item.Pixels.GetPixel(x, y).A > 0.08f;
    }

    private static Rect2I GetVisibleBounds(Image pixels)
    {
        int minX = pixels.GetWidth(), minY = pixels.GetHeight(), maxX = -1, maxY = -1;
        for (int y = 0; y < pixels.GetHeight(); y++)
        {
            for (int x = 0; x < pixels.GetWidth(); x++)
            {
                if (pixels.GetPixel(x, y).A <= 0.08f) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
        }
        return maxX >= minX ? new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1)
            : new Rect2I(0, 0, pixels.GetWidth(), pixels.GetHeight());
    }

    private sealed class PicnicItem(TaskDefinition definition, string id, string asset, Sprite2D sprite,
        Sprite2D target, Image pixels, Rect2I visibleBounds)
    {
        public TaskDefinition Definition { get; } = definition;
        public string Id { get; } = id;
        public string Asset { get; } = asset;
        public Sprite2D Sprite { get; } = sprite;
        public Color OriginalModulate { get; } = sprite.SelfModulate;
        public Sprite2D Target { get; set; } = target;
        public Image Pixels { get; } = pixels;
        public Rect2I VisibleBounds { get; } = visibleBounds;
        public Vector2 HomePosition { get; set; }
        public Vector2 HomeScale { get; set; }
        public float HomeRotation { get; set; }
        public int HomeZIndex { get; set; }
        public bool Placed { get; set; }
    }
}
