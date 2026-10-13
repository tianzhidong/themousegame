extends Area2D

const BIRD_SOUND := preload("res://assets/audio/voice/day1/小鸟飞.mp3")
const BIRD_COUNT := 5
const MIN_SPAWN_DISTANCE := 120.0
const BIRD_FLIGHT_SPEED := 60.0
const BIRD_FLIGHT_SPEED_MIN := 45.0
const BIRD_FLIGHT_SPEED_MAX := 80.0
const BIRD_FLIGHT_MARGIN := 96.0
const BIRD_TASK_TYPE := "bird"
const BIRD_TASK_TAG := "garden_bird"

var _random := RandomNumberGenerator.new()
var _birds: Array[AnimatedSprite2D] = []
var _triggered_birds: Dictionary = {}
var _task_manager: Node
var _audio_manager: Node


func _ready() -> void:
	_random.randomize()
	_audio_manager = get_node("/root/AudioManager")
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager == null:
		push_error("Birds: required TaskManager autoload was not found")
	elif _task_manager.has_method("EnsureSelectedDayLoaded"):
		_task_manager.call("EnsureSelectedDayLoaded")
	elif _task_manager.has_method("ensure_selected_day_loaded"):
		_task_manager.call("ensure_selected_day_loaded")

	var template := get_node_or_null("CollisionPolygon2D/AnimatedSprite2D") as AnimatedSprite2D
	var region := get_node_or_null("CollisionPolygon2D") as CollisionPolygon2D
	if template == null or region == null or region.polygon.is_empty():
		push_error("Birds: bird template or spawn region is missing")
		return

	var occupied_positions: Array[Vector2] = []
	template.visible = false
	for index in range(BIRD_COUNT):
		var bird: AnimatedSprite2D
		if index == 0:
			bird = template
		else:
			bird = template.duplicate() as AnimatedSprite2D
			bird.name = "bird_%02d" % (index + 1)
			template.get_parent().add_child(bird)

		bird.visible = true
		bird.animation = &"bird"
		bird.frame = 0
		bird.stop()
		var spawn_position := _random_spawn_position(region, bird, occupied_positions)
		_place_bird_center(bird, spawn_position)
		occupied_positions.append(spawn_position)
		_birds.append(bird)
		_set_bird_metadata(bird, "bird_%02d" % (index + 1))
		bird.set_meta(
			"flight_speed",
			_random.randf_range(BIRD_FLIGHT_SPEED_MIN, BIRD_FLIGHT_SPEED_MAX)
		)


func _process(_delta: float) -> void:
	if _birds.is_empty():
		return

	var mouse_position := get_global_mouse_position()
	for bird in _birds:
		if not is_instance_valid(bird) or bird in _triggered_birds:
			continue
		if bird.visible and _contains_bird_point(bird, mouse_position):
			if _try_accept_interaction(bird):
				_triggered_birds[bird] = true
				_audio_manager.call("PlayEffect", BIRD_SOUND)
				bird.animation = &"bird_fly"
				bird.frame = 0
				bird.play()
				_start_flight_to_right(bird)


func _random_spawn_position(
	region: CollisionPolygon2D,
	bird: AnimatedSprite2D,
	existing_positions: Array[Vector2]
) -> Vector2:
	var polygon := region.polygon
	var bounds := Rect2(polygon[0], Vector2.ZERO)
	for point in polygon:
		bounds = bounds.expand(point)

	var bird_texture: Texture2D = bird.sprite_frames.get_frame_texture(bird.animation, bird.frame)
	var bird_half_size: Vector2 = Vector2.ZERO
	if bird_texture != null:
		bird_half_size = bird_texture.get_size() * bird.global_scale.abs() * 0.5
	var best_position := region.to_global(polygon[0])
	var best_distance := -1.0
	for _attempt in range(500):
		var local_position := Vector2(
			_random.randf_range(bounds.position.x, bounds.end.x),
			_random.randf_range(bounds.position.y, bounds.end.y)
		)
		if not Geometry2D.is_point_in_polygon(local_position, polygon):
			continue

		var global_position := region.to_global(local_position)
		var nearest_distance := _nearest_position_distance(global_position, existing_positions)
		if nearest_distance > best_distance:
			best_position = global_position
			best_distance = nearest_distance
		if nearest_distance < MIN_SPAWN_DISTANCE:
			continue
		if not _bird_fits_in_region(region, polygon, global_position, bird_half_size):
			continue
		return global_position

	return best_position


func _bird_fits_in_region(
	region: CollisionPolygon2D,
	polygon: PackedVector2Array,
	global_position: Vector2,
	half_size: Vector2
) -> bool:
	var inset := half_size * 0.8
	for corner in [
		Vector2(-inset.x, -inset.y),
		Vector2(inset.x, -inset.y),
		Vector2(inset.x, inset.y),
		Vector2(-inset.x, inset.y)
	]:
		if not Geometry2D.is_point_in_polygon(region.to_local(global_position + corner), polygon):
			return false
	return true


func _nearest_position_distance(candidate: Vector2, existing_positions: Array[Vector2]) -> float:
	if existing_positions.is_empty():
		return INF

	var nearest_distance := INF
	for existing_position in existing_positions:
		nearest_distance = minf(nearest_distance, candidate.distance_to(existing_position))
	return nearest_distance


func _place_bird_center(bird: AnimatedSprite2D, target_position: Vector2) -> void:
	var texture := bird.sprite_frames.get_frame_texture(bird.animation, bird.frame)
	if texture == null:
		bird.global_position = target_position
		return

	var local_rect := Rect2(-texture.get_size() * 0.5 + bird.offset, texture.get_size())
	if not bird.centered:
		local_rect.position = bird.offset
	var current_center := bird.to_global(local_rect.get_center())
	bird.global_position += target_position - current_center


func _contains_bird_point(bird: AnimatedSprite2D, global_point: Vector2) -> bool:
	var texture := bird.sprite_frames.get_frame_texture(bird.animation, bird.frame)
	if texture == null:
		return false

	var local_rect := Rect2(-texture.get_size() * 0.5 + bird.offset, texture.get_size())
	if not bird.centered:
		local_rect.position = bird.offset
	return local_rect.has_point(bird.to_local(global_point))


func _set_bird_metadata(bird: AnimatedSprite2D, bird_id: String) -> void:
	bird.set_meta("object_id", bird_id)
	bird.set_meta("object_type", BIRD_TASK_TYPE)
	bird.set_meta("object_color", "")
	bird.set_meta("object_tags", PackedStringArray([BIRD_TASK_TAG]))
	bird.set_meta("can_receive_mouse_input", false)


func _try_accept_interaction(bird: AnimatedSprite2D) -> bool:
	if _task_manager == null:
		return false

	var report := {
		"operation": "move",
		"object_id": str(bird.get_meta("object_id", "")),
		"object_type": BIRD_TASK_TYPE,
		"object_color": "",
		"object_tags": PackedStringArray([BIRD_TASK_TAG]),
		"timestamp": Time.get_ticks_msec()
	}
	var can_interact_method := &"CanInteract"
	if not _task_manager.has_method(can_interact_method):
		can_interact_method = &"can_interact"
	if not _task_manager.has_method(can_interact_method):
		return false
	if not bool(_task_manager.call(can_interact_method, report)):
		return false

	var handle_method := &"TryHandleInteraction"
	if not _task_manager.has_method(handle_method):
		handle_method = &"try_handle_interaction"
	if not _task_manager.has_method(handle_method):
		return false
	var accepted := bool(_task_manager.call(handle_method, report))
	bird.set_meta("can_receive_mouse_input", accepted)
	return accepted


func _start_flight_to_right(bird: AnimatedSprite2D) -> void:
	var viewport_size := get_viewport_rect().size
	var target_x := viewport_size.x + BIRD_FLIGHT_MARGIN
	var target_y: float
	var vertical_margin := minf(BIRD_FLIGHT_MARGIN, viewport_size.y * 0.5)
	if viewport_size.y <= vertical_margin * 2.0:
		target_y = viewport_size.y * 0.5
	else:
		target_y = _random.randf_range(vertical_margin, viewport_size.y - vertical_margin)

	var target_position := Vector2(target_x, target_y)
	var distance := bird.global_position.distance_to(target_position)
	var flight_speed: float = float(bird.get_meta("flight_speed", BIRD_FLIGHT_SPEED))
	var duration := maxf(distance / flight_speed, 2.0)
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_SINE)
	tween.set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(bird, "global_position", target_position, duration)
	tween.tween_callback(Callable(bird, "queue_free"))
