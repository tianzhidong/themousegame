extends Node2D

const MODULE_MIN := 1
const MODULE_MAX := 8
const ASSET_ROOT := "res://assets/images/objects/day5"
# Edit PartTarget1..5 in these scenes to adjust installation positions and sizes.
const TARGET_SCENE_ROOT := "res://scenes/game/day5_targets"
const REPAIR_SHOWCASE := preload("res://scenes/game/day5_repair_showcase.tscn")

@export_range(20.0, 150.0, 1.0) var snap_distance := 80.0
@export var target_color := Color(0.35, 0.78, 1.0, 0.45)
@export_range(0.0, 5.0, 0.1) var repaired_return_delay_seconds := 1.0

@onready var _background: Sprite2D = get_node_or_null("M1Bg") as Sprite2D
@onready var _part_1: TextureRect = get_node_or_null("Tbox/BoxContainer/VBoxContainer/TextureRect") as TextureRect
@onready var _part_2: TextureRect = get_node_or_null("Tbox/BoxContainer/VBoxContainer/TextureRect2") as TextureRect
@onready var _part_3: TextureRect = get_node_or_null("Tbox/BoxContainer/VBoxContainer/TextureRect3") as TextureRect
@onready var _part_4: TextureRect = get_node_or_null("Tbox/BoxContainer/VBoxContainer/TextureRect4") as TextureRect
@onready var _part_5: TextureRect = get_node_or_null("Tbox/BoxContainer/VBoxContainer/TextureRect5") as TextureRect

var _toolbox_parts: Array[TextureRect] = []
var _movable_parts: Array[Sprite2D] = []
var _part_targets: Array[Sprite2D] = []
var _installed_parts: Array[Sprite2D] = []
var _dragged_part: Sprite2D
var _grab_offset := Vector2.ZERO
var _module_index := 1
var _task_manager: Node
var _audio_manager: Node
var _task_type := ""
var _installed_count := 0
var _return_pending := false


func _ready() -> void:
	_audio_manager = get_node("/root/AudioManager")
	var module_index := clampi(GameState.get_selected_day5_module(), MODULE_MIN, MODULE_MAX)
	_module_index = module_index
	_task_type = GameState.get_day5_task_type(module_index)
	GameState.set_selected_day(5)
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager != null:
		_task_manager.call("EnsureSelectedDayLoaded")
	_set_texture(_background, "%s/m%d_bg.png" % [ASSET_ROOT, module_index])

	_toolbox_parts.assign([_part_1, _part_2, _part_3, _part_4, _part_5])
	for part_index in _toolbox_parts.size():
		var part := _toolbox_parts[part_index]
		_set_texture(part, "%s/m%d/m%d_%d.png" % [ASSET_ROOT, module_index, module_index, part_index + 1])
		if part != null:
			part.mouse_filter = Control.MOUSE_FILTER_IGNORE

	# These containers only arrange the toolbox; let clicks reach the part picker.
	for path in ["Tbox/BoxContainer", "Tbox/BoxContainer/VBoxContainer"]:
		var container := get_node(path) as Control
		container.mouse_filter = Control.MOUSE_FILTER_IGNORE

	var task_panel := get_node_or_null("Taskbar/TaskPanel")
	if task_panel != null:
		# The last machine gets the same showcase before the day result appears.
		task_panel.set("AutoShowResultOnCompletion", false)
		task_panel.call("FocusTask", "repair_" + _task_type)
	_create_part_targets(module_index)
	_restore_installed_parts()


func _create_part_targets(module_index: int) -> void:
	var silhouette_shader := Shader.new()
	silhouette_shader.code = "shader_type canvas_item; uniform vec4 hint_color : source_color; void fragment() { COLOR = vec4(hint_color.rgb, texture(TEXTURE, UV).a * hint_color.a); }"
	var layout_scene := load("%s/m%d.tscn" % [TARGET_SCENE_ROOT, module_index]) as PackedScene
	if layout_scene == null:
		push_error("Day5 is missing the target scene for machine %d" % module_index)
		_return_pending = true
		return
	var layout := layout_scene.instantiate() as Sprite2D
	for part_index in _toolbox_parts.size():
		var source := _toolbox_parts[part_index]
		var template := layout.get_node("PartTarget%d" % (part_index + 1)) as Sprite2D
		var target := template.duplicate() as Sprite2D
		add_child(target)
		_part_targets.append(target)
		if source == null or source.texture == null or _background == null:
			target.hide()
			continue

		target.texture = source.texture
		var image_origin := Transform2D(0.0, -Vector2(_background.texture.get_size()) * 0.5)
		target.transform = global_transform.affine_inverse() * _background.get_global_transform() * image_origin * template.transform
		var material := ShaderMaterial.new()
		material.shader = silhouette_shader
		material.set_shader_parameter("hint_color", target_color)
		target.material = material
	layout.free()


func _unhandled_input(event: InputEvent) -> void:
	if _return_pending:
		return
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	var local_mouse := get_global_transform_with_canvas().affine_inverse() * mouse_event.position
	# The last picked part is drawn on top and should receive overlapping clicks first.
	for index in range(_movable_parts.size() - 1, -1, -1):
		var part := _movable_parts[index]
		if part.get_rect().has_point(part.get_transform().affine_inverse() * local_mouse):
			_begin_drag(part, local_mouse)
			return

	for part_index in _toolbox_parts.size():
		var source := _toolbox_parts[part_index]
		if source == null or source.texture == null:
			continue
		var source_mouse := source.get_global_transform_with_canvas().affine_inverse() * mouse_event.position
		if not Rect2(Vector2.ZERO, source.size).has_point(source_mouse):
			continue

		var part := Sprite2D.new()
		part.texture = source.texture
		part.set_meta("part_index", part_index)
		part.z_index = 1
		add_child(part)
		part.transform = global_transform.affine_inverse() * source.get_global_transform()
		part.scale *= source.size / Vector2(source.texture.get_size())
		part.position += part.transform.basis_xform(Vector2(part.texture.get_size()) * 0.5)
		_movable_parts.append(part)
		# Remove the toolbox image completely, retaining only its empty layout slot.
		source.custom_minimum_size = source.get_combined_minimum_size()
		source.texture = null
		_begin_drag(part, local_mouse)
		return


func _input(event: InputEvent) -> void:
	if _dragged_part == null:
		return

	if event is InputEventMouseMotion:
		if (event.button_mask & MOUSE_BUTTON_MASK_LEFT) == 0:
			_finish_drag()
			return
		_move_dragged_part(event.position)
		get_viewport().set_input_as_handled()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
		_move_dragged_part(event.position)
		_finish_drag()
		get_viewport().set_input_as_handled()


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT:
		_dragged_part = null


func _begin_drag(part: Sprite2D, local_mouse: Vector2) -> void:
	_dragged_part = part
	_grab_offset = local_mouse - part.position
	_movable_parts.erase(part)
	_movable_parts.append(part)
	move_child(part, get_child_count() - 1)
	get_viewport().set_input_as_handled()


func _move_dragged_part(viewport_mouse: Vector2) -> void:
	var viewport_to_local := get_global_transform_with_canvas().affine_inverse()
	var bounds := get_viewport().get_visible_rect()
	var half_size := _dragged_part.get_rect().size * _dragged_part.scale.abs() * 0.5
	var minimum := viewport_to_local * bounds.position + half_size
	var maximum := viewport_to_local * bounds.end - half_size
	var target := viewport_to_local * viewport_mouse - _grab_offset
	_dragged_part.position = Vector2(
		clampf(target.x, minimum.x, maxf(minimum.x, maximum.x)),
		clampf(target.y, minimum.y, maxf(minimum.y, maximum.y))
	)


func _finish_drag() -> void:
	var part := _dragged_part
	_dragged_part = null
	if part == null:
		return

	var part_index := int(part.get_meta("part_index"))
	var target := _part_targets[part_index]
	if not target.visible or not _is_in_snap_area(part, target):
		return
	if _task_manager == null or not bool(_task_manager.call("TryHandleInteraction", {
		"operation": "drag",
		"object_id": "day5_m%d_part%d" % [_module_index, part_index + 1],
		"object_type": _task_type,
		"object_tags": [_task_type],
		"object_color": "",
		"timestamp": Time.get_ticks_msec()
	})):
		return

	part.transform = target.transform
	part.offset = target.offset
	target.hide()
	_movable_parts.erase(part)
	_installed_parts.append(part)
	var installed: Array = GameState.day5_repaired_parts.get(_module_index, [])
	installed.append(part_index)
	GameState.day5_repaired_parts[_module_index] = installed
	_installed_count += 1
	if _installed_count == _toolbox_parts.size():
		_show_repaired_state()


func _is_in_snap_area(part: Sprite2D, target: Sprite2D) -> bool:
	if not bool(target.get_meta("snap_use_visible_rect", false)):
		return part.position.distance_to(target.position) <= snap_distance
	# Long parts use their full visible target, following editor position/scale/rotation.
	var used := target.texture.get_image().get_used_rect()
	var origin := Vector2(used.position) + target.offset
	if target.centered:
		origin -= Vector2(target.texture.get_size()) * 0.5
	var padding: Vector2 = target.get_meta("snap_padding", Vector2.ZERO)
	var hot_rect := Rect2(origin - padding, Vector2(used.size) + padding * 2.0)
	var part_used := part.texture.get_image().get_used_rect()
	var visible_center := Vector2(part_used.get_center()) + part.offset
	if part.centered:
		visible_center -= Vector2(part.texture.get_size()) * 0.5
	var point_in_target := target.transform.affine_inverse() * (part.transform * visible_center)
	return hot_rect.has_point(point_in_target)


func _restore_installed_parts() -> void:
	if _task_manager == null:
		return
	var progress := int(_task_manager.call("GetTaskProgress", "repair_" + _task_type))
	if progress == 0:
		GameState.day5_repaired_parts.erase(_module_index)
		return
	var installed: Array = GameState.day5_repaired_parts.get(_module_index, [])
	if progress == _toolbox_parts.size():
		installed = [0, 1, 2, 3, 4]
	for index in installed:
		var source := _toolbox_parts[int(index)]
		var target := _part_targets[int(index)]
		var part := Sprite2D.new()
		part.texture = target.texture
		part.z_index = 1
		add_child(part)
		part.transform = target.transform
		part.offset = target.offset
		_installed_parts.append(part)
		source.custom_minimum_size = source.get_combined_minimum_size()
		source.texture = null
		target.hide()
		_installed_count += 1
	if _installed_count == _toolbox_parts.size():
		_show_repaired_state()


func _show_repaired_state() -> void:
	if _return_pending:
		return
	_return_pending = true
	# Keep the installed sprites so editor adjustments survive completion.
	for target in _part_targets:
		target.hide()
	var showcase := REPAIR_SHOWCASE.instantiate()
	showcase.set("module_index", _module_index)
	showcase.set("all_repaired", bool(_task_manager.call("IsDayComplete")))
	add_child(showcase)
	var repair_player := _audio_manager.call("GetDay5RepairPlayer") as AudioStreamPlayer
	if is_instance_valid(repair_player) and repair_player.playing:
		await repair_player.finished
	if not is_inside_tree():
		return
	await get_tree().create_timer(repaired_return_delay_seconds, false).timeout
	if not is_inside_tree():
		return
	if bool(_task_manager.call("IsDayComplete")):
		var task_panel := get_node_or_null("Taskbar/TaskPanel")
		if task_panel != null:
			task_panel.call("ShowCompletionResult")
	else:
		get_tree().change_scene_to_file("res://scenes/levels/level_5.tscn")


func _set_texture(target: CanvasItem, texture_path: String) -> void:
	if target == null:
		push_error("Fixgame is missing a texture node for: %s" % texture_path)
		return

	if not ResourceLoader.exists(texture_path):
		push_error("Fixgame texture does not exist: %s" % texture_path)
		return

	var loaded_texture := load(texture_path) as Texture2D
	if loaded_texture == null:
		push_error("Fixgame could not load texture: %s" % texture_path)
		return

	if target is Sprite2D:
		(target as Sprite2D).texture = loaded_texture
	elif target is TextureRect:
		(target as TextureRect).texture = loaded_texture
