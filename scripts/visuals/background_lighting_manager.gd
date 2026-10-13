extends Node

const ADAPTER_SCRIPT := preload("res://scripts/visuals/background_brightness_adapter.gd")
const ADAPTER_NODE_NAME := "BackgroundBrightnessAdapter"
const SCAN_INTERVAL := 0.25
const SUPPORTED_SCENE_PREFIX := "res://scenes/levels/"
const FIXGAME_SCENE_PATH := "res://scenes/game/fixgame.tscn"

var _current_scene: Node
var _background: Sprite2D
var _background_texture: Texture2D
var _background_image: Image
var _scan_timer := 0.0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_scan_timer = 0.0


func _process(delta: float) -> void:
	var scene := get_tree().current_scene
	if scene != _current_scene:
		_current_scene = scene
		_background = null
		_background_texture = null
		_background_image = null
		_scan_timer = 0.0

	if _current_scene == null or not _is_supported_scene(_current_scene):
		return

	_scan_timer -= delta
	if _scan_timer > 0.0:
		return

	_scan_timer = SCAN_INTERVAL
	_refresh_background()
	if _background == null or _background_image == null:
		return

	_scan_targets(_current_scene)


func _is_supported_scene(scene: Node) -> bool:
	var scene_path := scene.scene_file_path
	return scene_path.begins_with(SUPPORTED_SCENE_PREFIX) or scene_path == FIXGAME_SCENE_PATH


func _refresh_background() -> void:
	_background = _find_background(_current_scene)
	if _background == null or _background.texture == null:
		_background_texture = null
		_background_image = null
		return

	if _background.texture == _background_texture and _background_image != null:
		return

	_background_texture = _background.texture
	_background_image = _background_texture.get_image()
	if _background_image == null:
		push_warning("BackgroundLightingManager: could not read background image from %s" % _background.get_path())


func _find_background(root: Node) -> Sprite2D:
	var best_background: Sprite2D = null
	var best_score := -INF
	for candidate in _collect_sprite_background_candidates(root):
		var score := _get_background_score(candidate)
		if score > best_score:
			best_score = score
			best_background = candidate
	return best_background


func _collect_sprite_background_candidates(root: Node) -> Array[Sprite2D]:
	var candidates: Array[Sprite2D] = []
	if root is Sprite2D and (root as Sprite2D).texture != null:
		candidates.append(root as Sprite2D)

	for child in root.get_children():
		if child is CanvasLayer:
			continue
		candidates.append_array(_collect_sprite_background_candidates(child))
	return candidates


func _get_background_score(sprite: Sprite2D) -> float:
	var lower_name := str(sprite.name).to_lower()
	var rect_size := sprite.get_rect().size
	var global_scale := sprite.global_scale.abs()
	var score := maxf(rect_size.x * global_scale.x * rect_size.y * global_scale.y, 0.0)

	if lower_name.contains("day") and lower_name.contains("bg"):
		score += 1000000000.0
	elif lower_name.ends_with("bg") or lower_name.contains("background"):
		score += 500000000.0

	return score


func _scan_targets(root: Node) -> void:
	if _should_skip_branch(root):
		return

	if _is_brightness_target(root):
		_attach_or_update_adapter(root as CanvasItem)

	for child in root.get_children():
		_scan_targets(child)


func _is_brightness_target(node: Node) -> bool:
	if node == _background or not (node is CanvasItem):
		return false
	if bool(node.get_meta("skip_background_brightness", false)):
		_remove_adapter(node)
		return false
	if _is_adapter_node(node) or _is_background_node(node) or _is_ui_texture(node):
		return false

	if node is Sprite2D:
		return (node as Sprite2D).texture != null
	if node is TextureRect:
		return (node as TextureRect).texture != null
	if node is AnimatedSprite2D:
		var animated_sprite := node as AnimatedSprite2D
		return animated_sprite.sprite_frames != null \
			and animated_sprite.sprite_frames.has_animation(animated_sprite.animation) \
			and animated_sprite.sprite_frames.get_frame_count(animated_sprite.animation) > 0

	return false


func _attach_or_update_adapter(target: CanvasItem) -> void:
	var adapter := target.get_node_or_null(ADAPTER_NODE_NAME)
	if adapter == null:
		adapter = ADAPTER_SCRIPT.new()
		adapter.name = ADAPTER_NODE_NAME
		target.add_child(adapter)

	if adapter.has_method("configure"):
		adapter.call("configure", _background, _background_image)


func _remove_adapter(target: Node) -> void:
	var adapter := target.get_node_or_null(ADAPTER_NODE_NAME)
	if adapter != null:
		adapter.queue_free()


func _should_skip_branch(node: Node) -> bool:
	if node is CanvasLayer:
		return true
	if _is_adapter_node(node):
		return true

	var lower_name := str(node.name).to_lower()
	if lower_name in [
		"taskbar",
		"backbutton",
		"settingsbutton",
		"friend",
		"handlight",
		"resultoverlaylayer",
		"warmoverlay"
	]:
		return true

	return false


func _is_adapter_node(node: Node) -> bool:
	return node.name == ADAPTER_NODE_NAME


func _is_background_node(node: Node) -> bool:
	var lower_name := str(node.name).to_lower()
	return lower_name.ends_with("bg") or lower_name.contains("background")


func _is_ui_texture(node: Node) -> bool:
	var texture := _get_node_texture(node)
	if texture == null:
		return false

	var texture_path := texture.resource_path.to_lower()
	return texture_path.contains("/assets/images/ui/")


func _get_node_texture(node: Node) -> Texture2D:
	if node is Sprite2D:
		return (node as Sprite2D).texture
	if node is TextureRect:
		return (node as TextureRect).texture
	if node is AnimatedSprite2D:
		var animated_sprite := node as AnimatedSprite2D
		if animated_sprite.sprite_frames == null:
			return null
		if not animated_sprite.sprite_frames.has_animation(animated_sprite.animation):
			return null
		var frame_count := animated_sprite.sprite_frames.get_frame_count(animated_sprite.animation)
		if frame_count <= 0:
			return null
		return animated_sprite.sprite_frames.get_frame_texture(
			animated_sprite.animation,
			clampi(animated_sprite.frame, 0, frame_count - 1)
		)
	return null
