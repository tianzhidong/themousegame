extends Sprite2D

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const HOVER_SCALE := 1.06
const TWEEN_DURATION := 0.1
const SCENE_CHANGE_DELAY := 0.12

@export_enum("bear", "rabbit", "cat") var friend_id := "bear"

var _base_scale: Vector2
var _is_hovered := false
var _is_changing_scene := false
var _scale_tween: Tween
var _click_player: AudioStreamPlayer


func _ready() -> void:
	_base_scale = scale

	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)


func _process(_delta: float) -> void:
	var hovered := _contains_global_point(get_global_mouse_position())
	if hovered == _is_hovered:
		return

	_is_hovered = hovered
	_tween_scale(_base_scale * HOVER_SCALE if _is_hovered else _base_scale)


func _unhandled_input(event: InputEvent) -> void:
	if _is_changing_scene:
		return

	var mouse_event := event as InputEventMouseButton
	if mouse_event == null:
		return

	if mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	if _contains_global_point(mouse_event.global_position):
		_is_changing_scene = true
		GameState.set_selected_friend(friend_id)
		_click_player.play()
		get_viewport().set_input_as_handled()

		await get_tree().create_timer(SCENE_CHANGE_DELAY).timeout
		get_tree().change_scene_to_file(GameState.get_selected_start_scene_path())


func _contains_global_point(global_point: Vector2) -> bool:
	if texture == null:
		return false

	return get_rect().has_point(to_local(global_point))


func _tween_scale(target_scale: Vector2) -> void:
	if _scale_tween != null:
		_scale_tween.kill()

	_scale_tween = create_tween()
	_scale_tween.set_trans(Tween.TRANS_QUAD)
	_scale_tween.set_ease(Tween.EASE_OUT)
	_scale_tween.tween_property(self, "scale", target_scale, TWEEN_DURATION)
