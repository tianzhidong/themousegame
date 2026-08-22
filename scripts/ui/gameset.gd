extends Node2D

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const MUSIC_BUS_NAME := "Music"
const HOVER_SCALE := 1.06
const TWEEN_DURATION := 0.1
const TRACK_START_RATIO := 0.384
const TRACK_END_RATIO := 0.938

@onready var _panel: Sprite2D = $Blank2
@onready var _volume_bar: Sprite2D = $Blank2/Bgmv
@onready var _dot: Sprite2D = $Blank2/Dot
@onready var _ok_button: Sprite2D = $Blank2/OKbutton

var _track_start := Vector2.ZERO
var _track_end := Vector2.ZERO
var _is_dragging := false
var _ok_base_scale: Vector2
var _ok_is_hovered := false
var _scale_tween: Tween
var _click_player: AudioStreamPlayer


func _ready() -> void:
	_ok_base_scale = _ok_button.scale

	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)

	_update_track_points()
	_set_volume(_get_music_volume())


func _process(_delta: float) -> void:
	var ok_hovered := _contains_sprite_point(_ok_button, get_global_mouse_position())
	if ok_hovered == _ok_is_hovered:
		return

	_ok_is_hovered = ok_hovered
	_tween_ok_scale(_ok_base_scale * HOVER_SCALE if _ok_is_hovered else _ok_base_scale)


func _input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and _is_dragging:
		_set_volume_from_global_position(event.position)
		get_viewport().set_input_as_handled()
		return

	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return

	if mouse_event.pressed:
		if _contains_sprite_point(_ok_button, mouse_event.position):
			_click_player.play()
			get_viewport().set_input_as_handled()
			await get_tree().create_timer(TWEEN_DURATION).timeout
			_close_settings_overlay()
			return

		if _is_on_volume_track(mouse_event.position):
			_is_dragging = true
			_set_volume_from_global_position(mouse_event.position)
			get_viewport().set_input_as_handled()
			return

		if _contains_sprite_point(_dot, mouse_event.position):
			_is_dragging = true
			get_viewport().set_input_as_handled()
			return

		return

	_is_dragging = false


func _update_track_points() -> void:
	var bar_rect := _volume_bar.get_rect()
	var track_y := 0.5 * bar_rect.size.y - bar_rect.size.y * 0.5
	var start_x := lerpf(-bar_rect.size.x * 0.5, bar_rect.size.x * 0.5, TRACK_START_RATIO)
	var end_x := lerpf(-bar_rect.size.x * 0.5, bar_rect.size.x * 0.5, TRACK_END_RATIO)

	_track_start = _panel.to_local(_volume_bar.to_global(Vector2(start_x, track_y)))
	_track_end = _panel.to_local(_volume_bar.to_global(Vector2(end_x, track_y)))


func _is_on_volume_track(global_position: Vector2) -> bool:
	var panel_position := _panel.to_local(global_position)
	var track_y := _track_start.y
	var track_min_x := minf(_track_start.x, _track_end.x)
	var track_max_x := maxf(_track_start.x, _track_end.x)
	return panel_position.x >= track_min_x \
		and panel_position.x <= track_max_x \
		and absf(panel_position.y - track_y) <= 48.0


func _set_volume_from_global_position(global_position: Vector2) -> void:
	var panel_position := _panel.to_local(global_position)
	var value := inverse_lerp(_track_start.x, _track_end.x, panel_position.x)
	_set_volume(clampf(value, 0.0, 1.0))


func _set_volume(value: float) -> void:
	var bus_index := AudioServer.get_bus_index(MUSIC_BUS_NAME)
	if bus_index < 0:
		return

	if value <= 0.0:
		AudioServer.set_bus_volume_db(bus_index, -80.0)
	else:
		AudioServer.set_bus_volume_db(bus_index, linear_to_db(value))

	_dot.position = Vector2(
		lerpf(_track_start.x, _track_end.x, value),
		lerpf(_track_start.y, _track_end.y, value)
	)


func _get_music_volume() -> float:
	var bus_index := AudioServer.get_bus_index(MUSIC_BUS_NAME)
	if bus_index < 0:
		return 1.0

	var volume_db := AudioServer.get_bus_volume_db(bus_index)
	if volume_db <= -80.0:
		return 0.0

	return db_to_linear(volume_db)


func _contains_sprite_point(sprite: Sprite2D, global_point: Vector2) -> bool:
	if sprite.texture == null:
		return false

	return sprite.get_rect().has_point(sprite.to_local(global_point))


func _tween_ok_scale(target_scale: Vector2) -> void:
	if _scale_tween != null:
		_scale_tween.kill()

	_scale_tween = create_tween()
	_scale_tween.set_trans(Tween.TRANS_QUAD)
	_scale_tween.set_ease(Tween.EASE_OUT)
	_scale_tween.tween_property(_ok_button, "scale", target_scale, TWEEN_DURATION)


func _close_settings_overlay() -> void:
	var overlay := get_parent()
	if overlay is CanvasLayer:
		overlay.queue_free()
	else:
		queue_free()
