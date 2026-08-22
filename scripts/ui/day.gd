extends Sprite2D

const DAY_IMAGE_PATH := "res://assets/images/stoies/day_%d.png"

func _ready() -> void:
	var day := GameState.selected_day
	var image_path := DAY_IMAGE_PATH % day
	var day_texture := load(image_path) as Texture2D

	if day_texture == null:
		push_error("Failed to load story image: %s" % image_path)
		return

	texture = day_texture
