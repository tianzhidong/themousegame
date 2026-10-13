extends Node2D

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const MAIN_SCENE_PATH := "res://scenes/main/Main.tscn"
const HOVER_SCALE := 1.06
const PRESSED_SCALE := 0.96
const TWEEN_DURATION := 0.1
const SCENE_CHANGE_DELAY := 0.12
const DAY12_COMPLETION_VOICE_PATH := "res://assets/audio/voice/pages/day12_all_mouse_practice_complete.wav"

## Plays the friends entrance without changing the result panel layout.
## Friends is rendered inside FriendsClip, whose bounds match the Area2D
## collision rectangle, so the part outside the display region is clipped.

@export var entrance_duration := 0.85
@export var bottom_padding := 24.0

@onready var _friends_clip: Control = get_node_or_null("Area2D/CollisionShape2D/FriendsClip") as Control
@onready var _friends: Sprite2D = get_node_or_null("Area2D/CollisionShape2D/FriendsClip/Friends") as Sprite2D
@onready var _confetti: GPUParticles2D = get_node_or_null("GPUParticles2D") as GPUParticles2D
@onready var _congratulation_sound: AudioStreamPlayer = get_node_or_null("CongratulationSound") as AudioStreamPlayer
@onready var _time_label: Label = get_node_or_null("ResultPanel/time") as Label
@onready var _score_label: Label = get_node_or_null("ResultPanel/Label2") as Label
@onready var _ok_button: Sprite2D = get_node_or_null("OKbutton") as Sprite2D

var _rest_position := Vector2.ZERO
var _entrance_tween: Tween
var _ok_base_scale := Vector2.ONE
var _ok_is_hovered := false
var _ok_is_pressed := false
var _ok_scale_tween: Tween
var _is_changing_scene := false
var _celebration_started := false
var _elapsed_seconds := 0.0
var _score := 0.0
var _click_player: AudioStreamPlayer


func _ready() -> void:
	_configure_result_voice()
	_setup_ok_button()
	_refresh_result_labels()
	if _friends_clip == null or _friends == null:
		push_error("Result scene is missing FriendsClip or Friends; the friends entrance animation was skipped.")
		return

	_rest_position = _friends.position
	var texture_height := 0.0
	if _friends.texture != null:
		texture_height = float(_friends.texture.get_height())

	# With Sprite2D centered, this places the complete image below the clipped area.
	_friends.position = Vector2(
		_rest_position.x,
		_friends_clip.size.y + texture_height * 0.5 + bottom_padding
	)
	call_deferred("_play_friends_entrance")


func _configure_result_voice() -> void:
	var game_state := get_node_or_null("/root/GameState")
	if game_state == null or int(game_state.get("selected_day")) != 12:
		return
	var page_voice := get_node_or_null("PageVoice") as AudioStreamPlayer
	if page_voice != null:
		page_voice.stream = load(DAY12_COMPLETION_VOICE_PATH) as AudioStream


func _process(_delta: float) -> void:
	if _ok_button == null or _is_changing_scene:
		return

	var hovered := _contains_sprite_point(_ok_button, get_global_mouse_position())
	if hovered == _ok_is_hovered:
		return

	_ok_is_hovered = hovered
	_tween_ok_scale(_ok_base_scale * HOVER_SCALE if _ok_is_hovered else _ok_base_scale)


func _input(event: InputEvent) -> void:
	if _ok_button == null or _is_changing_scene:
		return

	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return

	if mouse_event.pressed:
		if not _contains_sprite_point(_ok_button, get_global_mouse_position()):
			return

		_ok_is_pressed = true
		_tween_ok_scale(_ok_base_scale * PRESSED_SCALE)
		if _click_player != null:
			_click_player.play()
		get_viewport().set_input_as_handled()
		return

	if not _ok_is_pressed:
		return

	_ok_is_pressed = false
	if not _contains_sprite_point(_ok_button, get_global_mouse_position()):
		_tween_ok_scale(_ok_base_scale * HOVER_SCALE if _ok_is_hovered else _ok_base_scale)
		return

	_is_changing_scene = true
	_tween_ok_scale(_ok_base_scale)
	get_viewport().set_input_as_handled()
	await get_tree().create_timer(SCENE_CHANGE_DELAY).timeout
	get_tree().change_scene_to_file(MAIN_SCENE_PATH)


func set_result_data(elapsed_seconds: float, score: float) -> void:
	_elapsed_seconds = maxf(elapsed_seconds, 0.0)
	_score = clampf(score, 80.0, 95.0)
	_refresh_result_labels()


func _refresh_result_labels() -> void:
	if _time_label != null:
		var total_seconds := int(round(_elapsed_seconds))
		var minutes := total_seconds / 60
		var seconds := total_seconds % 60
		_time_label.text = "%d分%02d秒" % [minutes, seconds]
	if _score_label != null:
		_score_label.text = "%.1f" % _score


func _play_friends_entrance() -> void:
	if _friends == null:
		return

	_play_confetti()

	if _entrance_tween != null and _entrance_tween.is_valid():
		_entrance_tween.kill()

	_entrance_tween = create_tween()
	_entrance_tween.set_trans(Tween.TRANS_BACK)
	_entrance_tween.set_ease(Tween.EASE_OUT)
	_entrance_tween.tween_property(_friends, "position", _rest_position, entrance_duration)


func _play_confetti() -> void:
	if _celebration_started:
		return
	_celebration_started = true

	if _confetti != null:
		_confetti.restart()
		_confetti.emitting = true

	if _congratulation_sound != null:
		_congratulation_sound.play()


func _setup_ok_button() -> void:
	if _ok_button == null:
		push_error("Result scene is missing OKbutton.")
		return

	_ok_base_scale = _ok_button.scale
	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)


func _contains_sprite_point(sprite: Sprite2D, global_point: Vector2) -> bool:
	if sprite == null or sprite.texture == null:
		return false

	return sprite.get_rect().has_point(sprite.to_local(global_point))


func _tween_ok_scale(target_scale: Vector2) -> void:
	if _ok_button == null:
		return
	if _ok_scale_tween != null:
		_ok_scale_tween.kill()

	_ok_scale_tween = create_tween()
	_ok_scale_tween.set_trans(Tween.TRANS_QUAD)
	_ok_scale_tween.set_ease(Tween.EASE_OUT)
	_ok_scale_tween.tween_property(_ok_button, "scale", target_scale, TWEEN_DURATION)
