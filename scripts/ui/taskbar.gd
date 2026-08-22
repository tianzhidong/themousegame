extends Node2D

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const EXPANDED_TEXTURE := preload("res://assets/images/ui/taskbar.png")
const COLLAPSED_TEXTURE := preload("res://assets/images/ui/taskbar2.png")

@onready var _taskbar_sprite: Sprite2D = $CanvasLayer/Taskbar
@onready var _task_label: Sprite2D = $CanvasLayer/Taskbar/Tasklabel

var _content_clip: Control
var _content_root: Node2D
var _collapsed_clip_shape: CollisionShape2D
var _collapsed_clip_area: Area2D
var _expanded_position := Vector2.ZERO
var _expanded_top_y := 0.0
var _task_label_global_position := Vector2.ZERO
var _is_collapsed := false
var _click_player: AudioStreamPlayer


func _ready() -> void:
	_expanded_position = _taskbar_sprite.position
	_expanded_top_y = _get_top_y(EXPANDED_TEXTURE, _expanded_position.y)
	_task_label_global_position = _task_label.global_position
	_collapsed_clip_shape = get_node_or_null("CanvasLayer/Taskbar/Area2D/CollisionShape2D") as CollisionShape2D
	if _collapsed_clip_shape != null:
		_collapsed_clip_area = _collapsed_clip_shape.get_parent() as Area2D
	_task_label.z_index = 100
	_create_content_clip()
	_update_content_clip(false, Rect2())

	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)


func _unhandled_input(event: InputEvent) -> void:
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	if _contains_sprite_point(_task_label, mouse_event.position):
		_click_player.play()
		_set_collapsed(not _is_collapsed)
		get_viewport().set_input_as_handled()


func _set_collapsed(collapsed: bool) -> void:
	var content_global_position: Vector2 = _content_root.global_position
	var clip_global_rect := Rect2()
	if collapsed:
		clip_global_rect = _get_collision_clip_global_rect()

	_is_collapsed = collapsed
	_taskbar_sprite.texture = COLLAPSED_TEXTURE if collapsed else EXPANDED_TEXTURE
	_taskbar_sprite.position = Vector2(
		_expanded_position.x,
		_get_center_y(_taskbar_sprite.texture) if collapsed else _expanded_position.y
	)
	_update_content_clip(collapsed, clip_global_rect)
	_content_root.global_position = content_global_position
	_set_content_nodes_visible()
	_task_label.visible = true
	_task_label.global_position = _task_label_global_position


func _create_content_clip() -> void:
	_content_clip = Control.new()
	_content_clip.name = "ContentClip"
	_content_clip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_content_clip.clip_contents = false
	_taskbar_sprite.add_child(_content_clip)
	_content_clip.position = Vector2.ZERO
	_content_clip.size = Vector2.ZERO

	_content_root = Node2D.new()
	_content_root.name = "ContentRoot"
	_content_clip.add_child(_content_root)

	for child in _taskbar_sprite.get_children().duplicate():
		if child == _task_label or child == _content_clip or child == _collapsed_clip_area:
			continue
		child.reparent(_content_root, true)
	_set_content_nodes_visible()


func _set_content_nodes_visible() -> void:
	if _content_root == null:
		return

	for child in _content_root.get_children():
		if child is CanvasItem:
			var should_be_visible := true
			if child.has_meta("task_panel_visible"):
				should_be_visible = bool(child.get_meta("task_panel_visible"))
			(child as CanvasItem).visible = should_be_visible


func _update_content_clip(collapsed: bool, clip_global_rect: Rect2) -> void:
	if _content_clip == null:
		return

	if not collapsed:
		_content_clip.clip_contents = false
		_content_clip.position = Vector2.ZERO
		_content_clip.size = Vector2.ZERO
		return

	var clip_rect := _global_rect_to_taskbar_local(clip_global_rect)
	if clip_global_rect.size == Vector2.ZERO:
		var texture_size := Vector2(COLLAPSED_TEXTURE.get_size())
		clip_rect = Rect2(-texture_size * 0.5, texture_size)

	_content_clip.clip_contents = true
	_content_clip.position = clip_rect.position
	_content_clip.size = clip_rect.size


func _get_collision_clip_global_rect() -> Rect2:
	if _collapsed_clip_shape == null or not is_instance_valid(_collapsed_clip_shape):
		return Rect2()
	if _collapsed_clip_shape.shape == null:
		return Rect2()

	var half_size := Vector2.ZERO
	var rectangle_shape := _collapsed_clip_shape.shape as RectangleShape2D
	if rectangle_shape != null:
		half_size = rectangle_shape.size * 0.5
	else:
		var circle_shape := _collapsed_clip_shape.shape as CircleShape2D
		if circle_shape != null:
			half_size = Vector2.ONE * circle_shape.radius
		else:
			var capsule_shape := _collapsed_clip_shape.shape as CapsuleShape2D
			if capsule_shape == null:
				return Rect2()
			half_size = Vector2(capsule_shape.radius, capsule_shape.height * 0.5)

	var corners := [
		Vector2(-half_size.x, -half_size.y),
		Vector2(half_size.x, -half_size.y),
		Vector2(half_size.x, half_size.y),
		Vector2(-half_size.x, half_size.y),
	]
	var min_point := Vector2(INF, INF)
	var max_point := Vector2(-INF, -INF)
	for corner in corners:
		var local_corner: Vector2 = corner
		var global_point: Vector2 = _collapsed_clip_shape.global_transform * local_corner
		min_point.x = minf(min_point.x, global_point.x)
		min_point.y = minf(min_point.y, global_point.y)
		max_point.x = maxf(max_point.x, global_point.x)
		max_point.y = maxf(max_point.y, global_point.y)

	return Rect2(min_point, max_point - min_point)


func _global_rect_to_taskbar_local(global_rect: Rect2) -> Rect2:
	if global_rect.size == Vector2.ZERO:
		return Rect2()

	var global_corners: Array[Vector2] = [
		global_rect.position,
		Vector2(global_rect.end.x, global_rect.position.y),
		global_rect.end,
		Vector2(global_rect.position.x, global_rect.end.y),
	]
	var min_point := Vector2(INF, INF)
	var max_point := Vector2(-INF, -INF)
	for global_corner in global_corners:
		var local_point: Vector2 = _taskbar_sprite.to_local(global_corner)
		min_point.x = minf(min_point.x, local_point.x)
		min_point.y = minf(min_point.y, local_point.y)
		max_point.x = maxf(max_point.x, local_point.x)
		max_point.y = maxf(max_point.y, local_point.y)

	return Rect2(min_point, max_point - min_point)


func _get_top_y(texture: Texture2D, center_y: float) -> float:
	return center_y - texture.get_height() * absf(_taskbar_sprite.scale.y) * 0.5


func _get_center_y(texture: Texture2D) -> float:
	return _expanded_top_y + texture.get_height() * absf(_taskbar_sprite.scale.y) * 0.5


func _contains_sprite_point(sprite: Sprite2D, global_point: Vector2) -> bool:
	if not sprite.visible or sprite.texture == null:
		return false

	return sprite.get_rect().has_point(sprite.to_local(global_point))
