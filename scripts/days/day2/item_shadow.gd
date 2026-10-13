class_name ItemShadow
extends Node2D

const POINT_COUNT := 40
const SOFT_LAYER_COUNT := 5
const OUTER_ALPHA := 0.06
const INNER_ALPHA := 0.16
const CONTACT_ALPHA := 0.16
const SHADOW_COLOR := Color(0.035, 0.02, 0.01)

var _radius := Vector2(48.0, 14.0)


func setup(item_size: Vector2) -> void:
	var short_side := minf(item_size.x, item_size.y)
	_radius = Vector2(maxf(short_side * 0.52, 30.0), maxf(short_side * 0.18, 10.0))
	position = Vector2(
		item_size.x * 0.5 + maxf(short_side * 0.04, 8.0),
		item_size.y * 0.5 + maxf(short_side * 0.16, 18.0)
	)
	queue_redraw()


func _draw() -> void:
	for layer in range(SOFT_LAYER_COUNT):
		var progress := float(layer) / float(SOFT_LAYER_COUNT - 1)
		var layer_radius := _radius * lerpf(1.0, 0.5, progress)
		var alpha := lerpf(OUTER_ALPHA, INNER_ALPHA, progress)
		draw_colored_polygon(_make_ellipse_polygon(layer_radius), _shadow_color(alpha))

	draw_colored_polygon(_make_ellipse_polygon(_radius * Vector2(0.55, 0.45)), _shadow_color(CONTACT_ALPHA))


func _shadow_color(alpha: float) -> Color:
	return Color(SHADOW_COLOR.r, SHADOW_COLOR.g, SHADOW_COLOR.b, alpha)


func _make_ellipse_polygon(radius: Vector2) -> PackedVector2Array:
	var polygon := PackedVector2Array()
	for point_index in range(POINT_COUNT):
		var angle := TAU * float(point_index) / float(POINT_COUNT)
		polygon.append(Vector2(cos(angle) * radius.x, sin(angle) * radius.y))
	return polygon
