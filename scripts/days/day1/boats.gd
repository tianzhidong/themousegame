extends Area2D

const BOAT_SOUND := preload("res://assets/audio/voice/day1/划水声.mp3")
const BOAT_COUNT := 5
const MIN_SPAWN_DISTANCE := 62.0
const BOAT_SPEED := 78.0
const BOAT_DIRECTION_VARIATION := 0.08
const BOAT_SPEED_VARIATION := 14.0
const BOAT_SEPARATION_DISTANCE := 58.0
const DOWNSTREAM_DIRECTION := Vector2(-0.2, 1.0)
const BOAT_TASK_TYPE := "boat"
const BOAT_TASK_TAG := "garden_boat"

var _random := RandomNumberGenerator.new()
var _boats: Array[TextureRect] = []
var _sailed_boats: Dictionary = {}
var _boat_directions: Dictionary = {}
var _boat_speeds: Dictionary = {}
var _task_manager: Node
var _audio_manager: Node


func _ready() -> void:
	_random.randomize()
	_audio_manager = get_node("/root/AudioManager")
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager == null:
		push_error("Boats: required TaskManager autoload was not found")
	elif _task_manager.has_method("EnsureSelectedDayLoaded"):
		_task_manager.call("EnsureSelectedDayLoaded")
	elif _task_manager.has_method("ensure_selected_day_loaded"):
		_task_manager.call("ensure_selected_day_loaded")

	var template := get_node_or_null("CollisionPolygon2D/boat") as TextureRect
	var region := get_node_or_null("CollisionPolygon2D") as CollisionPolygon2D
	if template == null or region == null or region.polygon.is_empty():
		push_error("Boats: boat template or spawn region is missing")
		return

	var occupied_positions: Array[Vector2] = []
	template.visible = false
	for index in range(BOAT_COUNT):
		var boat: TextureRect
		if index == 0:
			boat = template
		else:
			boat = template.duplicate() as TextureRect
			boat.name = "boat_%02d" % (index + 1)
			template.get_parent().add_child(boat)

		boat.visible = true
		boat.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var initial_angle := _random.randf_range(-BOAT_DIRECTION_VARIATION, BOAT_DIRECTION_VARIATION)
		boat.rotation = initial_angle
		boat.set_meta("downstream_direction", DOWNSTREAM_DIRECTION.rotated(initial_angle))
		_boat_speeds[boat] = _random.randf_range(
			BOAT_SPEED - BOAT_SPEED_VARIATION,
			BOAT_SPEED + BOAT_SPEED_VARIATION
		)
		var spawn_position := _random_spawn_position(region, boat, occupied_positions)
		_place_boat_center(boat, spawn_position)
		occupied_positions.append(spawn_position)
		_boats.append(boat)
		_set_boat_metadata(boat, "boat_%02d" % (index + 1))


func _process(_delta: float) -> void:
	if _boats.is_empty():
		return

	_move_sailing_boats(_delta)
	var mouse_position := get_global_mouse_position()
	for boat in _boats:
		if not is_instance_valid(boat) or boat in _sailed_boats:
			continue
		if boat.visible and boat.get_global_rect().has_point(mouse_position):
			if _try_accept_interaction(boat):
				_sailed_boats[boat] = true
				_audio_manager.call("PlayEffect", BOAT_SOUND)
				_start_downstream_motion(boat)


func _move_sailing_boats(delta: float) -> void:
	var region := get_node_or_null("CollisionPolygon2D") as CollisionPolygon2D
	if region == null or region.polygon.is_empty():
		return

	var polygon := region.polygon
	for boat in _boats:
		if not is_instance_valid(boat) or boat not in _sailed_boats:
			continue

		var boat_rect := boat.get_global_rect()
		var boat_half_size: Vector2 = boat_rect.size * 0.5
		var current_center := boat_rect.get_center()
		var direction: Vector2 = _boat_directions.get(boat, DOWNSTREAM_DIRECTION)
		var speed: float = _boat_speeds.get(boat, BOAT_SPEED)
		var step_distance := speed * delta
		var next_direction := _find_sailing_direction(
			region,
			polygon,
			boat,
			current_center,
			direction,
			step_distance,
			boat_half_size
		)
		if next_direction == Vector2.ZERO:
			continue

		var next_center := current_center + next_direction * step_distance
		boat.global_position += next_center - current_center
		_boat_directions[boat] = next_direction

		if boat.get_global_rect().end.y >= get_viewport_rect().size.y:
			boat.queue_free()


func _find_sailing_direction(
	region: CollisionPolygon2D,
	polygon: PackedVector2Array,
	boat: TextureRect,
	current_center: Vector2,
	current_direction: Vector2,
	step_distance: float,
	boat_half_size: Vector2
) -> Vector2:
	var direction_candidates: Array[Vector2] = []
	var angle_offsets := [0.0, -0.12, 0.12, -0.25, 0.25, -0.45, 0.45, -0.7, 0.7]
	for angle_offset in angle_offsets:
		direction_candidates.append(current_direction.rotated(angle_offset).normalized())

	var best_direction := Vector2.ZERO
	var best_score := -INF
	for candidate in direction_candidates:
		var candidate_center := current_center + candidate * step_distance
		if not _boat_fits_in_region(region, polygon, candidate_center, boat_half_size):
			continue
		if _boat_would_overlap(boat, candidate_center, boat_half_size):
			continue

		var downstream_score := candidate.dot(DOWNSTREAM_DIRECTION.normalized())
		var downward_score := candidate.y
		var score := downstream_score * 2.0 + downward_score
		if score > best_score:
			best_score = score
			best_direction = candidate

	if best_direction != Vector2.ZERO:
		return best_direction

	# If boats are temporarily close together, keep searching around the full circle
	# so the boat can slide around an obstacle instead of stopping at the bank.
	for index in range(16):
		var candidate := current_direction.rotated(TAU * float(index) / 16.0).normalized()
		var candidate_center := current_center + candidate * step_distance
		if _boat_fits_in_region(region, polygon, candidate_center, boat_half_size) \
			and not _boat_would_overlap(boat, candidate_center, boat_half_size):
			return candidate

	return Vector2.ZERO


func _boat_would_overlap(
	moving_boat: TextureRect,
	candidate_center: Vector2,
	boat_half_size: Vector2
) -> bool:
	for other_boat in _boats:
		if other_boat == moving_boat or not is_instance_valid(other_boat) or not other_boat.visible:
			continue
		var other_rect := other_boat.get_global_rect()
		var other_center := other_rect.get_center()
		var minimum_distance := maxf(
			maxf(boat_half_size.x, boat_half_size.y),
			maxf(other_rect.size.x, other_rect.size.y) * 0.5
		) * 2.0
		if candidate_center.distance_to(other_center) < maxf(minimum_distance, BOAT_SEPARATION_DISTANCE):
			return true
	return false


func _random_spawn_position(
	region: CollisionPolygon2D,
	boat: TextureRect,
	existing_positions: Array[Vector2]
) -> Vector2:
	var polygon := region.polygon
	var bounds := Rect2(polygon[0], Vector2.ZERO)
	for point in polygon:
		bounds = bounds.expand(point)

	var boat_half_size := boat.get_global_rect().size * 0.5
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
		if not _boat_fits_in_region(region, polygon, global_position, boat_half_size):
			continue
		return global_position

	return best_position


func _boat_fits_in_region(
	region: CollisionPolygon2D,
	polygon: PackedVector2Array,
	global_position: Vector2,
	half_size: Vector2
) -> bool:
	var inset := half_size
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


func _place_boat_center(boat: TextureRect, target_position: Vector2) -> void:
	var current_rect := boat.get_global_rect()
	boat.global_position += target_position - current_rect.get_center()


func _set_boat_metadata(boat: TextureRect, boat_id: String) -> void:
	boat.set_meta("object_id", boat_id)
	boat.set_meta("object_type", BOAT_TASK_TYPE)
	boat.set_meta("object_color", "")
	boat.set_meta("object_tags", PackedStringArray([BOAT_TASK_TAG]))
	boat.set_meta("can_receive_mouse_input", false)


func _try_accept_interaction(boat: TextureRect) -> bool:
	if _task_manager == null:
		return false

	var report := {
		"operation": "move",
		"object_id": str(boat.get_meta("object_id", "")),
		"object_type": BOAT_TASK_TYPE,
		"object_color": "",
		"object_tags": PackedStringArray([BOAT_TASK_TAG]),
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
	boat.set_meta("can_receive_mouse_input", accepted)
	return accepted


func _start_downstream_motion(boat: TextureRect) -> void:
	var direction: Vector2 = DOWNSTREAM_DIRECTION
	var saved_direction = boat.get_meta("downstream_direction", DOWNSTREAM_DIRECTION)
	if saved_direction is Vector2:
		direction = saved_direction
	_boat_directions[boat] = direction.normalized()
