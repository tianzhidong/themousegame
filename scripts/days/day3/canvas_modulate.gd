extends CanvasModulate

@export var night_color := Color(0.20, 0.25, 0.42, 1.0)
@export_range(1.0, 30.0, 0.5) var follow_speed := 14.0
@export_range(1.0, 30.0, 0.5) var turn_speed := 18.0
@export_range(0.1, 4.0, 0.1) var light_energy := 1.35
@export var light_color := Color(1.0, 0.91, 0.68, 1.0)

@onready var _handlight: Sprite2D = get_node_or_null("../Handlight") as Sprite2D
@onready var _beam: PointLight2D = get_node_or_null("../Handlight/PointLight2D") as PointLight2D

var _muzzle_offset := Vector2.ZERO
var _last_mouse_position := Vector2.ZERO
var _target_rotation := 0.0


func _ready() -> void:
	color = night_color

	if _handlight == null or _beam == null:
		push_error("Day3 night effect needs Handlight and Handlight/PointLight2D nodes")
		return

	_beam.enabled = true
	_beam.energy = light_energy
	_beam.color = light_color
	_muzzle_offset = _beam.position * _handlight.scale
	_last_mouse_position = get_viewport().get_mouse_position()
	_target_rotation = _handlight.global_rotation


func _process(_delta: float) -> void:
	if _handlight == null or _beam == null:
		return

	var mouse_position := get_viewport().get_mouse_position()
	var mouse_movement := mouse_position - _last_mouse_position
	if mouse_movement.length_squared() > 1.0:
		# Freeze this target as soon as the mouse stops, avoiding rotation feedback.
		var direction_to_cursor := mouse_position - _handlight.global_position
		if direction_to_cursor.length_squared() > 1.0:
			_target_rotation = direction_to_cursor.angle()

	var rotation_weight := 1.0 - exp(-turn_speed * _delta)
	_handlight.global_rotation = lerp_angle(
		_handlight.global_rotation,
		_target_rotation,
		rotation_weight
	)

	var desired_handlight_position := mouse_position - _muzzle_offset.rotated(_handlight.global_rotation)
	var position_weight := 1.0 - exp(-follow_speed * _delta)
	_handlight.global_position = _handlight.global_position.lerp(
		desired_handlight_position,
		position_weight
	)

	# Keep illumination exactly at the cursor, even while the torch body eases into place.
	_beam.global_position = mouse_position
	_last_mouse_position = mouse_position
