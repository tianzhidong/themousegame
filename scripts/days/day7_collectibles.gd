extends Node2D

const ITEM_SHADOW_SCRIPT := preload("res://scripts/days/day7_item_shadow.gd")
const DAY7_MUSIC := preload("res://assets/audio/voice/day7/day7_bgm.mp3")
const DEFAULT_SOUND := preload("res://assets/audio/voice/通用.ogg")
const ITEM_SOUNDS := {
	"hat": preload("res://assets/audio/voice/day4/cloth_rustle.mp3"),
	"book": preload("res://assets/audio/voice/day6/paper_rustle.mp3"),
	"ball": preload("res://assets/audio/voice/day7/ball.mp3"),
	"doll": preload("res://assets/audio/voice/generic_soft.mp3"),
	"badge": DEFAULT_SOUND,
	"plant": preload("res://assets/audio/voice/day4/leaf_rustle.mp3"),
	"photo": preload("res://assets/audio/voice/day6/paper_rustle.mp3"),
	"light": preload("res://assets/audio/voice/day7/light_switch.ogg")
}

var _items: Array[Sprite2D] = []
var _collected: Dictionary = {}
var _initial_scales: Dictionary = {}
var _collection_tweens: Dictionary = {}
var _task_manager: Node
var _audio_manager: Node
var _day7_music_player: AudioStreamPlayer
var _random := RandomNumberGenerator.new()


func _enter_tree() -> void:
	# Set the day before the taskbar loads, including direct editor scene runs.
	GameState.set_selected_day(7)


func _ready() -> void:
	_random.randomize()
	_audio_manager = get_node("/root/AudioManager")
	var music := DAY7_MUSIC.duplicate() as AudioStreamMP3
	music.loop = true
	_day7_music_player = AudioStreamPlayer.new()
	_day7_music_player.name = "Day7MusicPlayer"
	_day7_music_player.stream = music
	_day7_music_player.bus = "Music"
	add_child(_day7_music_player)
	_audio_manager.call("SetBackgroundMusicPaused", true)
	_day7_music_player.play()
	_task_manager = get_node("/root/TaskManager")
	_task_manager.call("LoadDay", 7)
	_task_manager.connect("on_day_reset", _on_day_reset)

	for child in $Day7Bg/HiddenItems.get_children():
		var item := child as Sprite2D
		if item == null:
			continue
		_items.append(item)
		# Keep the authored furniture positions, varying only size, tilt and hue.
		item.scale *= _random.randf_range(0.95, 1.05)
		var tilt_limit := float(item.get_meta("tilt_limit", 2.0))
		item.rotation_degrees += _random.randf_range(-tilt_limit, tilt_limit)
		item.modulate = Color.from_hsv(_random.randf_range(0.0, 1.0), 0.025, _random.randf_range(0.97, 1.0))
		_initial_scales[item] = item.scale
		var shadow := ITEM_SHADOW_SCRIPT.new()
		shadow.name = "GroundShadow"
		item.add_child(shadow)
		shadow.setup(item)

	get_window().grab_focus.call_deferred()


func _exit_tree() -> void:
	if is_instance_valid(_day7_music_player):
		_day7_music_player.stop()
	if is_instance_valid(_audio_manager):
		_audio_manager.call("SetBackgroundMusicPaused", false)


func _unhandled_input(event: InputEvent) -> void:
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or not mouse_event.pressed or mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return
	if _has_blocking_overlay() or bool(_task_manager.call("IsDayComplete")):
		return
	var point := get_global_mouse_position()
	if _is_over_interface(point):
		return

	# Check frontmost props first. A generous sprite rectangle suits children.
	for index in range(_items.size() - 1, -1, -1):
		var item := _items[index]
		if item in _collected or not item.is_visible_in_tree():
			continue
		if not item.get_rect().grow(6.0).has_point(item.to_local(point)):
			continue
		var report := {
			"operation": "click",
			"object_id": str(item.name),
			"object_type": str(item.get_meta("object_type")),
			"object_color": "",
			"object_tags": ["cottage_decoration"],
			"timestamp": Time.get_ticks_msec()
		}
		if not bool(_task_manager.call("TryHandleInteraction", report)):
			continue

		_collected[item] = true
		var object_type := str(item.get_meta("object_type", ""))
		_audio_manager.call("PlayEffect", ITEM_SOUNDS.get(object_type, DEFAULT_SOUND))
		var tween := create_tween().set_parallel(true)
		_collection_tweens[item] = tween
		tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		tween.tween_property(item, "scale", item.scale * 0.65, 0.18)
		tween.tween_property(item, "modulate:a", 0.0, 0.18)
		tween.chain().tween_callback(item.hide)
		get_viewport().set_input_as_handled()
		return


func _has_blocking_overlay() -> bool:
	return get_node_or_null("SettingsOverlay") != null or get_node_or_null("ResultOverlayLayer") != null


func _is_over_interface(point: Vector2) -> bool:
	for path in ["BackButton", "SettingsButton", "Taskbar/CanvasLayer/Taskbar", "Taskbar/CanvasLayer/Taskbar/Tasklabel"]:
		var sprite := get_node_or_null(path) as Sprite2D
		if sprite != null and sprite.is_visible_in_tree() and sprite.get_rect().has_point(sprite.to_local(point)):
			return true
	return false


func _on_day_reset(day_id: int) -> void:
	if day_id != 7:
		return
	for tween in _collection_tweens.values():
		if tween.is_valid():
			tween.kill()
	_collection_tweens.clear()
	_collected.clear()
	for item in _items:
		item.scale = _initial_scales[item]
		item.modulate.a = 1.0
		item.show()
