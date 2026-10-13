extends Sprite2D

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const FIX_GAME_SCENE := "res://scenes/game/fixgame.tscn"
const HOVER_SCALE := 1.06
const TWEEN_DURATION := 0.1
const SCENE_CHANGE_DELAY := 0.1

@export_range(1, 8, 1) var module_index := 1

var _base_scale := Vector2.ONE
var _is_hovered := false
var _is_changing_scene := false
var _scale_tween: Tween
var _click_player: AudioStreamPlayer
var _task_manager: Node
var _task_id := ""
var _repaired_parts: Node2D


func _ready() -> void:
	_base_scale = scale
	_create_repaired_parts()
	GameState.set_selected_day(5)
	_task_id = "repair_" + GameState.get_day5_task_type(module_index)
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager != null:
		_task_manager.call("EnsureSelectedDayLoaded")
	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)


func _process(_delta: float) -> void:
	var state := "Active"
	if _task_manager != null:
		state = str(_task_manager.call("GetTaskState", _task_id))
	_repaired_parts.visible = state == "Completed"
	self_modulate = Color.WHITE if state in ["Active", "Completed"] else Color(0.65, 0.65, 0.65)
	var hovered := state == "Active" and _contains_global_point(get_global_mouse_position())
	if hovered == _is_hovered:
		return

	_is_hovered = hovered
	_tween_scale(_base_scale * HOVER_SCALE if _is_hovered else _base_scale)


func _unhandled_input(event: InputEvent) -> void:
	if _is_changing_scene:
		return
	if not _can_select():
		return

	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	if not _contains_global_point(mouse_event.global_position):
		return

	_is_changing_scene = true
	GameState.set_selected_day5_module(module_index)
	_click_player.play()
	get_viewport().set_input_as_handled()
	await get_tree().create_timer(SCENE_CHANGE_DELAY).timeout
	get_tree().change_scene_to_file(FIX_GAME_SCENE)


func _can_select() -> bool:
	return _task_manager == null or str(_task_manager.call("GetTaskState", _task_id)) == "Active"


func _create_repaired_parts() -> void:
	_repaired_parts = Node2D.new()
	_repaired_parts.name = "RepairedParts"
	_repaired_parts.z_index = 1
	_repaired_parts.hide()
	add_child(_repaired_parts)
	var scene := load("res://scenes/game/day5_targets/m%d.tscn" % module_index) as PackedScene
	if scene == null:
		return
	var layout := scene.instantiate() as Sprite2D
	var yard_scale: float = layout.get_meta("yard_scale")
	var yard_offset: Vector2 = layout.get_meta("yard_offset")
	var to_yard := Transform2D(Vector2(yard_scale, 0), Vector2(0, yard_scale), yard_offset)
	for index in range(1, 6):
		var template := layout.get_node("PartTarget%d" % index) as Sprite2D
		var part := template.duplicate() as Sprite2D
		_repaired_parts.add_child(part)
		part.transform = to_yard * template.transform
	layout.free()


func _contains_global_point(global_point: Vector2) -> bool:
	return texture != null and get_rect().has_point(to_local(global_point))


func _tween_scale(target_scale: Vector2) -> void:
	if _scale_tween != null:
		_scale_tween.kill()

	_scale_tween = create_tween()
	_scale_tween.set_trans(Tween.TRANS_QUAD)
	_scale_tween.set_ease(Tween.EASE_OUT)
	_scale_tween.tween_property(self, "scale", target_scale, TWEEN_DURATION)
