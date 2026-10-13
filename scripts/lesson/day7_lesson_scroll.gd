extends CollisionShape2D

signal first_valid_scroll

@export_range(20.0, 200.0, 10.0) var scroll_step := 90.0
@export_range(0.05, 0.5, 0.01) var scroll_duration := 0.16

@onready var _background: Sprite2D = $Day7Bg
@onready var _lesson: Node = get_parent().get_parent()

var _region: Rect2
var _minimum_y := 0.0
var _maximum_y := 0.0
var _target_y := 0.0
var _scroll_tween: Tween
var _has_scrolled := false


func _ready() -> void:
	var rectangle := shape as RectangleShape2D
	if rectangle == null or _background.texture == null:
		set_process_input(false)
		return

	_region = Rect2(-rectangle.size * 0.5, rectangle.size)
	# Draw an invisible rectangular mask that clips the background child.
	clip_children = CanvasItem.CLIP_CHILDREN_ONLY
	queue_redraw()

	var image_rect := _background.get_rect()
	var first_y := image_rect.position.y * _background.scale.y
	var last_y := image_rect.end.y * _background.scale.y
	var image_top := minf(first_y, last_y)
	var image_bottom := maxf(first_y, last_y)
	_minimum_y = _region.end.y - image_bottom
	_maximum_y = _region.position.y - image_top
	if _minimum_y > _maximum_y:
		_minimum_y = _region.get_center().y - (image_top + image_bottom) * 0.5
		_maximum_y = _minimum_y

	_target_y = clampf(_background.position.y, _minimum_y, _maximum_y)
	_background.position.y = _target_y


func _draw() -> void:
	if _region.has_area():
		draw_rect(_region, Color.WHITE)


func _input(event: InputEvent) -> void:
	if not _lesson.can_accept_practice_input():
		return
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or not mouse_event.pressed or not is_visible_in_tree():
		return
	if not _region.has_point(make_input_local(mouse_event).position):
		return

	var direction := 0.0
	if mouse_event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
		direction = -1.0
	elif mouse_event.button_index == MOUSE_BUTTON_WHEEL_UP:
		direction = 1.0
	else:
		return

	get_viewport().set_input_as_handled()
	var wheel_factor := mouse_event.factor if mouse_event.factor > 0.0 else 1.0
	var next_y := clampf(_target_y + direction * scroll_step * wheel_factor, _minimum_y, _maximum_y)
	if is_equal_approx(next_y, _target_y):
		return

	_target_y = next_y
	if _scroll_tween != null:
		_scroll_tween.kill()
	_scroll_tween = create_tween()
	_scroll_tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	_scroll_tween.tween_property(_background, "position:y", _target_y, scroll_duration)
	if not _has_scrolled:
		_has_scrolled = true
		first_valid_scroll.emit()
