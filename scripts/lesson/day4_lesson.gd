extends "res://scripts/ui/lesson.gd"

const SUCCESS_SOUND := preload("res://assets/audio/ui/target_success.ogg")
const LEVEL_4_SCENE := "res://scenes/levels/level_4.tscn"

@export_range(1.0, 30.0, 1.0) var minimum_drag_distance := 8.0

@onready var _drop_zone: CollisionPolygon2D = $Area2D/CollisionPolygon2D
@onready var _tomato: Sprite2D = $Area2D/CollisionPolygon2D/VegetableTomato

var _success_player: AudioStreamPlayer
var _original_position: Vector2
var _tomato_base_scale: Vector2
var _tomato_is_hovered := false
var _tomato_scale_tween: Tween
var _drag_offset: Vector2
var _drag_start: Vector2
var _drag_distance := 0.0
var _is_dragging := false
var _is_starting := false


func _ready() -> void:
	_original_position = _tomato.global_position
	_tomato_base_scale = _tomato.scale
	teaching_started.connect(_cancel_drag)
	super._ready()
	_success_player = AudioStreamPlayer.new()
	_success_player.stream = SUCCESS_SOUND
	add_child(_success_player)


func _process(delta: float) -> void:
	super._process(delta)
	var hovered := not _is_starting and (
		_is_dragging or _contains_global_point(_tomato, get_global_mouse_position())
	)
	_set_tomato_hovered(hovered)


func _set_tomato_hovered(hovered: bool) -> void:
	if hovered == _tomato_is_hovered:
		return
	_tomato_is_hovered = hovered
	if _tomato_scale_tween != null:
		_tomato_scale_tween.kill()
	_tomato_scale_tween = create_tween()
	_tomato_scale_tween.set_trans(Tween.TRANS_QUAD)
	_tomato_scale_tween.set_ease(Tween.EASE_OUT)
	var target_scale := _tomato_base_scale * HOVER_SCALE if hovered else _tomato_base_scale
	_tomato_scale_tween.tween_property(_tomato, "scale", target_scale, TWEEN_DURATION)


func _input(event: InputEvent) -> void:
	if _is_starting:
		return
	super._input(event)
	if not can_accept_practice_input():
		_cancel_drag()
		return
	if get_viewport().is_input_handled():
		return

	var mouse_position := get_global_mouse_position()
	if event is InputEventMouseMotion and _is_dragging:
		_move_tomato(mouse_position)
		get_viewport().set_input_as_handled()
		return

	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return
	if mouse_event.pressed:
		if _contains_global_point(_tomato, mouse_position):
			_is_dragging = true
			_drag_start = mouse_position
			_drag_distance = 0.0
			_drag_offset = _tomato.global_position - mouse_position
			get_viewport().set_input_as_handled()
	elif _is_dragging:
		_move_tomato(mouse_position)
		_is_dragging = false
		get_viewport().set_input_as_handled()
		var inside_target := Geometry2D.is_point_in_polygon(
			_drop_zone.to_local(_tomato.global_position), _drop_zone.polygon
		)
		if inside_target and _drag_distance >= minimum_drag_distance:
			_start_practice()
		else:
			_cancel_drag()


func _move_tomato(mouse_position: Vector2) -> void:
	_tomato.global_position = mouse_position + _drag_offset
	_drag_distance = maxf(_drag_distance, mouse_position.distance_to(_drag_start))


func _cancel_drag() -> void:
	_is_dragging = false
	_tomato.global_position = _original_position


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and _is_dragging:
		_cancel_drag()


func _start_practice() -> void:
	if _is_starting or not can_accept_practice_input():
		return
	_is_starting = true
	_set_tomato_hovered(false)
	finish_practice()
	_success_player.play()
	await _success_player.finished
	get_node("/root/GameState").call("set_selected_day", 4)
	get_tree().change_scene_to_file(LEVEL_4_SCENE)
