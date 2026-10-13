extends CanvasLayer

const MACHINE_NAMES := ["风车", "水泵", "水车", "发电机器", "街灯", "机械钟", "传送带", "通风风机"]

@export_range(1, 8, 1) var module_index := 1
@export_range(-30.0, 30.0, 0.5) var ray_rotation_speed := 10.0
@export var machine_display_size := Vector2(880, 660)
var all_repaired := false

@onready var _presentation: Control = $Presentation
@onready var _center: Node2D = $Presentation/Center
@onready var _rays: Sprite2D = $Presentation/Center/Rays
@onready var _machine: Node2D = $Presentation/Center/Machine


func _ready() -> void:
	module_index = clampi(module_index, 1, 8)
	_center.position = get_viewport().get_visible_rect().get_center()
	$Presentation/Title.text = "%s修好啦！" % MACHINE_NAMES[module_index - 1]
	$Presentation/Note.text = "全部机器都修好了！" if all_repaired else "马上返回修理站"
	_rays.rotation = randf_range(0.0, TAU)
	_build_machine()
	_presentation.modulate.a = 0.0
	var final_scale := _machine.scale
	_machine.scale = final_scale * 0.92
	var tween := create_tween().set_parallel(true)
	tween.tween_property(_presentation, "modulate:a", 1.0, 0.2)
	tween.tween_property(_machine, "scale", final_scale, 0.3).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)


func _process(delta: float) -> void:
	_rays.rotation += deg_to_rad(ray_rotation_speed) * delta


func _build_machine() -> void:
	var body := Sprite2D.new()
	body.name = "MachineBody"
	body.texture = load("res://assets/images/objects/day5/m%d.png" % module_index) as Texture2D
	_machine.add_child(body)
	var scene := load("res://scenes/game/day5_targets/m%d.tscn" % module_index) as PackedScene
	var layout := scene.instantiate() as Sprite2D
	var yard_scale: float = layout.get_meta("yard_scale")
	var yard_offset: Vector2 = layout.get_meta("yard_offset")
	var to_yard := Transform2D(Vector2(yard_scale, 0), Vector2(0, yard_scale), yard_offset)
	var bounds := _visible_bounds(body)
	for index in range(1, 6):
		var template := layout.get_node("PartTarget%d" % index) as Sprite2D
		var part := template.duplicate() as Sprite2D
		_machine.add_child(part)
		part.transform = to_yard * template.transform
		bounds = bounds.merge(_visible_bounds(part))
	layout.free()
	# Fit the actual visible artwork, including any manually moved/rotated parts.
	var display_scale := minf(machine_display_size.x / maxf(bounds.size.x, 1.0), machine_display_size.y / maxf(bounds.size.y, 1.0))
	for child in _machine.get_children():
		(child as Node2D).position -= bounds.get_center()
	_machine.scale = Vector2.ONE * display_scale


func _visible_bounds(sprite: Sprite2D) -> Rect2:
	var used := sprite.texture.get_image().get_used_rect()
	var origin := Vector2(used.position) + sprite.offset
	if sprite.centered:
		origin -= Vector2(sprite.texture.get_size()) * 0.5
	var rect := Rect2(origin, Vector2(used.size))
	var bounds := Rect2(sprite.transform * rect.position, Vector2.ZERO)
	for corner in [Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]:
		bounds = bounds.expand(sprite.transform * corner)
	return bounds
