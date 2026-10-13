extends Area2D

const APPLE_COUNT := 5
const MIN_SPAWN_DISTANCE := 78.0
const MIN_TARGET_DISTANCE := 16.0
const APPLE_SCALE_MIN := 0.92
const APPLE_SCALE_MAX := 1.08
const APPLE_ROTATION_LIMIT := 0.24
const FLIGHT_SPEED := 720.0
const BLANKET_FALLBACK_GRID_SIZE := 12
const APPLE_TASK_TYPE := "apple"
const APPLE_TASK_TAG := "forest_apple"
const BLANKET_ITEM_GROUP := "day2_blanket_items"
const PICK_SOUND := preload("res://assets/audio/voice/day2/摘果子.mp3")
const ITEM_SHADOW_SCRIPT := preload("res://scripts/days/day2/item_shadow.gd")

var _random := RandomNumberGenerator.new()
var _apples: Array[TextureRect] = []
var _collected_apples: Dictionary = {}
var _task_manager: Node
var _audio_manager: Node


func _ready() -> void:
	_random.randomize()
	_audio_manager = get_node("/root/AudioManager")
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager == null:
		push_error("Apples: required TaskManager autoload was not found")
	elif _task_manager.has_method("EnsureSelectedDayLoaded"):
		_task_manager.call("EnsureSelectedDayLoaded")
	elif _task_manager.has_method("ensure_selected_day_loaded"):
		_task_manager.call("ensure_selected_day_loaded")

	var template := get_node_or_null("CollisionPolygon2D/TextureRect") as TextureRect
	var spawn_region := get_node_or_null("CollisionPolygon2D") as CollisionPolygon2D
	if template == null or spawn_region == null or spawn_region.polygon.is_empty():
		push_error("Apples: apple template or spawn region is missing")
		return

	var occupied_positions: Array[Vector2] = []
	var base_scale := template.scale
	template.visible = false
	for index in range(APPLE_COUNT):
		var apple: TextureRect
		if index == 0:
			apple = template
		else:
			apple = template.duplicate() as TextureRect
			apple.name = "apple_%02d" % (index + 1)
			template.get_parent().add_child(apple)

		apple.visible = true
		apple.mouse_filter = Control.MOUSE_FILTER_IGNORE
		apple.scale = base_scale * _random.randf_range(APPLE_SCALE_MIN, APPLE_SCALE_MAX)
		apple.pivot_offset = apple.size * 0.5
		apple.rotation = _random.randf_range(-APPLE_ROTATION_LIMIT, APPLE_ROTATION_LIMIT)
		var spawn_position := _random_spawn_position(spawn_region, apple, occupied_positions)
		_place_apple_center(apple, spawn_position)
		occupied_positions.append(spawn_position)
		_apples.append(apple)
		_set_apple_metadata(apple, "apple_%02d" % (index + 1))

	for apple in _apples:
		_add_item_shadow(apple)


func _unhandled_input(event: InputEvent) -> void:
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or not mouse_event.pressed or mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return

	var mouse_position := get_global_mouse_position()
	for apple in _apples:
		if not is_instance_valid(apple) or apple in _collected_apples or not apple.visible:
			continue
		if not apple.get_global_rect().has_point(mouse_position):
			continue
		if _try_accept_interaction(apple):
			_audio_manager.call("PlayEffect", PICK_SOUND)
			_collected_apples[apple] = true
			_fly_to_blanket(apple)
			get_viewport().set_input_as_handled()
			return


func _random_spawn_position(
	region: CollisionPolygon2D,
	apple: TextureRect,
	existing_positions: Array[Vector2]
) -> Vector2:
	var polygon := region.polygon
	var bounds := Rect2(polygon[0], Vector2.ZERO)
	for point in polygon:
		bounds = bounds.expand(point)

	var apple_half_size: Vector2 = apple.get_global_rect().size * 0.5
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
		if not _apple_fits_in_region(region, polygon, global_position, apple_half_size):
			continue
		return global_position

	return best_position


func _apple_fits_in_region(
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


func _place_apple_center(apple: TextureRect, target_position: Vector2) -> void:
	var current_rect := apple.get_global_rect()
	apple.global_position += target_position - current_rect.get_center()


func _set_apple_metadata(apple: TextureRect, apple_id: String) -> void:
	apple.set_meta("object_id", apple_id)
	apple.set_meta("object_type", APPLE_TASK_TYPE)
	apple.set_meta("object_color", "")
	apple.set_meta("object_tags", PackedStringArray([APPLE_TASK_TAG]))
	apple.set_meta("can_receive_mouse_input", false)


func _add_item_shadow(item: TextureRect) -> void:
	var shadow: ItemShadow = ITEM_SHADOW_SCRIPT.new()
	shadow.z_index = -1
	item.add_child(shadow)
	shadow.setup(item.size)


func _try_accept_interaction(apple: TextureRect) -> bool:
	if _task_manager == null:
		return false

	var report := {
		"operation": "click",
		"object_id": str(apple.get_meta("object_id", "")),
		"object_type": APPLE_TASK_TYPE,
		"object_color": "",
		"object_tags": PackedStringArray([APPLE_TASK_TAG]),
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
	apple.set_meta("can_receive_mouse_input", accepted)
	return accepted


func _fly_to_blanket(apple: TextureRect) -> void:
	var target_region := get_node_or_null("../blanket/CollisionPolygon2D") as CollisionPolygon2D
	if target_region == null or target_region.polygon.is_empty():
		push_error("Apples: blanket target region is missing")
		return

	var target_corner_offsets := _get_item_global_corner_offsets(apple)
	var occupied_targets := _get_blanket_occupied_targets(apple)
	var target_center := _random_blanket_position(target_region, target_corner_offsets, occupied_targets)
	apple.set_meta("blanket_target_center", target_center)
	apple.add_to_group(BLANKET_ITEM_GROUP)

	var current_center := _get_item_visual_center(apple)
	var target_position := apple.global_position + target_center - current_center
	var distance := current_center.distance_to(target_center)
	var duration := maxf(distance / FLIGHT_SPEED, 0.35)
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_QUAD)
	tween.set_ease(Tween.EASE_IN)
	tween.set_parallel(true)
	tween.tween_property(apple, "global_position", target_position, duration)
	tween.set_parallel(false)


func _get_blanket_occupied_targets(current_item: TextureRect) -> Array[Vector2]:
	var occupied_targets: Array[Vector2] = []
	for node in get_tree().get_nodes_in_group(BLANKET_ITEM_GROUP):
		var item := node as TextureRect
		if item == null or item == current_item or not is_instance_valid(item):
			continue
		if item.has_meta("blanket_target_center"):
			var target = item.get_meta("blanket_target_center")
			if target is Vector2:
				occupied_targets.append(target)
	return occupied_targets


func _random_blanket_position(
	region: CollisionPolygon2D,
	corner_offsets: Array[Vector2],
	existing_positions: Array[Vector2]
) -> Vector2:
	var polygon := region.polygon
	var bounds := Rect2(polygon[0], Vector2.ZERO)
	for point in polygon:
		bounds = bounds.expand(point)

	var spaced_positions: Array[Vector2] = []
	var valid_positions: Array[Vector2] = []
	for _attempt in range(1200):
		var local_position := Vector2(
			_random.randf_range(bounds.position.x, bounds.end.x),
			_random.randf_range(bounds.position.y, bounds.end.y)
		)
		if not Geometry2D.is_point_in_polygon(local_position, polygon):
			continue

		var global_position := region.to_global(local_position)
		if not _item_corners_fit_in_region(region, polygon, global_position, corner_offsets):
			continue

		valid_positions.append(global_position)
		if _nearest_position_distance(global_position, existing_positions) >= MIN_TARGET_DISTANCE:
			spaced_positions.append(global_position)

	if not spaced_positions.is_empty():
		return spaced_positions[_random.randi_range(0, spaced_positions.size() - 1)]
	if not valid_positions.is_empty():
		return valid_positions[_random.randi_range(0, valid_positions.size() - 1)]
	return _find_fitting_blanket_position(region, polygon, bounds, corner_offsets)


func _find_fitting_blanket_position(
	region: CollisionPolygon2D,
	polygon: PackedVector2Array,
	bounds: Rect2,
	corner_offsets: Array[Vector2]
) -> Vector2:
	for y_index in range(BLANKET_FALLBACK_GRID_SIZE):
		for x_index in range(BLANKET_FALLBACK_GRID_SIZE):
			var local_position := Vector2(
				lerpf(bounds.position.x, bounds.end.x, (float(x_index) + 0.5) / float(BLANKET_FALLBACK_GRID_SIZE)),
				lerpf(bounds.position.y, bounds.end.y, (float(y_index) + 0.5) / float(BLANKET_FALLBACK_GRID_SIZE))
			)
			if not Geometry2D.is_point_in_polygon(local_position, polygon):
				continue

			var global_position := region.to_global(local_position)
			if _item_corners_fit_in_region(region, polygon, global_position, corner_offsets):
				return global_position

	push_warning("Apples: no fully contained blanket position was found for this item")
	return _polygon_global_center(region, polygon)


func _item_corners_fit_in_region(
	region: CollisionPolygon2D,
	polygon: PackedVector2Array,
	global_center: Vector2,
	corner_offsets: Array[Vector2]
) -> bool:
	for corner_offset in corner_offsets:
		if not Geometry2D.is_point_in_polygon(region.to_local(global_center + corner_offset), polygon):
			return false
	return true


func _get_item_global_corner_offsets(item: TextureRect) -> Array[Vector2]:
	var corners := _get_item_global_corners(item)
	var center := _get_points_center(corners)
	var offsets: Array[Vector2] = []
	for corner in corners:
		offsets.append(corner - center)
	return offsets


func _get_item_visual_center(item: TextureRect) -> Vector2:
	return _get_points_center(_get_item_global_corners(item))


func _get_item_global_corners(item: TextureRect) -> Array[Vector2]:
	var transform := item.get_global_transform()
	var corners: Array[Vector2] = []
	corners.append(transform * Vector2.ZERO)
	corners.append(transform * Vector2(item.size.x, 0.0))
	corners.append(transform * item.size)
	corners.append(transform * Vector2(0.0, item.size.y))
	return corners


func _get_points_center(points: Array[Vector2]) -> Vector2:
	var center := Vector2.ZERO
	for point in points:
		center += point
	return center / float(points.size())


func _polygon_global_center(region: CollisionPolygon2D, polygon: PackedVector2Array) -> Vector2:
	var local_center := Vector2.ZERO
	for point in polygon:
		local_center += point
	return region.to_global(local_center / float(polygon.size()))
