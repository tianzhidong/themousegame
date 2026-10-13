extends Node2D

signal teaching_finished
signal teaching_started

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const HOVER_SCALE := 1.06
const TWEEN_DURATION := 0.1

@export_file("*.ogg", "*.wav", "*.mp3") var voice_path := ""

@onready var _voice_replay_button: Sprite2D = $VoiceReplayButton

var _voice_player: AudioStreamPlayer
var _click_player: AudioStreamPlayer
var _base_scale: Vector2
var _is_hovered := false
var _scale_tween: Tween
var _teaching_is_finished := false
var _practice_is_finished := false


func _ready() -> void:
	_base_scale = _voice_replay_button.scale

	_voice_player = AudioStreamPlayer.new()
	var voice := load(voice_path) as AudioStream
	if voice == null:
		push_error("Failed to load lesson voice: %s" % voice_path)
		_finish_teaching()
	else:
		_voice_player.stream = voice
		_voice_player.finished.connect(_finish_teaching)
	add_child(_voice_player)
	if voice != null:
		_play_teaching_voice()

	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)


func is_teaching_finished() -> bool:
	return _teaching_is_finished


func can_accept_practice_input() -> bool:
	return _teaching_is_finished and not _practice_is_finished and _voice_player != null and not _voice_player.playing


func finish_practice() -> void:
	_practice_is_finished = true


func _play_teaching_voice() -> void:
	if _voice_player.stream == null:
		return
	_teaching_is_finished = false
	teaching_started.emit()
	_voice_player.play()


func _finish_teaching() -> void:
	if _teaching_is_finished:
		return

	_teaching_is_finished = true
	teaching_finished.emit()


func _process(_delta: float) -> void:
	var hovered := _contains_global_point(_voice_replay_button, get_global_mouse_position())
	if hovered == _is_hovered:
		return

	_is_hovered = hovered
	_tween_scale(_base_scale * HOVER_SCALE if _is_hovered else _base_scale)


func _input(event: InputEvent) -> void:
	if _practice_is_finished:
		return
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null:
		return

	if mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	if _contains_global_point(_voice_replay_button, mouse_event.position):
		_click_player.play()
		_play_teaching_voice()
		get_viewport().set_input_as_handled()


func _contains_global_point(sprite: Sprite2D, global_point: Vector2) -> bool:
	if sprite.texture == null:
		return false

	return sprite.get_rect().has_point(sprite.to_local(global_point))


func _tween_scale(target_scale: Vector2) -> void:
	if _scale_tween != null:
		_scale_tween.kill()

	_scale_tween = create_tween()
	_scale_tween.set_trans(Tween.TRANS_QUAD)
	_scale_tween.set_ease(Tween.EASE_OUT)
	_scale_tween.tween_property(_voice_replay_button, "scale", target_scale, TWEEN_DURATION)
