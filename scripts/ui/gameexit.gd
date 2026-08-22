extends Node2D

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const CHOSE_DAY_SCENE := "res://scenes/ui/choseday.tscn"
const HOVER_SCALE := 1.06
const TWEEN_DURATION := 0.1

@onready var _exit_button: Sprite2D = $ExitWin/ExitButton
@onready var _continue_button: Sprite2D = $ExitWin/ContinueButton

var _exit_base_scale: Vector2
var _continue_base_scale: Vector2
var _exit_hovered := false
var _continue_hovered := false
var _scale_tween: Tween
var _click_player: AudioStreamPlayer


func _ready() -> void:
	_exit_base_scale = _exit_button.scale
	_continue_base_scale = _continue_button.scale

	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)


func _process(_delta: float) -> void:
	var mouse_position := get_global_mouse_position()
	var exit_hovered := _contains_sprite_point(_exit_button, mouse_position)
	var continue_hovered := _contains_sprite_point(_continue_button, mouse_position)

	if exit_hovered != _exit_hovered:
		_exit_hovered = exit_hovered
		_tween_scale(_exit_button, _exit_base_scale * HOVER_SCALE if exit_hovered else _exit_base_scale)

	if continue_hovered != _continue_hovered:
		_continue_hovered = continue_hovered
		_tween_scale(
			_continue_button,
			_continue_base_scale * HOVER_SCALE if continue_hovered else _continue_base_scale
		)


func _input(event: InputEvent) -> void:
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null:
		return

	if mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	if _contains_sprite_point(_exit_button, mouse_event.position):
		_click_player.play()
		get_viewport().set_input_as_handled()
		await get_tree().create_timer(TWEEN_DURATION).timeout
		get_tree().change_scene_to_file(CHOSE_DAY_SCENE)
		return

	if _contains_sprite_point(_continue_button, mouse_event.position):
		_click_player.play()
		get_viewport().set_input_as_handled()
		_close_exit_overlay()


func _contains_sprite_point(sprite: Sprite2D, global_point: Vector2) -> bool:
	if sprite.texture == null:
		return false

	return sprite.get_rect().has_point(sprite.to_local(global_point))


func _tween_scale(sprite: Sprite2D, target_scale: Vector2) -> void:
	if _scale_tween != null:
		_scale_tween.kill()

	_scale_tween = create_tween()
	_scale_tween.set_trans(Tween.TRANS_QUAD)
	_scale_tween.set_ease(Tween.EASE_OUT)
	_scale_tween.tween_property(sprite, "scale", target_scale, TWEEN_DURATION)


func _close_exit_overlay() -> void:
	var overlay := get_parent()
	if overlay is CanvasLayer:
		overlay.queue_free()
	else:
		queue_free()
