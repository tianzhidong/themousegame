extends AnimatedSprite2D

const FLOWER_SOUND := preload("res://assets/audio/ui/object_flower.ogg")
const LEVEL_1_SCENE := "res://scenes/levels/level_1.tscn"

var _audio_player: AudioStreamPlayer
var _is_hovered := false
var _has_triggered := false
var _can_detect_hover := false


func _ready() -> void:
	_audio_player = AudioStreamPlayer.new()
	_audio_player.stream = FLOWER_SOUND
	add_child(_audio_player)

	var lesson := get_parent()
	if lesson != null and lesson.has_signal("teaching_finished"):
		lesson.connect("teaching_finished", _on_teaching_finished)
		if lesson.has_method("is_teaching_finished") and lesson.is_teaching_finished():
			_on_teaching_finished()


func _process(_delta: float) -> void:
	if _has_triggered or not _can_detect_hover:
		return

	var hovered := _contains_mouse()
	if hovered and not _is_hovered:
		_trigger_flower()

	_is_hovered = hovered


func _on_teaching_finished() -> void:
	_can_detect_hover = true


func _contains_mouse() -> bool:
	var texture := sprite_frames.get_frame_texture(animation, frame)
	if texture == null:
		return false

	var texture_size := texture.get_size()
	var local_position := -texture_size * 0.5 + offset if centered else offset
	return Rect2(local_position, texture_size).has_point(
		to_local(get_global_mouse_position())
	)


func _trigger_flower() -> void:
	_has_triggered = true
	animation = &"blue_flower_open"
	play()
	_audio_player.play()

	while is_playing() or _audio_player.playing:
		await get_tree().process_frame

	get_tree().change_scene_to_file(LEVEL_1_SCENE)
