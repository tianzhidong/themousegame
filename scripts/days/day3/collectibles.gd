extends Node2D

const DAY_CONFIG_PATH := "res://data/days/day_03.json"
const DAY3_MUSIC := preload("res://assets/audio/voice/day3/day3bgm.mp3")
const DEFAULT_SOUND := preload("res://assets/audio/voice/通用.ogg")
const ITEM_SOUNDS := {
	"fish": preload("res://assets/audio/voice/day3/水花.mp3"),
	"frog": preload("res://assets/audio/voice/day3/青蛙.mp3"),
	"shell": preload("res://assets/audio/voice/day3/贝壳.mp3"),
	"stone": preload("res://assets/audio/voice/day3/石头.mp3"),
	"lotus": preload("res://assets/audio/voice/day3/泡泡.mp3"),
	"turtle": preload("res://assets/audio/voice/day3/树叶.mp3"),
	"firefly": preload("res://assets/audio/voice/day3/闪光.mp3")
}
const DEFAULT_ITEM_COUNT := 5
const MIN_SPAWN_DISTANCE := 58.0
const ITEM_SIZE_MULTIPLIER := 1.25
const ITEM_FAR_SCALE := 0.72
const ITEM_NEAR_SCALE := 1.16
const PERSPECTIVE_SCALE_JITTER := 0.06
const ITEM_ROTATION_LIMIT := 0.22
const FIREFLY_ROTATION_LIMIT := 0.45
const FIREFLY_FAR_SCALE := 0.62
const FIREFLY_NEAR_SCALE := 1.08
const FIREFLY_MOVE_SPEED_MIN := 18.0
const FIREFLY_MOVE_SPEED_MAX := 34.0
const FIREFLY_MIN_TRAVEL_DISTANCE := 90.0
const FIREFLY_TARGET_REACHED_DISTANCE := 8.0
const FIREFLY_PAUSE_MIN := 0.25
const FIREFLY_PAUSE_MAX := 0.85
const SPAWN_ATTEMPTS_PER_REGION := 180
const MIN_REGION_AREA := 1.0

@export_group("Fish Swimming")
@export_range(0.0, 100.0, 1.0) var fish_speed_min := 12.0
@export_range(0.0, 100.0, 1.0) var fish_speed_max := 24.0

const ITEM_GROUPS := [
	{
		"template_path": "pond/CollisionPolygon2D/fish",
		"region_paths": ["pond/CollisionPolygon2D"],
		"object_type": "fish",
		"object_color": "golden",
		"object_tag": "garden_fish"
	},
	{
		"template_path": "land/forg",
		"region_parent_path": "land",
		"object_type": "frog",
		"object_color": "green",
		"object_tag": "garden_frog"
	},
	{
		"template_path": "land/shell",
		"region_parent_path": "land",
		"object_type": "shell",
		"object_color": "",
		"object_tag": "garden_shell"
	},
	{
		"template_path": "land/stone",
		"region_parent_path": "land",
		"object_type": "stone",
		"object_color": "",
		"object_tag": "garden_stone"
	},
	{
		"template_path": "pond/CollisionPolygon2D/flower2",
		"region_paths": ["pond/CollisionPolygon2D"],
		"object_type": "lotus",
		"object_color": "",
		"object_tag": "garden_lotus",
		"rotation_limit": 0.0
	},
	{
		"template_path": "land/turtle",
		"region_parent_path": "land",
		"object_type": "turtle",
		"object_color": "",
		"object_tag": "garden_turtle"
	},
	{
		"template_path": "land/snall",
		"region_parent_path": "land",
		"object_type": "snail",
		"object_color": "",
		"object_tag": "garden_snail"
	},
	{
		"template_path": "sky/CollisionShape2D/firefly",
		"region_paths": ["sky/CollisionShape2D"],
		"object_type": "firefly",
		"object_color": "",
		"object_tag": "garden_firefly",
		"rotation_limit": FIREFLY_ROTATION_LIMIT,
		"far_scale": FIREFLY_FAR_SCALE,
		"near_scale": FIREFLY_NEAR_SCALE
	}
]

var _random := RandomNumberGenerator.new()
var _items: Array[TextureRect] = []
var _collected_items: Dictionary = {}
var _occupied_positions: Array[Vector2] = []
var _spawn_region_counts: Dictionary = {}
var _target_counts: Dictionary = {}
var _fish_motion: Dictionary = {}
var _firefly_regions: Dictionary = {}
var _firefly_base_scales: Dictionary = {}
var _firefly_far_scales: Dictionary = {}
var _firefly_near_scales: Dictionary = {}
var _firefly_scale_jitters: Dictionary = {}
var _firefly_targets: Dictionary = {}
var _firefly_speeds: Dictionary = {}
var _firefly_pause_timers: Dictionary = {}
var _task_manager: Node
var _audio_manager: Node
var _day3_music_player: AudioStreamPlayer


func _ready() -> void:
	_random.randomize()
	_audio_manager = get_node("/root/AudioManager")
	var music := DAY3_MUSIC.duplicate() as AudioStreamMP3
	music.loop = true
	_day3_music_player = AudioStreamPlayer.new()
	_day3_music_player.name = "Day3MusicPlayer"
	_day3_music_player.stream = music
	_day3_music_player.bus = "Music"
	add_child(_day3_music_player)
	_audio_manager.call("SetBackgroundMusicPaused", true)
	_day3_music_player.play()
	_spawn_region_counts.clear()
	_load_target_counts()
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager == null:
		push_error("Level3 collectibles: required TaskManager autoload was not found")
	elif _task_manager.has_method("EnsureSelectedDayLoaded"):
		_task_manager.call("EnsureSelectedDayLoaded")
	elif _task_manager.has_method("ensure_selected_day_loaded"):
		_task_manager.call("ensure_selected_day_loaded")

	for item_group in ITEM_GROUPS:
		_create_item_group(item_group)


func _exit_tree() -> void:
	if is_instance_valid(_day3_music_player):
		_day3_music_player.stop()
	if is_instance_valid(_audio_manager):
		_audio_manager.call("SetBackgroundMusicPaused", false)


func _process(delta: float) -> void:
	_move_fish(delta)
	_move_fireflies(delta)


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
			_audio_manager.call("PlayEffect", ITEM_SOUNDS.get(object_type, DEFAULT_SOUND))
			_collected_items[item] = true
			item.hide()
			get_viewport().set_input_as_handled()
			return


func _load_target_counts() -> void:
	_target_counts.clear()
	var file := FileAccess.open(DAY_CONFIG_PATH, FileAccess.READ)
	if file == null:
		return

	var parsed = JSON.parse_string(file.get_as_text())
	if not (parsed is Dictionary):
		return

	var tasks: Array = parsed.get("tasks", [])
	for task in tasks:
		if not (task is Dictionary):
			continue
		var tag := str(task.get("target_tag", ""))
		if tag.is_empty():
			continue
		_target_counts[tag] = max(1, int(task.get("target_count", DEFAULT_ITEM_COUNT)))


func _create_item_group(definition: Dictionary) -> void:
	var template_path := NodePath(str(definition.get("template_path", "")))
	var template := get_node_or_null(template_path) as TextureRect
	if template == null:
		push_error("Level3 collectibles: missing item template %s" % template_path)
		return

	var regions := _get_regions(definition)
	if regions.is_empty():
		push_error("Level3 collectibles: no spawn regions for %s" % template_path)
		return

	var object_type := str(definition.get("object_type", ""))
	var object_color := str(definition.get("object_color", ""))
	var object_tag := str(definition.get("object_tag", ""))
	var item_count := int(_target_counts.get(object_tag, DEFAULT_ITEM_COUNT))
	var rotation_limit := float(definition.get("rotation_limit", ITEM_ROTATION_LIMIT))
	var far_scale := float(definition.get("far_scale", ITEM_FAR_SCALE))
	var near_scale := float(definition.get("near_scale", ITEM_NEAR_SCALE))
	var scale_jitter := float(definition.get("scale_jitter", PERSPECTIVE_SCALE_JITTER))
	var base_scale := template.scale * ITEM_SIZE_MULTIPLIER
	var region_order: Array[Node2D] = []
	for spawn_region in regions:
		region_order.append(spawn_region)
	region_order.shuffle()
	_ensure_region_spawn_counts(regions)

	template.visible = false
	for index in range(item_count):
		var item: TextureRect
		if index == 0:
			item = template
		else:
			item = template.duplicate() as TextureRect
			item.name = "%s_%02d" % [object_type, index + 1]
			template.get_parent().add_child(item)

		item.visible = true
		item.mouse_filter = Control.MOUSE_FILTER_IGNORE
		item.scale = base_scale
		item.pivot_offset = item.size * 0.5
		item.rotation = _random.randf_range(-rotation_limit, rotation_limit)
		var scale_jitter_factor := _random.randf_range(1.0 - scale_jitter, 1.0 + scale_jitter)

		var preferred_region: Node2D
		if index < region_order.size():
			preferred_region = region_order[index]
		else:
			preferred_region = regions[_random.randi_range(0, regions.size() - 1)]

		var placement: Dictionary = _random_spawn_placement(
			regions,
			preferred_region,
			item,
			_occupied_positions,
			_spawn_region_counts,
			base_scale,
			far_scale,
			near_scale,
			scale_jitter_factor
		)
		var region := placement.get("region") as Node2D
		var spawn_position: Vector2 = placement.get("position", Vector2.ZERO)
		_place_item_center(item, spawn_position)
		_occupied_positions.append(spawn_position)
		if region != null:
			_spawn_region_counts[region] = int(_spawn_region_counts.get(region, 0)) + 1
		_items.append(item)
		_set_item_metadata(
			item,
			"%s_%02d" % [object_type, index + 1],
			object_type,
			object_color,
			object_tag
		)
		if object_type == "fish":
			_fish_motion[item] = {
				"region": region,
				"target": _get_item_visual_center(item),
				"speed": 0.0,
				"pause": _random.randf_range(0.0, 0.8)
			}
		elif object_type == "firefly":
			_setup_firefly(
				item,
				region,
				base_scale,
				far_scale,
				near_scale,
				scale_jitter_factor
			)


func _move_fish(delta: float) -> void:
	for key in _fish_motion.keys():
		var fish := key as TextureRect
		if not is_instance_valid(fish):
			_fish_motion.erase(key)
			continue
		if fish in _collected_items or not fish.visible:
			continue

		var motion: Dictionary = _fish_motion[fish]
		var pause := float(motion["pause"])
		if pause > 0.0:
			motion["pause"] = maxf(pause - delta, 0.0)
			continue

		var region := motion["region"] as Node2D
		var center := _get_item_visual_center(fish)
		var target: Vector2 = motion["target"]
		if center.distance_to(target) <= 1.0:
			_assign_fish_target(fish, motion)
			continue

		var next_center := center.move_toward(target, float(motion["speed"]) * delta)
		var corner_offsets := _get_item_global_corner_offsets(fish)
		if not _fish_path_fits(region, center, next_center, corner_offsets):
			_assign_fish_target(fish, motion)
			continue

		_place_item_center(fish, next_center)


func _assign_fish_target(fish: TextureRect, motion: Dictionary) -> void:
	var region := motion["region"] as Node2D
	var center := _get_item_visual_center(fish)
	var corner_offsets := _get_item_global_corner_offsets(fish)
	motion["target"] = center
	motion["pause"] = _random.randf_range(0.3, 0.9)
	var minimum_speed := maxf(minf(fish_speed_min, fish_speed_max), 0.0)
	var maximum_speed := maxf(maxf(fish_speed_min, fish_speed_max), 0.0)
	motion["speed"] = _random.randf_range(minimum_speed, maximum_speed)

	for _attempt in range(48):
		var direction := Vector2.from_angle(_random.randf_range(-PI, PI))
		var target := center + direction * _random.randf_range(45.0, 150.0)
		if _fish_path_fits(region, center, target, corner_offsets):
			motion["target"] = target
			return


func _fish_path_fits(
	region: Node2D,
	start: Vector2,
	end: Vector2,
	corner_offsets: Array[Vector2]
) -> bool:
	# Check the whole fish footprint along the route, including concave shorelines.
	var steps := maxi(1, int(ceil(start.distance_to(end) / 4.0)))
	for index in range(steps + 1):
		var center := start.lerp(end, float(index) / float(steps))
		if region is CollisionPolygon2D:
			var footprint := PackedVector2Array()
			for offset in corner_offsets:
				footprint.append(region.to_local(center + offset))
			if not Geometry2D.clip_polygons(footprint, (region as CollisionPolygon2D).polygon).is_empty():
				return false
		elif not _item_corners_fit_in_region(region, center, corner_offsets):
			return false
	return true


func _setup_firefly(
	firefly: TextureRect,
	region: Node2D,
	base_scale: Vector2,
	far_scale: float,
	near_scale: float,
	scale_jitter_factor: float
) -> void:
	var unshaded_material := CanvasItemMaterial.new()
	unshaded_material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	firefly.material = unshaded_material
	firefly.light_mask = 0
	firefly.self_modulate = Color.WHITE
	firefly.set_meta("skip_background_brightness", true)
	var existing_adapter := firefly.get_node_or_null("BackgroundBrightnessAdapter")
	if existing_adapter != null:
		existing_adapter.queue_free()

	_firefly_regions[firefly] = region
	_firefly_base_scales[firefly] = base_scale
	_firefly_far_scales[firefly] = far_scale
	_firefly_near_scales[firefly] = near_scale
	_firefly_scale_jitters[firefly] = scale_jitter_factor
	_firefly_pause_timers[firefly] = _random.randf_range(FIREFLY_PAUSE_MIN, FIREFLY_PAUSE_MAX)
	_assign_firefly_target(firefly)


func _move_fireflies(delta: float) -> void:
	for key in _firefly_regions.keys():
		var firefly := key as TextureRect
		if firefly == null or not is_instance_valid(firefly):
			_cleanup_firefly(key)
			continue
		if firefly in _collected_items or not firefly.visible:
			continue

		var pause_timer := float(_firefly_pause_timers.get(firefly, 0.0))
		if pause_timer > 0.0:
			_firefly_pause_timers[firefly] = pause_timer - delta
			continue

		if not (_firefly_targets.get(firefly) is Vector2):
			_assign_firefly_target(firefly)
			continue

		var current_center := _get_item_visual_center(firefly)
		var target: Vector2 = _firefly_targets[firefly]
		var to_target := target - current_center
		var distance := to_target.length()
		if distance <= FIREFLY_TARGET_REACHED_DISTANCE:
			_firefly_pause_timers[firefly] = _random.randf_range(FIREFLY_PAUSE_MIN, FIREFLY_PAUSE_MAX)
			_assign_firefly_target(firefly)
			continue

		var speed := float(_firefly_speeds.get(firefly, FIREFLY_MOVE_SPEED_MIN))
		var movement := to_target.normalized() * minf(distance, speed * delta)
		firefly.global_position += movement
		_update_firefly_perspective_scale(firefly)


func _assign_firefly_target(firefly: TextureRect) -> void:
	if not is_instance_valid(firefly):
		return

	var region := _firefly_regions.get(firefly) as Node2D
	if region == null:
		return

	var base_scale: Vector2 = _firefly_base_scales.get(firefly, firefly.scale)
	var far_scale := float(_firefly_far_scales.get(firefly, FIREFLY_FAR_SCALE))
	var near_scale := float(_firefly_near_scales.get(firefly, FIREFLY_NEAR_SCALE))
	var scale_jitter_factor := float(_firefly_scale_jitters.get(firefly, 1.0))
	var target := _random_movement_target(
		region,
		firefly,
		base_scale,
		far_scale,
		near_scale,
		scale_jitter_factor
	)
	_firefly_targets[firefly] = target
	_firefly_speeds[firefly] = _random.randf_range(FIREFLY_MOVE_SPEED_MIN, FIREFLY_MOVE_SPEED_MAX)
	_update_firefly_perspective_scale(firefly)


func _random_movement_target(
	region: Node2D,
	item: TextureRect,
	base_scale: Vector2,
	far_scale: float,
	near_scale: float,
	scale_jitter_factor: float
) -> Vector2:
	var bounds := _get_region_bounds(region)
	var current_center := _get_item_visual_center(item)
	var fallback := current_center
	var best_distance := -1.0

	for _attempt in range(100):
		var local_position := Vector2(
			_random.randf_range(bounds.position.x, bounds.end.x),
			_random.randf_range(bounds.position.y, bounds.end.y)
		)
		if not _is_point_in_region(region, local_position):
			continue

		var global_position: Vector2 = region.to_global(local_position)
		_apply_perspective_scale(
			item,
			base_scale,
			region,
			bounds,
			local_position,
			far_scale,
			near_scale,
			scale_jitter_factor
		)
		if not _item_corners_fit_in_region(region, global_position, _get_item_global_corner_offsets(item)):
			continue

		var distance := current_center.distance_to(global_position)
		if distance > best_distance:
			fallback = global_position
			best_distance = distance
		if distance >= FIREFLY_MIN_TRAVEL_DISTANCE:
			return global_position

	return fallback


func _update_firefly_perspective_scale(firefly: TextureRect) -> void:
	var region := _firefly_regions.get(firefly) as Node2D
	if region == null:
		return

	_apply_perspective_scale(
		firefly,
		_firefly_base_scales.get(firefly, firefly.scale),
		region,
		_get_region_bounds(region),
		region.to_local(_get_item_visual_center(firefly)),
		float(_firefly_far_scales.get(firefly, FIREFLY_FAR_SCALE)),
		float(_firefly_near_scales.get(firefly, FIREFLY_NEAR_SCALE)),
		float(_firefly_scale_jitters.get(firefly, 1.0))
	)


func _cleanup_firefly(key) -> void:
	_firefly_regions.erase(key)
	_firefly_base_scales.erase(key)
	_firefly_far_scales.erase(key)
	_firefly_near_scales.erase(key)
	_firefly_scale_jitters.erase(key)
	_firefly_targets.erase(key)
	_firefly_speeds.erase(key)
	_firefly_pause_timers.erase(key)


func _get_regions(definition: Dictionary) -> Array[Node2D]:
	var regions: Array[Node2D] = []
	var region_paths: Array = definition.get("region_paths", [])
	for path in region_paths:
		_append_spawn_region(regions, get_node_or_null(NodePath(str(path))))

	var region_parent_path := str(definition.get("region_parent_path", ""))
	if not region_parent_path.is_empty():
		var region_parent := get_node_or_null(NodePath(region_parent_path))
		if region_parent != null:
			_append_child_spawn_regions(regions, region_parent)

	return regions


func _append_child_spawn_regions(regions: Array[Node2D], parent: Node) -> void:
	for child in parent.get_children():
		_append_spawn_region(regions, child)
		_append_child_spawn_regions(regions, child)


func _append_spawn_region(regions: Array[Node2D], node: Node) -> void:
	var region := node as Node2D
	if region == null:
		return
	if not (region is CollisionPolygon2D) and not (region is CollisionShape2D):
		return
	if regions.has(region):
		return
	if _get_region_bounds(region).size == Vector2.ZERO:
		return

	regions.append(region)


func _ensure_region_spawn_counts(regions: Array[Node2D]) -> void:
	for region in regions:
		if not _spawn_region_counts.has(region):
			_spawn_region_counts[region] = 0


func _random_spawn_placement(
	regions: Array[Node2D],
	preferred_region: Node2D,
	item: TextureRect,
	existing_positions: Array[Vector2],
	region_spawn_counts: Dictionary,
	base_scale: Vector2,
	far_scale: float,
	near_scale: float,
	scale_jitter_factor: float
) -> Dictionary:
	var search_regions: Array[Node2D] = _get_regions_by_area_priority(regions, preferred_region, region_spawn_counts)
	var valid_candidates: Array[Dictionary] = []

	for region in search_regions:
		var candidate: Dictionary = _best_spawn_candidate_in_region(
			region,
			item,
			existing_positions,
			base_scale,
			far_scale,
			near_scale,
			scale_jitter_factor
		)
		if candidate.is_empty():
			continue

		valid_candidates.append(candidate)

	var placement: Dictionary = _pick_area_weighted_candidate(valid_candidates, region_spawn_counts)
	if not placement.is_empty():
		_apply_spawn_placement_scale(
			item,
			placement,
			base_scale,
			far_scale,
			near_scale,
			scale_jitter_factor
		)
		return placement

	var fallback_region: Node2D = _get_largest_region(regions)
	var fallback_bounds := _get_region_bounds(fallback_region)
	var fallback_position: Vector2 = fallback_region.to_global(fallback_bounds.get_center())

	_apply_perspective_scale(
		item,
		base_scale,
		fallback_region,
		fallback_bounds,
		fallback_region.to_local(fallback_position),
		far_scale,
		near_scale,
		scale_jitter_factor
	)
	return {
		"region": fallback_region,
		"position": fallback_position
	}


func _get_regions_by_area_priority(
	regions: Array[Node2D],
	preferred_region: Node2D,
	region_spawn_counts: Dictionary
) -> Array[Node2D]:
	var remaining: Array[Node2D] = []
	if preferred_region != null:
		remaining.append(preferred_region)

	for region in regions:
		if region != preferred_region:
			remaining.append(region)

	var ordered: Array[Node2D] = []
	while not remaining.is_empty():
		var lowest_density := _get_lowest_projected_region_density(remaining, region_spawn_counts)
		var next_remaining: Array[Node2D] = []
		for region in remaining:
			if is_equal_approx(_get_projected_region_density(region, region_spawn_counts), lowest_density):
				ordered.append(region)
			else:
				next_remaining.append(region)
		remaining = next_remaining

	return ordered


func _get_lowest_projected_region_density(regions: Array[Node2D], region_spawn_counts: Dictionary) -> float:
	var lowest_density := 0.0
	var has_density := false
	for region in regions:
		var density := _get_projected_region_density(region, region_spawn_counts)
		if not has_density or density < lowest_density:
			lowest_density = density
			has_density = true

	return lowest_density


func _best_spawn_candidate_in_region(
	region: Node2D,
	item: TextureRect,
	existing_positions: Array[Vector2],
	base_scale: Vector2,
	far_scale: float,
	near_scale: float,
	scale_jitter_factor: float
) -> Dictionary:
	var bounds := _get_region_bounds(region)
	var best_position := Vector2.ZERO
	var best_distance := -1.0
	var found_candidate := false

	for _attempt in range(SPAWN_ATTEMPTS_PER_REGION):
		var local_position := Vector2(
			_random.randf_range(bounds.position.x, bounds.end.x),
			_random.randf_range(bounds.position.y, bounds.end.y)
		)
		if not _is_point_in_region(region, local_position):
			continue

		var global_position: Vector2 = region.to_global(local_position)
		_apply_perspective_scale(
			item,
			base_scale,
			region,
			bounds,
			local_position,
			far_scale,
			near_scale,
			scale_jitter_factor
		)
		var corner_offsets := _get_item_global_corner_offsets(item)
		if not _item_corners_fit_in_region(region, global_position, corner_offsets):
			continue

		var nearest_distance := _nearest_position_distance(global_position, existing_positions)
		if not found_candidate or nearest_distance > best_distance:
			best_position = global_position
			best_distance = nearest_distance
			found_candidate = true

	if not found_candidate:
		return {}

	return {
		"region": region,
		"position": best_position,
		"distance": best_distance
	}


func _pick_area_weighted_candidate(
	candidates: Array[Dictionary],
	region_spawn_counts: Dictionary
) -> Dictionary:
	var best_candidate: Dictionary = {}
	var best_density := 0.0
	var best_is_spaced := false
	var found_candidate := false
	for candidate in candidates:
		var region := candidate.get("region") as Node2D
		if region == null:
			continue

		var density := _get_projected_region_density(region, region_spawn_counts)
		var is_spaced := float(candidate.get("distance", -1.0)) >= MIN_SPAWN_DISTANCE
		if not found_candidate or density < best_density or (is_equal_approx(density, best_density) and is_spaced and not best_is_spaced):
			best_candidate = candidate
			best_density = density
			best_is_spaced = is_spaced
			found_candidate = true

	return best_candidate


func _get_projected_region_density(region: Node2D, region_spawn_counts: Dictionary) -> float:
	var usage := int(region_spawn_counts.get(region, 0))
	return float(usage + 1) / maxf(_get_region_area(region), MIN_REGION_AREA)


func _get_region_area(region: Node2D) -> float:
	if region is CollisionPolygon2D:
		var polygon := (region as CollisionPolygon2D).polygon
		if polygon.size() < 3:
			return 0.0

		var area := 0.0
		for index in range(polygon.size()):
			var next_index := (index + 1) % polygon.size()
			area += polygon[index].x * polygon[next_index].y
			area -= polygon[next_index].x * polygon[index].y
		return absf(area) * 0.5

	if region is CollisionShape2D:
		var shape := (region as CollisionShape2D).shape
		if shape is RectangleShape2D:
			var size := (shape as RectangleShape2D).size
			return size.x * size.y
		if shape is CircleShape2D:
			var radius := (shape as CircleShape2D).radius
			return PI * radius * radius
		if shape is CapsuleShape2D:
			var capsule := shape as CapsuleShape2D
			var straight_height := maxf(capsule.height - capsule.radius * 2.0, 0.0)
			return capsule.radius * 2.0 * straight_height + PI * capsule.radius * capsule.radius

	var bounds := _get_region_bounds(region)
	return bounds.size.x * bounds.size.y


func _apply_spawn_placement_scale(
	item: TextureRect,
	placement: Dictionary,
	base_scale: Vector2,
	far_scale: float,
	near_scale: float,
	scale_jitter_factor: float
) -> void:
	var region := placement.get("region") as Node2D
	if region == null:
		return

	var placement_position = placement.get("position")
	if not (placement_position is Vector2):
		return

	var global_position: Vector2 = placement_position
	var bounds := _get_region_bounds(region)
	_apply_perspective_scale(
		item,
		base_scale,
		region,
		bounds,
		region.to_local(global_position),
		far_scale,
		near_scale,
		scale_jitter_factor
	)


func _get_largest_region(regions: Array[Node2D]) -> Node2D:
	var largest_region := regions[0]
	var largest_area := -1.0
	for region in regions:
		var area := _get_region_area(region)
		if area > largest_area:
			largest_region = region
			largest_area = area

	return largest_region


func _apply_perspective_scale(
	item: TextureRect,
	base_scale: Vector2,
	region: Node2D,
	bounds: Rect2,
	local_position: Vector2,
	far_scale: float,
	near_scale: float,
	scale_jitter_factor: float
) -> void:
	var depth := _get_region_depth_weight(region, bounds, local_position)
	var scale_multiplier := lerpf(far_scale, near_scale, smoothstep(0.0, 1.0, depth))
	item.scale = base_scale * scale_multiplier * scale_jitter_factor


func _get_region_depth_weight(region: Node2D, bounds: Rect2, local_position: Vector2) -> float:
	var top_left := region.to_global(bounds.position)
	var top_right := region.to_global(Vector2(bounds.end.x, bounds.position.y))
	var bottom_right := region.to_global(bounds.end)
	var bottom_left := region.to_global(Vector2(bounds.position.x, bounds.end.y))
	var min_y := minf(minf(top_left.y, top_right.y), minf(bottom_right.y, bottom_left.y))
	var max_y := maxf(maxf(top_left.y, top_right.y), maxf(bottom_right.y, bottom_left.y))
	var depth_range := maxf(max_y - min_y, 1.0)
	return clampf((region.to_global(local_position).y - min_y) / depth_range, 0.0, 1.0)


func _get_region_bounds(region: Node2D) -> Rect2:
	if region is CollisionPolygon2D:
		var polygon := (region as CollisionPolygon2D).polygon
		if polygon.is_empty():
			return Rect2()

		var bounds := Rect2(polygon[0], Vector2.ZERO)
		for point in polygon:
			bounds = bounds.expand(point)
		return bounds

	if region is CollisionShape2D:
		var shape := (region as CollisionShape2D).shape
		if shape is RectangleShape2D:
			var size := (shape as RectangleShape2D).size
			return Rect2(-size * 0.5, size)
		if shape is CircleShape2D:
			var radius := (shape as CircleShape2D).radius
			return Rect2(Vector2(-radius, -radius), Vector2(radius * 2.0, radius * 2.0))
		if shape is CapsuleShape2D:
			var capsule := shape as CapsuleShape2D
			return Rect2(Vector2(-capsule.radius, -capsule.height * 0.5), Vector2(capsule.radius * 2.0, capsule.height))

	return Rect2()


func _is_point_in_region(region: Node2D, local_position: Vector2) -> bool:
	if region is CollisionPolygon2D:
		return Geometry2D.is_point_in_polygon(local_position, (region as CollisionPolygon2D).polygon)

	if region is CollisionShape2D:
		var shape := (region as CollisionShape2D).shape
		if shape is RectangleShape2D:
			var size := (shape as RectangleShape2D).size
			return Rect2(-size * 0.5, size).has_point(local_position)
		if shape is CircleShape2D:
			return local_position.length() <= (shape as CircleShape2D).radius
		if shape is CapsuleShape2D:
			var capsule := shape as CapsuleShape2D
			var half_straight := maxf(capsule.height * 0.5 - capsule.radius, 0.0)
			var clamped_y := clampf(local_position.y, -half_straight, half_straight)
			return Vector2(local_position.x, local_position.y - clamped_y).length() <= capsule.radius

	return false


func _item_corners_fit_in_region(
	region: Node2D,
	global_center: Vector2,
	corner_offsets: Array[Vector2]
) -> bool:
	for corner_offset in corner_offsets:
		if not _is_point_in_region(region, region.to_local(global_center + corner_offset)):
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
	var current_center := _get_item_visual_center(item)
	item.global_position += target_position - current_center


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


func _set_item_metadata(
	item: TextureRect,
	item_id: String,
	object_type: String,
	object_color: String,
	object_tag: String
) -> void:
	item.set_meta("object_id", item_id)
	item.set_meta("object_type", object_type)
	item.set_meta("object_color", object_color)
	item.set_meta("object_tags", PackedStringArray([object_tag]))
	item.set_meta("can_receive_mouse_input", false)


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
