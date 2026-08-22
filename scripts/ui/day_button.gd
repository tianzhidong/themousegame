extends Button

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const STORIES_SCENE := "res://scenes/stories/stories.tscn"
const HOVER_SCALE := 1.06
const TWEEN_DURATION := 0.1
const SCENE_CHANGE_DELAY := 0.12

@export_range(1, 15, 1) var day_index := 1

var _base_scale: Vector2
var _is_changing_scene := false
var _scale_tween: Tween
var _click_player: AudioStreamPlayer


func _ready() -> void:
	_base_scale = scale
	resized.connect(_update_pivot_offset)
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)
	button_down.connect(_play_click_sound)
	pressed.connect(_on_pressed)
	_update_pivot_offset()

	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)


func _update_pivot_offset() -> void:
	pivot_offset = size * 0.5


func _on_mouse_entered() -> void:
	_tween_scale(_base_scale * HOVER_SCALE)


func _on_mouse_exited() -> void:
	_tween_scale(_base_scale)


func _play_click_sound() -> void:
	_click_player.play()


func _on_pressed() -> void:
	if _is_changing_scene:
		return

	_is_changing_scene = true
	GameState.set_selected_day(day_index)
	await get_tree().create_timer(SCENE_CHANGE_DELAY).timeout
	get_tree().change_scene_to_file(STORIES_SCENE)


func _tween_scale(target_scale: Vector2) -> void:
	if _scale_tween != null:
		_scale_tween.kill()

	_scale_tween = create_tween()
	_scale_tween.set_trans(Tween.TRANS_QUAD)
	_scale_tween.set_ease(Tween.EASE_OUT)
	_scale_tween.tween_property(self, "scale", target_scale, TWEEN_DURATION)
