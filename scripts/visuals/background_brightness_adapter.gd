extends Node

const SAMPLE_INTERVAL := 0.08

@export_range(0.0, 16.0, 1.0) var sample_radius := 4.0
@export_range(0.1, 1.0, 0.01) var darkest_scale := 0.68
@export_range(1.0, 1.5, 0.01) var brightest_scale := 1.12
@export_range(0.0, 1.0, 0.01) var adaptation_strength := 0.78
@export_range(1.0, 30.0, 0.5) var smoothing_speed := 14.0

var _target: CanvasItem
var _background: Sprite2D
var _background_image: Image
var _base_self_modulate := Color.WHITE
var _desired_self_modulate := Color.WHITE
var _sample_timer := 0.0


func _ready() -> void:
	_target = get_parent() as CanvasItem
	if _target == null:
		set_process(false)
		return

	_base_self_modulate = _target.self_modulate
	_desired_self_modulate = _base_self_modulate


func configure(background: Sprite2D, background_image: Image) -> void:
	_background = background
	_background_image = background_image
	_sample_timer = 0.0
	_update_desired_modulate()
	if _target != null:
		_target.self_modulate = _desired_self_modulate


func _process(delta: float) -> void:
	if _target == null:
		return

	_sample_timer -= delta
	if _sample_timer <= 0.0:
		_sample_timer = SAMPLE_INTERVAL
		_update_desired_modulate()

	var weight := 1.0 - exp(-smoothing_speed * delta)
	_target.self_modulate = _target.self_modulate.lerp(_desired_self_modulate, weight)


func _update_desired_modulate() -> void:
	if _background == null or _background_image == null or not is_instance_valid(_background):
		_desired_self_modulate = _base_self_modulate
		return

	var pixel := _get_background_pixel(_get_target_world_center())
	if pixel.x < 0 or pixel.y < 0:
		_desired_self_modulate = _base_self_modulate
		return

	var brightness := _sample_background_brightness(pixel)
	var local_light_scale := lerpf(darkest_scale, brightest_scale, smoothstep(0.05, 0.95, brightness))
	var final_light_scale := lerpf(1.0, local_light_scale, adaptation_strength)
	_desired_self_modulate = Color(
		_base_self_modulate.r * final_light_scale,
		_base_self_modulate.g * final_light_scale,
		_base_self_modulate.b * final_light_scale,
		_base_self_modulate.a
	)


func _get_target_world_center() -> Vector2:
	if _target is TextureRect:
		return (_target as TextureRect).get_global_rect().get_center()

	if _target is Sprite2D:
		var sprite := _target as Sprite2D
		return sprite.to_global(sprite.get_rect().get_center())

	if _target is AnimatedSprite2D:
		return _get_animated_sprite_center(_target as AnimatedSprite2D)

	if _target is Node2D:
		return (_target as Node2D).global_position

	return Vector2.ZERO


func _get_animated_sprite_center(sprite: AnimatedSprite2D) -> Vector2:
	if sprite.sprite_frames == null:
		return sprite.global_position
	if not sprite.sprite_frames.has_animation(sprite.animation):
		return sprite.global_position

	var frame_count := sprite.sprite_frames.get_frame_count(sprite.animation)
	if frame_count <= 0:
		return sprite.global_position

	var texture := sprite.sprite_frames.get_frame_texture(
		sprite.animation,
		clampi(sprite.frame, 0, frame_count - 1)
	)
	if texture == null:
		return sprite.global_position

	var texture_size := texture.get_size()
	var local_position := -texture_size * 0.5 + sprite.offset if sprite.centered else sprite.offset
	return sprite.to_global(Rect2(local_position, texture_size).get_center())


func _get_background_pixel(world_position: Vector2) -> Vector2i:
	if _background.texture == null:
		return Vector2i(-1, -1)

	var local_position := _background.to_local(world_position)
	var background_rect := _background.get_rect()
	if background_rect.size.x <= 0.0 or background_rect.size.y <= 0.0:
		return Vector2i(-1, -1)
	if not background_rect.has_point(local_position):
		return Vector2i(-1, -1)

	var uv := Vector2(
		(local_position.x - background_rect.position.x) / background_rect.size.x,
		(local_position.y - background_rect.position.y) / background_rect.size.y
	)
	var image_size := _background_image.get_size()
	var sample_rect := Rect2(Vector2.ZERO, Vector2(float(image_size.x), float(image_size.y)))
	if _background.region_enabled:
		sample_rect = _background.region_rect

	var pixel := Vector2i(
		clampi(int(round(sample_rect.position.x + sample_rect.size.x * uv.x)), 0, image_size.x - 1),
		clampi(int(round(sample_rect.position.y + sample_rect.size.y * uv.y)), 0, image_size.y - 1)
	)
	return pixel


func _sample_background_brightness(center_pixel: Vector2i) -> float:
	var image_size := _background_image.get_size()
	var radius := int(sample_radius)
	var from_x := clampi(center_pixel.x - radius, 0, image_size.x - 1)
	var to_x := clampi(center_pixel.x + radius, 0, image_size.x - 1)
	var from_y := clampi(center_pixel.y - radius, 0, image_size.y - 1)
	var to_y := clampi(center_pixel.y + radius, 0, image_size.y - 1)
	var brightness_sum := 0.0
	var sample_count := 0

	for y in range(from_y, to_y + 1):
		for x in range(from_x, to_x + 1):
			var color := _background_image.get_pixel(x, y)
			brightness_sum += color.r * 0.2126 + color.g * 0.7152 + color.b * 0.0722
			sample_count += 1

	if sample_count == 0:
		return 1.0

	return clampf(brightness_sum / float(sample_count), 0.0, 1.0)
