extends SceneTree

# Prepare complementary PNG pieces from the approved map, keeping every original pixel.
func _initialize() -> void:
	var folder := "res://assets/images/objects/day11/"
	var source := Image.load_from_file(folder + "treasure_puzzle_map.png")
	if source == null or source.is_empty():
		push_error("Missing Day11 puzzle map")
		quit(1)
		return
	source.convert(Image.FORMAT_RGBA8)
	var width := source.get_width()
	var height := source.get_height()
	var pieces: Array[Image] = []
	for index in range(5):
		pieces.append(Image.create(width, height, false, Image.FORMAT_RGBA8))
	for y in range(height):
		var vertical_split := width * (0.5 + 0.035 * sin(float(y) / height * TAU * 2.0))
		for x in range(width):
			var pixel := source.get_pixel(x, y)
			if pixel.a == 0:
				continue
			var phase := float(x) / width * TAU
			var upper := height * (0.31 + 0.035 * sin(phase + 0.3) + 0.015 * sin(phase * 3.0))
			var lower := height * (0.65 + 0.035 * sin(phase + 2.0) + 0.012 * sin(phase * 3.0 + 1.0))
			var owner := 2
			if y < upper:
				owner = 0 if x < vertical_split else 1
			elif y >= lower:
				owner = 3 if x < vertical_split else 4
			pieces[owner].set_pixel(x, y, pixel)
	var records: Array[Dictionary] = []
	for index in range(5):
		var bounds := pieces[index].get_used_rect()
		var cropped := pieces[index].get_region(bounds)
		var name := "treasure_puzzle_piece_%02d" % (index + 1)
		var error := cropped.save_png(folder + name + ".png")
		if error != OK:
			push_error("Failed to save " + name)
			quit(1)
			return
		var resource := '[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n'
		resource += '[ext_resource type="Texture2D" path="%s%s.png" id="1_piece"]\n\n' % [folder, name]
		resource += '[resource]\natlas = ExtResource("1_piece")\nregion = Rect2(0, 0, %d, %d)\nfilter_clip = true\n' % [bounds.size.x, bounds.size.y]
		FileAccess.open(folder + "map_fragment_%02d.tres" % (index + 1), FileAccess.WRITE).store_string(resource)
		records.append({"name": name, "size": [bounds.size.x, bounds.size.y],
			"offset": [bounds.position.x, bounds.position.y]})
	var manifest := {"source": "treasure_puzzle_map.png", "size": [width, height], "pieces": records,
		"generation": "imagegen transparent map; deterministic complementary curved cuts"}
	FileAccess.open(folder + "treasure_puzzle_layout.json", FileAccess.WRITE).store_string(JSON.stringify(manifest, "\t"))
	print("Prepared five complementary Day11 puzzle PNGs and resources.")
	quit()
