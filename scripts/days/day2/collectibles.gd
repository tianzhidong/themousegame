extends Node2D

const ITEM_COUNT := 5
const MIN_SPAWN_DISTANCE := 64.0
const MIN_TARGET_DISTANCE := 16.0
const ITEM_SCALE_MIN := 0.9
const ITEM_SCALE_MAX := 1.1
const ITEM_ROTATION_LIMIT := 0.24
const EGG_ROTATION_LIMIT := 0.85
const FLIGHT_SPEED := 720.0
const BLANKET_FALLBACK_GRID_SIZE := 12
const BLANKET_ITEM_GROUP := "day2_blanket_items"
const DAY2_MUSIC := preload("res://assets/audio/voice/day2/day2bgm.mp3")
const ITEM_SOUNDS := {
	"pear": preload("res://assets/audio/voice/day2/摘果子.mp3"),
	"orange": preload("res://assets/audio/voice/day2/摘果子.mp3"),
	"cherry": preload("res://assets/audio/voice/day2/摘果子.mp3"),
	"mushroom": preload("res://assets/audio/voice/day2/拔蘑菇.mp3"),
	"flower": preload("res://assets/audio/voice/day2/摘花.mp3"),
	"egg": preload("res://assets/audio/voice/day2/捡鸡蛋.mp3"),
	"feather": preload("res://assets/audio/voice/day2/捡羽毛.mp3")
}
const ITEM_SHADOW_SCRIPT := preload("res://scripts/days/day2/item_shadow.gd")

var _random := RandomNumberGenerator.new()
var _items: Array[TextureRect] = []
var _collected_items: Dictionary = {}
var _occupied_positions: Array[Vector2] = []
var _task_manager: Node
var _audio_manager: Node
var _day2_music_player: AudioStreamPlayer


func _ready() -> void:
	_random.randomize()
	_audio_manager = get_node("/root/AudioManager")
	var music := DAY2_MUSIC.duplicate() as AudioStreamMP3
	music.loop = true
	_day2_music_player = AudioStreamPlayer.new()
	_day2_music_player.name = "Day2MusicPlayer"
	_day2_music_player.stream = music
	_day2_music_player.bus = "Music"
	add_child(_day2_music_player)
	_audio_manager.call("SetBackgroundMusicPaused", true)
	_day2_music_player.play()
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager == null:
		push_error("Level2 collectibles: required TaskManager autoload was not found")
	elif _task_manager.has_method("EnsureSelectedDayLoaded"):
		_task_manager.call("EnsureSelectedDayLoaded")
	elif _task_manager.has_method("ensure_selected_day_loaded"):
		_task_manager.call("ensure_selected_day_loaded")

	_create_single_region_group(
		"pear",
		"pear/CollisionPolygon2D",
		"pear/CollisionPolygon2D/TextureRect",
		"pear",
		"forest_pear"
	)
	_create_multi_region_group(
		"berry",
		"berry/TextureRect",
		"cherry",
		"forest_cherry"
	)
	_create_single_region_group(
		"mushroom",
		"mushroom/CollisionPolygon2D",
		"mushroom/CollisionPolygon2D/TextureRect",
		"mushroom",
		"forest_mushroom"
	)
	_create_single_region_group(
		"flower",
		"mushroom/CollisionPolygon2D",
		"mushroom/CollisionPolygon2D/TextureRect2",
		"flower",
		"forest_flower"
	)
	_create_single_region_group(
		"orange",
		"orange/2d",
		"orange/2d/TextureRect",
		"orange",
		"forest_orange"
	)
	_create_single_region_group(
		"egg",
		"mushroom/CollisionPolygon2D",
		"mushroom/CollisionPolygon2D/egg",
		"egg",
		"forest_egg"
	)
	_create_single_region_group(
		"feather",
		"mushroom/CollisionPolygon2D",
		"mushroom/CollisionPolygon2D/feather",
		"feather",
		"forest_feather"
	)

	for item in _items:
		_add_item_shadow(item)


func _exit_tree() -> void:
	if is_instance_valid(_day2_music_player):
		_day2_music_player.stop()
	if is_instance_valid(_audio_manager):
		_audio_manager.call("SetBackgroundMusicPaused", false)


func _unhandled_input(event: InputEvent) -> void:
	var mouse_event := event as InputEventMouseButton
	if mouse_event == null or not mouse_event.pressed or mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return

	var mouse_position := get_global_mouse_position()
	for item in _items:
		if not is_instance_valid(item) or item in _collected_items or not item.visible:
			continue
		if not item.get_global_rect().has_point(mouse_position):
			continue
		if _try_accept_interaction(item):
			var object_type := str(item.get_meta("object_type", ""))
			_audio_manager.call("PlayEffect", ITEM_SOUNDS[object_type])
			_collected_items[item] = true
			_fly_to_blanket(item)
			get_viewport().set_input_as_handled()
			return


func _create_single_region_group(
	item_name: String,
	region_path: NodePath,
	template_path: NodePath,
	object_type: String,
	object_tag: String
) -> void:
	var region := get_node_or_null(region_path) as CollisionPolygon2D
	var template := get_node_or_null(template_path) as TextureRect
	if region == null or template == null or region.polygon.is_empty():
		push_error("Level2 collectibles: missing %s template or region" % item_name)
		return

	_create_items(
		item_name,
		[region],
		template,
		ITEM_COUNT,
		object_type,
		object_tag
	)


func _create_multi_region_group(
	item_name: String,
	template_path: NodePath,
	object_type: String,
	object_tag: String
) -> void:
	var group := get_node_or_null(item_name) as Node
	var template := get_node_or_null(template_path) as TextureRect
	if group == null or template == null:
		push_error("Level2 collectibles: missing %s template or group" % item_name)
		return

	var regions: Array[CollisionPolygon2D] = []
	for child in group.get_children():
		var region := child as CollisionPolygon2D
		if region != null and not region.polygon.is_empty():
			regions.append(region)
	if regions.is_empty():
		push_error("Level2 collectibles: no regions found for %s" % item_name)
		return

	_create_items(item_name, regions, template, ITEM_COUNT, object_type, object_tag)


func _create_items(
	item_name: String,
	regions: Array[CollisionPolygon2D],
	template: TextureRect,
	item_count: int,
	object_type: String,
	object_tag: String
) -> void:
	var base_scale := template.scale
	var region_order := regions.duplicate()
	region_order.shuffle()
	template.visible = false
	for index in range(item_count):
		var item: TextureRect
		if index == 0:
			item = template
		else:
			item = template.duplicate() as TextureRect
			item.name = "%s_%02d" % [item_name, index + 1]
			template.get_parent().add_child(item)

		item.visible = true
		item.mouse_filter = Control.MOUSE_FILTER_IGNORE
		item.scale = base_scale * _random.randf_range(ITEM_SCALE_MIN, ITEM_SCALE_MAX)
		item.pivot_offset = item.size * 0.5
		var rotation_limit := _get_item_rotation_limit(item_name)
		item.rotation = _random.randf_range(-rotation_limit, rotation_limit)

		var region: CollisionPolygon2D
		if index < region_order.size():
			region = region_order[index]
		else:
			region = regions[_random.randi_range(0, regions.size() - 1)]
		var spawn_position := _random_spawn_position(region, item, _occupied_positions)
		_place_item_center(item, spawn_position)
		_occupied_positions.append(spawn_position)
		_items.append(item)
		_set_item_metadata(item, "%s_%02d" % [item_name, index + 1], object_type, object_tag)


func _get_item_rotation_limit(item_name: String) -> float:
	if item_name == "egg":
		return EGG_ROTATION_LIMIT
	return ITEM_ROTATION_LIMIT


func _random_spawn_position(
	region: CollisionPolygon2D,
	item: TextureRect,
	existing_positions: Array[Vector2]
) -> Vector2:
	var polygon := region.polygon
	var bounds := Rect2(polygon[0], Vector2.ZERO)
	for point in polygon:
		bounds = bounds.expand(point)

	var item_half_size: Vector2 = item.get_global_rect().size * 0.5
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
		if _item_fits_in_region(region, polygon, global_position, item_half_size):
			return global_position

	return best_position


func _item_fits_in_region(
	region: CollisionPolygon2D,
	polygon: PackedVector2Array,
	global_position: Vector2,
	half_size: Vector2
) -> bool:
	for corner in [
		Vector2(-half_size.x, -half_size.y),
		Vector2(half_size.x, -half_size.y),
		Vector2(half_size.x, half_size.y),
		Vector2(-half_size.x, half_size.y)
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


func _place_item_center(item: TextureRect, target_position: Vector2) -> void:
	var current_rect := item.get_global_rect()
	item.global_position += target_position - current_rect.get_center()


func _set_item_metadata(item: TextureRect, item_id: String, object_type: String, object_tag: String) -> void:
	item.set_meta("object_id", item_id)
	item.set_meta("object_type", object_type)
	item.set_meta("object_color", "")
	item.set_meta("object_tags", PackedStringArray([object_tag]))
	item.set_meta("can_receive_mouse_input", false)


func _add_item_shadow(item: TextureRect) -> void:
	var shadow: ItemShadow = ITEM_SHADOW_SCRIPT.new()
	shadow.z_index = -1
	item.add_child(shadow)
	shadow.setup(item.size)


func _try_accept_interaction(item: TextureRect) -> bool:
	if _task_manager == null:
		return false

	var report := {
		"operation": "click",
		"object_id": str(item.get_meta("object_id", "")),
		"object_type": str(item.get_meta("object_type", "")),
		"object_color": str(item.get_meta("object_color", "")),
		"object_tags": item.get_meta("object_tags", PackedStringArray()),
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
	item.set_meta("can_receive_mouse_input", accepted)
	return accepted


func _fly_to_blanket(item: TextureRect) -> void:
	var target_region := get_node_or_null("blanket/CollisionPolygon2D") as CollisionPolygon2D
	if target_region == null or target_region.polygon.is_empty():
		push_error("Level2 collectibles: blanket target region is missing")
		return

	var target_corner_offsets := _get_item_global_corner_offsets(item)
	var occupied_targets := _get_blanket_occupied_targets(item)
	var target_center := _random_blanket_position(target_region, target_corner_offsets, occupied_targets)
	item.set_meta("blanket_target_center", target_center)
	item.add_to_group(BLANKET_ITEM_GROUP)

	var current_center := _get_item_visual_center(item)
	var target_position := item.global_position + target_center - current_center
	var distance := current_center.distance_to(target_center)
	var duration := maxf(distance / FLIGHT_SPEED, 0.35)
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_QUAD)
	tween.set_ease(Tween.EASE_IN_OUT)
	tween.set_parallel(true)
	tween.tween_property(item, "global_position", target_position, duration)
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

	push_warning("Level2 collectibles: no fully contained blanket position was found for this item")
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
