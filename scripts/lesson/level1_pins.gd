extends Area2D

const PIN_COUNT := 10
const PIN_SOUND := preload("res://assets/audio/voice/day1/pin.ogg")
const PIN_ROTATION_DURATION := 0.7
const OBJECT_COUNT :=10
const BUTTERFLY_COUNT := 10
const BUTTERFLY_SCREEN_MARGIN := 120.0
const FLOWER_SOUND := preload("res://assets/audio/ui/object_flower.ogg")
const LIGHT_SOUND := preload("res://assets/audio/ui/light_on.ogg")
const MIN_SPAWN_DISTANCE := 58.0

var _random := RandomNumberGenerator.new()
var _spawn_regions: Array[CollisionPolygon2D] = []
var _pin_template: Sprite2D
var _occupied_positions: Array[Vector2] = []
var _interactive_objects: Array[AnimatedSprite2D] = []
var _object_collision_shapes: Dictionary = {}
var _object_target_animations: Dictionary = {}
var _object_uses_light_sound: Dictionary = {}
var _object_is_butterfly: Dictionary = {}
var _object_ids: Dictionary = {}
var _object_types: Dictionary = {}
var _object_colors: Dictionary = {}
var _object_tags: Dictionary = {}
var _triggered_pins: Dictionary = {}
var _triggered_objects: Dictionary = {}
var _pin_audio_player: AudioStreamPlayer
var _flower_audio_player: AudioStreamPlayer
var _light_audio_player: AudioStreamPlayer
var _task_manager: Node


func _ready() -> void:
	_random.randomize()
	_task_manager = get_node_or_null("/root/TaskManager")
	if _task_manager == null:
		push_error("Level1: required TaskManager autoload was not found; interactive objects are disabled")
	elif _task_manager.has_method("EnsureSelectedDayLoaded"):
		_task_manager.call("EnsureSelectedDayLoaded")
	elif _task_manager.has_method("ensure_selected_day_loaded"):
		_task_manager.call("ensure_selected_day_loaded")

	_pin_template = get_node_or_null("Pin1") as Sprite2D
	if _pin_template == null:
		_pin_template = get_node_or_null("CollisionPolygon2D/Pin1") as Sprite2D
	if _pin_template == null:
		push_error("Pin1 template was not found in Area2D")
		return

	for child in get_children():
		var region := child as CollisionPolygon2D
		if region != null and not region.polygon.is_empty():
			_spawn_regions.append(region)
	if _spawn_regions.is_empty():
		push_error("No CollisionPolygon2D regions were found in Area2D")
		return

	_create_pin_groups()
	_create_interactive_object_groups()
	_create_butterfly_groups()

	_pin_audio_player = AudioStreamPlayer.new()
	_pin_audio_player.stream = PIN_SOUND
	add_child(_pin_audio_player)

	_flower_audio_player = AudioStreamPlayer.new()
	_flower_audio_player.stream = FLOWER_SOUND
	add_child(_flower_audio_player)

	_light_audio_player = AudioStreamPlayer.new()
	_light_audio_player.stream = LIGHT_SOUND
	add_child(_light_audio_player)


func _process(_delta: float) -> void:
	var mouse_position := get_global_mouse_position()
	var pins := _get_pin_groups()
	for pin_index in range(pins.size()):
		var pin := pins[pin_index]
		var pin2 := pin.get_node_or_null("Pin2") as Sprite2D
		if pin2 == null or pin2 in _triggered_pins:
			continue

		var pin_id := "pin_%02d" % (pin_index + 1)
		if _contains_sprite_point(pin2, mouse_position) and _try_accept_interaction(
			pin2,
			"move",
			pin_id,
			"pin",
			"",
			"garden_pin"
		):
			_trigger_pin(pin2)

	for object in _interactive_objects:
		if object in _triggered_objects:
			continue

		if _contains_collision_shape(object, mouse_position) and _try_accept_interaction(
			object,
			"move",
			str(_object_ids.get(object, "")),
			str(_object_types.get(object, "")),
			str(_object_colors.get(object, "")),
			str(_object_tags.get(object, ""))
		):
			_trigger_interactive_object(object)


func _create_pin_groups() -> void:
	var first_position := _random_spawn_position(_occupied_positions)
	_pin_template.position = first_position
	_occupied_positions.append(first_position)
	_set_interactive_metadata(
		_pin_template.get_node_or_null("Pin2"),
		"pin_01",
		"pin",
		"",
		"garden_pin"
	)

	for index in range(1, PIN_COUNT):
		var pin := _pin_template.duplicate() as Sprite2D
		pin.name = "Pin1_%02d" % (index + 1)
		add_child(pin)

		var pin_position := _random_spawn_position(_occupied_positions)
		pin.position = pin_position
		_occupied_positions.append(pin_position)
		_set_interactive_metadata(
			pin.get_node_or_null("Pin2"),
			"pin_%02d" % (index + 1),
			"pin",
			"",
			"garden_pin"
		)


func _create_interactive_object_groups() -> void:
	_create_object_groups("RedFlower", &"red_flower_open", false, "flower", "red", "garden_flower")
	_create_object_groups("BlueFlower", &"blue_flower_open", false, "flower", "blue", "garden_flower")
	_create_object_groups("YellowFlower", &"yellow_flower_open", false, "flower", "yellow", "garden_flower")
	_create_object_groups("Light", &"light_on", true, "light", "", "town_light")


func _create_butterfly_groups() -> void:
	var template := get_parent().get_node_or_null("AnimatedSprite2D") as AnimatedSprite2D
	if template == null or template.sprite_frames == null:
		push_error("AnimatedSprite2D butterfly template was not found in Level1")
		return

	if not template.sprite_frames.has_animation(&"butterfly_fly"):
		push_error("AnimatedSprite2D butterfly template has no butterfly_fly animation")
		return

	for index in range(BUTTERFLY_COUNT):
		var butterfly: AnimatedSprite2D
		if index == 0:
			butterfly = template
		else:
			butterfly = template.duplicate() as AnimatedSprite2D
			butterfly.name = "Butterfly_%02d" % (index + 1)
			get_parent().call_deferred("add_child", butterfly)

		butterfly.animation = &"butterfly_fly"
		butterfly.frame = 0
		butterfly.stop()
		var area_position := _random_spawn_position(_occupied_positions, BUTTERFLY_SCREEN_MARGIN)
		if index == 0:
			butterfly.global_position = _clamp_butterfly_global_position(to_global(area_position))
		else:
			call_deferred("_place_butterfly", butterfly, area_position)
		_occupied_positions.append(area_position)
		_interactive_objects.append(butterfly)
		_object_target_animations[butterfly] = &"butterfly_fly"
		_object_uses_light_sound[butterfly] = false
		_object_is_butterfly[butterfly] = true
		var butterfly_id := "butterfly_%02d" % (index + 1)
		_object_ids[butterfly] = butterfly_id
		_object_types[butterfly] = "butterfly"
		_object_colors[butterfly] = ""
		_object_tags[butterfly] = "garden_butterfly"
		_set_interactive_metadata(
			butterfly,
			butterfly_id,
			"butterfly",
			"",
			"garden_butterfly"
		)


func _place_butterfly(butterfly: AnimatedSprite2D, area_position: Vector2) -> void:
	if not is_instance_valid(butterfly):
		return
	if not butterfly.is_inside_tree():
		call_deferred("_place_butterfly", butterfly, area_position)
		return

	butterfly.global_position = _clamp_butterfly_global_position(to_global(area_position))


func _create_object_groups(
	object_name: String,
	target_animation: StringName,
	uses_light_sound: bool,
	object_type: String,
	object_color: String,
	object_tag: String
) -> void:
	var template := _find_object_template(object_name)
	if template == null:
		push_error("Interactive object template was not found in Area2D: %s" % object_name)
		return
	var template_collision_shape := template.get_parent() as CollisionShape2D
	if template_collision_shape == null:
		push_error("Interactive object is not wrapped by CollisionShape2D: %s" % object_name)
		return

	for index in range(OBJECT_COUNT):
		var collision_shape: CollisionShape2D = template_collision_shape
		if index > 0:
			collision_shape = template_collision_shape.duplicate() as CollisionShape2D
			collision_shape.name = "%s_%02d" % [template_collision_shape.name, index + 1]
			add_child(collision_shape)

		var object := collision_shape.get_node(object_name) as AnimatedSprite2D
		if index > 0:
			object.name = "%s_%02d" % [object_name, index + 1]

		var object_pivot := _random_spawn_position(_occupied_positions)
		_place_object_by_pivot(collision_shape, object, object_pivot)
		_occupied_positions.append(object_pivot)
		_interactive_objects.append(object)
		_object_collision_shapes[object] = collision_shape
		_object_target_animations[object] = target_animation
		_object_uses_light_sound[object] = uses_light_sound
		var object_id := "%s_%02d" % [object_name.to_snake_case(), index + 1]
		_object_ids[object] = object_id
		_object_types[object] = object_type
		_object_colors[object] = object_color
		_object_tags[object] = object_tag
		_set_interactive_metadata(object, object_id, object_type, object_color, object_tag)


func _try_accept_interaction(
	interactive_node: Node,
	operation: String,
	object_id: String,
	object_type: String,
	object_color: String,
	object_tag: String
) -> bool:
	if _task_manager == null:
		return false

	var tags := PackedStringArray()
	if not object_tag.is_empty():
		tags.append(object_tag)

	var report := {
		"operation": operation,
		"object_id": object_id,
		"object_type": object_type,
		"object_color": object_color,
		"object_tags": tags,
		"timestamp": Time.get_ticks_msec(),
	}

	var can_interact_method := &"CanInteract"
	if not _task_manager.has_method(can_interact_method):
		can_interact_method = &"can_interact"
	if not _task_manager.has_method(can_interact_method):
		interactive_node.set_meta("can_receive_mouse_input", false)
		return false

	var can_receive_input := bool(_task_manager.call(can_interact_method, report))
	interactive_node.set_meta("can_receive_mouse_input", can_receive_input)
	if not can_receive_input:
		return false

	var handle_method := &"TryHandleInteraction"
	if not _task_manager.has_method(handle_method):
		handle_method = &"try_handle_interaction"
	if not _task_manager.has_method(handle_method):
		interactive_node.set_meta("can_receive_mouse_input", false)
		return false

	var accepted := bool(_task_manager.call(handle_method, report))
	interactive_node.set_meta(
		"can_receive_mouse_input",
		bool(_task_manager.call(can_interact_method, report))
	)
	return accepted


func _set_interactive_metadata(
	interactive_node: Node,
	object_id: String,
	object_type: String,
	object_color: String,
	object_tag: String
) -> void:
	if interactive_node == null:
		return

	var tags := PackedStringArray()
	if not object_tag.is_empty():
		tags.append(object_tag)
	interactive_node.set_meta("object_id", object_id)
	interactive_node.set_meta("object_type", object_type)
	interactive_node.set_meta("object_color", object_color)
	interactive_node.set_meta("object_tags", tags)
	interactive_node.set_meta("can_receive_mouse_input", false)


func _place_object_by_pivot(collision_shape: CollisionShape2D, object: AnimatedSprite2D, target_pivot: Vector2) -> void:
	collision_shape.position = Vector2.ZERO
	var object_pivot_at_zero := to_local(object.global_position)
	collision_shape.position = target_pivot - object_pivot_at_zero


func _find_object_template(object_name: String) -> AnimatedSprite2D:
	for child in get_children():
		var collision_shape := child as CollisionShape2D
		if collision_shape == null:
			continue
		var object := collision_shape.get_node_or_null(object_name) as AnimatedSprite2D
		if object != null:
			return object
	return null


func _get_pin_groups() -> Array[Sprite2D]:
	var pins: Array[Sprite2D] = []
	for child in get_children():
		var pin := child as Sprite2D
		if pin != null and pin.name.begins_with("Pin1"):
			pins.append(pin)
	return pins


func _random_spawn_position(
	existing_positions: Array[Vector2],
	screen_margin: float = 0.0
) -> Vector2:
	var best_position := Vector2.ZERO
	var best_distance := -1.0
	for _attempt in range(500):
		var region := _spawn_regions[_random.randi_range(0, _spawn_regions.size() - 1)]
		var polygon := region.polygon
		if polygon.is_empty():
			continue

		var bounds := Rect2(polygon[0], Vector2.ZERO)
		for point in polygon:
			bounds = bounds.expand(point)

		var candidate := Vector2(
			_random.randf_range(bounds.position.x, bounds.end.x),
			_random.randf_range(bounds.position.y, bounds.end.y)
		)
		if not Geometry2D.is_point_in_polygon(candidate, polygon):
			continue
		var area_position := to_local(region.to_global(candidate))
		if screen_margin > 0.0:
			var viewport_size := get_viewport_rect().size
			var safe_rect := Rect2(
				screen_margin,
				screen_margin,
				viewport_size.x - screen_margin * 2.0,
				viewport_size.y - screen_margin * 2.0
			)
			if safe_rect.size.x <= 0.0 or safe_rect.size.y <= 0.0:
				continue
			if not safe_rect.has_point(to_global(area_position)):
				continue
		var nearest_distance := _nearest_position_distance(area_position, existing_positions)
		if nearest_distance > best_distance:
			best_position = area_position
			best_distance = nearest_distance
		if nearest_distance >= MIN_SPAWN_DISTANCE:
			return area_position

	return best_position


func _nearest_position_distance(candidate: Vector2, existing_positions: Array[Vector2]) -> float:
	if existing_positions.is_empty():
		return INF

	var nearest_distance := INF
	for existing_position in existing_positions:
		nearest_distance = minf(nearest_distance, candidate.distance_to(existing_position))
	return nearest_distance


func _contains_sprite_point(sprite: Node2D, global_point: Vector2) -> bool:
	if not sprite.visible:
		return false

	if sprite is Sprite2D:
		var regular_sprite := sprite as Sprite2D
		if regular_sprite.texture == null:
			return false
		return regular_sprite.get_rect().has_point(regular_sprite.to_local(global_point))

	if sprite is AnimatedSprite2D:
		var animated_sprite := sprite as AnimatedSprite2D
		if animated_sprite.sprite_frames == null:
			return false
		var texture := animated_sprite.sprite_frames.get_frame_texture(
			animated_sprite.animation,
			animated_sprite.frame
		)
		if texture == null:
			return false

		var texture_size := texture.get_size()
		var local_position := -texture_size * 0.5 + animated_sprite.offset if animated_sprite.centered else animated_sprite.offset
		return Rect2(local_position, texture_size).has_point(animated_sprite.to_local(global_point))

	return false


func _contains_collision_shape(object: AnimatedSprite2D, global_point: Vector2) -> bool:
	var collision_shape := _object_collision_shapes.get(object) as CollisionShape2D
	if collision_shape == null or collision_shape.shape == null or collision_shape.disabled:
		return _contains_sprite_point(object, global_point)

	var query := PhysicsPointQueryParameters2D.new()
	query.position = global_point
	query.collision_mask = collision_layer
	query.collide_with_areas = true
	query.collide_with_bodies = false

	for hit in get_world_2d().direct_space_state.intersect_point(query):
		if hit.get("collider") != self:
			continue

		var shape_index := int(hit.get("shape", -1))
		if shape_index < 0:
			continue

		var owner_id := shape_find_owner(shape_index)
		if owner_id != -1 and shape_owner_get_owner(owner_id) == collision_shape:
			return true

	return false


func _trigger_pin(pin2: Sprite2D) -> void:
	_triggered_pins[pin2] = true
	_pin_audio_player.play()
	_spin_pin_forever(pin2)


func _trigger_interactive_object(object: AnimatedSprite2D) -> void:
	_triggered_objects[object] = true
	object.animation = _object_target_animations[object]
	object.frame = 0
	object.play()

	if bool(_object_is_butterfly.get(object, false)):
		_start_butterfly_flight(object)
		return

	if _object_uses_light_sound[object]:
		_light_audio_player.play()
	else:
		_flower_audio_player.play()


func _start_butterfly_flight(butterfly: AnimatedSprite2D) -> void:
	_fly_butterfly_forever(butterfly)


func _fly_butterfly_forever(butterfly: AnimatedSprite2D) -> void:
	while is_inside_tree() and is_instance_valid(butterfly):
		var area_target := _random_spawn_position([], BUTTERFLY_SCREEN_MARGIN)
		var world_target := _clamp_butterfly_global_position(to_global(area_target))
		var parent_2d := get_parent() as Node2D
		if parent_2d == null:
			return

		var target_position := parent_2d.to_local(world_target)
		var distance := butterfly.position.distance_to(target_position)
		var duration := clampf(distance / 120.0, 1.8, 4.5)
		var flight_tween := create_tween()
		flight_tween.set_trans(Tween.TRANS_SINE)
		flight_tween.set_ease(Tween.EASE_IN_OUT)
		flight_tween.tween_property(butterfly, "position", target_position, duration)
		await flight_tween.finished


func _clamp_butterfly_global_position(world_position: Vector2) -> Vector2:
	var viewport_size := get_viewport_rect().size
	var horizontal_margin := minf(BUTTERFLY_SCREEN_MARGIN, viewport_size.x * 0.5)
	var vertical_margin := minf(BUTTERFLY_SCREEN_MARGIN, viewport_size.y * 0.5)
	return Vector2(
		clampf(world_position.x, horizontal_margin, viewport_size.x - horizontal_margin),
		clampf(world_position.y, vertical_margin, viewport_size.y - vertical_margin)
	)


func _spin_pin_forever(pin2: Sprite2D) -> void:
	while is_inside_tree() and is_instance_valid(pin2):
		var rotation_tween := create_tween()
		rotation_tween.set_trans(Tween.TRANS_LINEAR)
		rotation_tween.tween_property(pin2, "rotation", pin2.rotation + TAU, PIN_ROTATION_DURATION)
		await rotation_tween.finished
