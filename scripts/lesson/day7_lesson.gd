extends "res://scripts/ui/lesson.gd"

const SUCCESS_SOUND := preload("res://assets/audio/ui/target_success.ogg")
const LEVEL_7_SCENE := "res://scenes/levels/level_7.tscn"

var _success_player: AudioStreamPlayer
var _is_starting := false


func _ready() -> void:
	super._ready()
	_success_player = AudioStreamPlayer.new()
	_success_player.stream = SUCCESS_SOUND
	add_child(_success_player)
	$Area2D/CollisionShape2D.first_valid_scroll.connect(_on_first_valid_scroll)

	# Wheel events require window focus on Windows, including editor runs.
	get_window().mouse_entered.connect(_focus_game_window)
	_focus_game_window.call_deferred()


func _focus_game_window() -> void:
	if not get_window().has_focus():
		get_window().grab_focus()


func _on_first_valid_scroll() -> void:
	if _is_starting or not can_accept_practice_input():
		return
	_is_starting = true
	finish_practice()
	await get_tree().create_timer(2.0).timeout
	_voice_player.stop()
	_success_player.play()
	await _success_player.finished
	get_node("/root/GameState").call("set_selected_day", 7)
	get_tree().change_scene_to_file(LEVEL_7_SCENE)


func _input(event: InputEvent) -> void:
	# Once practice succeeds, replaying narration must not overlap the cue.
	if not _is_starting:
		super._input(event)
