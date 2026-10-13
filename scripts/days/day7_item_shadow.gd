extends Node2D

const POINT_COUNT := 40
const SOFT_LAYERS := 6
const SHADOW_COLOR := Color(0.055, 0.035, 0.02)

var _radius := Vector2.ZERO


func setup(item: Sprite2D) -> void:
	var image := item.texture.get_image()
	if image == null:
		return

	# Ignore transparent padding so the shadow touches the visible object.
	var minimum := Vector2(image.get_size())
	var maximum := Vector2.ZERO
	for y in range(image.get_height()):
		for x in range(image.get_width()):
			if image.get_pixel(x, y).a <= 0.1:
				continue
			minimum = minimum.min(Vector2(x, y))
			maximum = maximum.max(Vector2(x + 1, y + 1))
	if minimum.x >= maximum.x or minimum.y >= maximum.y:
		return

	var visible_rect := Rect2(item.get_rect().position + minimum, maximum - minimum)
	var corners := [
		visible_rect.position,
		Vector2(visible_rect.end.x, visible_rect.position.y),
		visible_rect.end,
		Vector2(visible_rect.position.x, visible_rect.end.y),
	]
	minimum = Vector2(INF, INF)
	maximum = Vector2(-INF, -INF)
	for corner: Vector2 in corners:
		var rotated_corner := corner.rotated(item.rotation)
		minimum = minimum.min(rotated_corner)
		maximum = maximum.max(rotated_corner)

	# Cancel the item's tilt: the ground shadow remains horizontal and below it.
	rotation = -item.rotation
	position = Vector2((minimum.x + maximum.x) * 0.5, maximum.y - 2.0).rotated(-item.rotation)
	var width := maximum.x - minimum.x
	_radius = Vector2(width * 0.38, maxf(width * 0.075, 4.0))
	show_behind_parent = true
	queue_redraw()


func _draw() -> void:
	if _radius == Vector2.ZERO:
		return
	for layer in range(SOFT_LAYERS):
		var fraction := float(layer) / float(SOFT_LAYERS - 1)
		var radius := _radius * lerpf(1.0, 0.42, fraction)
		var points := PackedVector2Array()
		for index in range(POINT_COUNT):
			var angle := TAU * float(index) / float(POINT_COUNT)
			points.append(Vector2(cos(angle) * radius.x, sin(angle) * radius.y))
		var color := SHADOW_COLOR
		color.a = lerpf(0.025, 0.09, fraction)
		draw_colored_polygon(points, color)
