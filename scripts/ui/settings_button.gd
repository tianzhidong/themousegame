extends Sprite2D

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")
const GAMESET_SCENE := preload("res://scenes/ui/gameset.tscn")
const HOVER_SCALE := 1.06
const TWEEN_DURATION := 0.1

var _base_scale: Vector2
var _is_hovered := false
var _scale_tween: Tween
var _click_player: AudioStreamPlayer
var _settings_overlay: CanvasLayer


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
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null:
		return

	if mouse_event.button_index != MOUSE_BUTTON_LEFT or not mouse_event.pressed:
		return

	if _contains_global_point(mouse_event.global_position):
		_click_player.play()
		_toggle_settings_overlay()
		get_viewport().set_input_as_handled()

func _toggle_settings_overlay() -> void:
	if is_instance_valid(_settings_overlay):
		_settings_overlay.queue_free()
		_settings_overlay = null
		return

	var current_scene := get_tree().current_scene
	if current_scene == null:
		return

	_settings_overlay = CanvasLayer.new()
	_settings_overlay.name = "SettingsOverlay"
	_settings_overlay.layer = 100
	current_scene.add_child(_settings_overlay)

	var dim_background := ColorRect.new()
	dim_background.name = "DimBackground"
	dim_background.color = Color(0.02, 0.05, 0.04, 0.42)
	dim_background.mouse_filter = Control.MOUSE_FILTER_STOP
	_settings_overlay.add_child(dim_background)
	dim_background.position = Vector2.ZERO
	dim_background.size = get_viewport().get_visible_rect().size

	var settings_scene := GAMESET_SCENE.instantiate()
	settings_scene.name = "Gameset"
	_settings_overlay.add_child(settings_scene)


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
