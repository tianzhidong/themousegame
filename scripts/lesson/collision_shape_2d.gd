extends CollisionShape2D

const FLOWER_SOUND := preload("res://assets/audio/ui/object_flower.ogg")
const LEVEL_2_SCENE := "res://scenes/levels/level_2.tscn"

var _area: Area2D
var _flower: AnimatedSprite2D
var _audio_player: AudioStreamPlayer
var _has_triggered := false


func _ready() -> void:
	_area = get_parent() as Area2D
	_flower = get_node("AnimatedSprite2D")
	_flower.sprite_frames.set_animation_loop(&"red_flower_open", false)

	_audio_player = AudioStreamPlayer.new()
	_audio_player.stream = FLOWER_SOUND
	add_child(_audio_player)

	_area.input_pickable = true
	_area.input_event.connect(_on_area_input_event)


func _on_area_input_event(_viewport: Node, event: InputEvent, _shape_idx: int) -> void:
	if _has_triggered:
		return

	var mouse_event := event as InputEventMouseButton
	if mouse_event == null:
		return

	if mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	_trigger_flower()
	get_viewport().set_input_as_handled()


func _trigger_flower() -> void:
	_has_triggered = true
	_flower.animation = &"red_flower_open"
	_flower.play()
	_audio_player.play()

	while _flower.is_playing() or _audio_player.playing:
		await get_tree().process_frame

	get_tree().change_scene_to_file(LEVEL_2_SCENE)
