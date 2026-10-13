extends "res://scripts/ui/lesson.gd"

const SUCCESS_SOUND := preload("res://assets/audio/ui/target_success.ogg")
const BOX_OPEN_SOUND := preload("res://assets/audio/voice/day8/打开盒子.mp3")
const LEVEL_8_SCENE := "res://scenes/levels/level_8.tscn"

@export var opened_box_texture: Texture2D

@onready var _practice_area: Area2D = $PracticeArea
@onready var _box: Sprite2D = $PracticeArea/Box

var _success_player: AudioStreamPlayer
var _box_open_player: AudioStreamPlayer
var _is_starting := false
var _has_practice_click := false
var _box_base_scale: Vector2
var _area_base_scale: Vector2
var _box_is_hovered := false
var _box_hover_tween: Tween


func _ready() -> void:
	_box_base_scale = _box.scale
	_area_base_scale = _practice_area.scale
	teaching_started.connect(_reset_practice_click)
	super._ready()
	get_node("/root/GameState").call("set_selected_day", 8)
	_success_player = AudioStreamPlayer.new()
	_success_player.stream = SUCCESS_SOUND
	add_child(_success_player)
	_box_open_player = AudioStreamPlayer.new()
	_box_open_player.stream = BOX_OPEN_SOUND
	add_child(_box_open_player)
	$PracticeArea.input_event.connect(_on_practice_input)


func _process(delta: float) -> void:
	super._process(delta)
	_set_box_hovered(not _is_starting and _contains_global_point(_box, get_global_mouse_position()))


func _set_box_hovered(hovered: bool) -> void:
	if hovered == _box_is_hovered:
		return
	_box_is_hovered = hovered
	if _box_hover_tween != null:
		_box_hover_tween.kill()
	_box_hover_tween = create_tween()
	_box_hover_tween.set_trans(Tween.TRANS_QUAD)
	_box_hover_tween.set_ease(Tween.EASE_OUT)
	var target_scale := _area_base_scale * HOVER_SCALE if hovered else _area_base_scale
	_box_hover_tween.tween_property(_practice_area, "scale", target_scale, TWEEN_DURATION)


func _on_practice_input(_viewport: Node, event: InputEvent, _shape_idx: int) -> void:
	var mouse_event := event as InputEventMouseButton
	if _is_starting or not can_accept_practice_input() or mouse_event == null:
		return
	if mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return
	var valid_double_click := mouse_event.double_click and _has_practice_click
	_has_practice_click = true
	if not valid_double_click:
		return

	_is_starting = true
	_set_box_hovered(false)
	finish_practice()
	get_viewport().set_input_as_handled()
	_voice_player.stop()
	await _play_box_open_animation()
	if _box_open_player.playing:
		await _box_open_player.finished
	_success_player.play()
	await _success_player.finished
	get_tree().change_scene_to_file(LEVEL_8_SCENE)


func _play_box_open_animation() -> void:
	var animation := create_tween()
	animation.set_trans(Tween.TRANS_QUAD)
	animation.set_ease(Tween.EASE_OUT)
	animation.tween_property(_box, "scale", _box_base_scale * Vector2(1.06, 0.9), 0.12)
	animation.tween_callback(_show_opened_box)
	var width_ratio := _box.texture.get_width() / float(opened_box_texture.get_width())
	animation.tween_property(_box, "scale", _box_base_scale * width_ratio, 0.4).set_trans(Tween.TRANS_BACK)
	animation.tween_interval(0.65)
	await animation.finished


func _show_opened_box() -> void:
	_box_open_player.play()
	var width_ratio := _box.texture.get_width() / float(opened_box_texture.get_width())
	_box.texture = opened_box_texture
	# Both states share a bottom anchor, so the box stays on the same surface.
	_box.offset = Vector2(-opened_box_texture.get_width() * 0.5, -opened_box_texture.get_height())
	_box.scale = _box_base_scale * width_ratio * 0.9


func _reset_practice_click() -> void:
	_has_practice_click = false


func _input(event: InputEvent) -> void:
	if not _is_starting:
		super._input(event)
		var mouse_event := event as InputEventMouseButton
		if mouse_event != null and mouse_event.button_index == MOUSE_BUTTON_LEFT and mouse_event.pressed:
			if not _contains_global_point(_box, get_global_mouse_position()):
				_has_practice_click = false
