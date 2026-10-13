extends Node2D

const DAY_ID := 1
const DAY1_MUSIC := preload("res://assets/audio/voice/day1/day1bgm.mp3")
const RESULT_DELAY_SECONDS := 2.0
const RESULT_SCENE := preload("res://scenes/ui/result.tscn")
const RESULT_OVERLAY_LAYER := 50
const RESULT_OVERLAY_COLOR := Color(0.86, 0.52, 0.24, 0.34)

var _task_manager: Node
var _audio_manager: Node
var _day1_music_player: AudioStreamPlayer
var _result_shown := false
var _result_pending := false
var _level_started_at_msec := 0
var _completion_elapsed_seconds := 0.0
var _random := RandomNumberGenerator.new()


func _ready() -> void:
	_audio_manager = get_node("/root/AudioManager")
	var music := DAY1_MUSIC.duplicate() as AudioStreamMP3
	music.loop = true
	_day1_music_player = AudioStreamPlayer.new()
	_day1_music_player.name = "Day1MusicPlayer"
	_day1_music_player.stream = music
	_day1_music_player.bus = "Music"
	add_child(_day1_music_player)
	_audio_manager.call("SetBackgroundMusicPaused", true)
	_day1_music_player.play()
	_level_started_at_msec = Time.get_ticks_msec()
	_random.randomize()
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager == null:
		push_error("Level1: required TaskManager autoload was not found")
		return

	if _task_manager.has_method("EnsureSelectedDayLoaded"):
		_task_manager.call("EnsureSelectedDayLoaded")

	if _task_manager.has_signal("on_all_tasks_completed"):
		_task_manager.connect(
			"on_all_tasks_completed",
			Callable(self, "_on_all_tasks_completed")
		)
	else:
		push_error("Level1: TaskManager signal 'on_all_tasks_completed' was not found")
		return

	if _task_manager.has_method("IsDayComplete") and bool(_task_manager.call("IsDayComplete")):
		_on_all_tasks_completed(DAY_ID, 0.0)


func _process(_delta: float) -> void:
	if _result_shown or _result_pending or _task_manager == null:
		return
	if _task_manager.has_method("IsDayComplete") and bool(_task_manager.call("IsDayComplete")):
		_on_all_tasks_completed(DAY_ID, 0.0)


func _exit_tree() -> void:
	if is_instance_valid(_day1_music_player):
		_day1_music_player.stop()
	if is_instance_valid(_audio_manager):
		_audio_manager.call("SetBackgroundMusicPaused", false)
	if _task_manager != null and is_instance_valid(_task_manager):
		if _task_manager.has_signal("on_all_tasks_completed") \
			and _task_manager.is_connected("on_all_tasks_completed", Callable(self, "_on_all_tasks_completed")):
			_task_manager.disconnect(
				"on_all_tasks_completed",
				Callable(self, "_on_all_tasks_completed")
			)


func _on_all_tasks_completed(day_id, _elapsed_seconds) -> void:
	if int(day_id) != DAY_ID:
		return
	if _result_shown or _result_pending:
		return

	_completion_elapsed_seconds = (Time.get_ticks_msec() - _level_started_at_msec) / 1000.0
	_result_pending = true
	_show_result_after_delay()


func _show_result_after_delay() -> void:
	await get_tree().create_timer(RESULT_DELAY_SECONDS).timeout
	if not is_inside_tree() or _result_shown:
		return
	_show_result()


func _show_result() -> void:
	if _result_shown or not is_inside_tree():
		return
	if get_node_or_null("ResultOverlayLayer") != null:
		_result_shown = true
		_result_pending = false
		return

	_result_shown = true
	_result_pending = false
	var result_layer := CanvasLayer.new()
	result_layer.name = "ResultOverlayLayer"
	result_layer.layer = RESULT_OVERLAY_LAYER
	add_child(result_layer)

	var warm_overlay := ColorRect.new()
	warm_overlay.name = "WarmOverlay"
	warm_overlay.color = RESULT_OVERLAY_COLOR
	warm_overlay.position = Vector2.ZERO
	warm_overlay.size = get_viewport_rect().size
	warm_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	result_layer.add_child(warm_overlay)

	var result := RESULT_SCENE.instantiate() as Node2D
	if result == null:
		push_error("Level1: failed to instantiate result.tscn")
		return

	result.z_index = 100
	result_layer.add_child(result)
	if result.has_method("set_result_data"):
		result.call(
			"set_result_data",
			_completion_elapsed_seconds,
			_random.randf_range(80.0, 95.0)
		)
