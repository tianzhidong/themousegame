extends Sprite2D

const FALLBACK_REFERENCE_TEXTURE_HEIGHT := 900.0

var _base_scale := Vector2.ONE
var _reference_texture_height := FALLBACK_REFERENCE_TEXTURE_HEIGHT


func _ready() -> void:
	_base_scale = scale
	if texture != null and texture.get_height() > 0:
		_reference_texture_height = float(texture.get_height())

	var friend_texture_path := GameState.get_selected_friend_idle_texture_path()
	var friend_texture := load(friend_texture_path) as Texture2D
	if friend_texture == null:
		push_error("Failed to load selected friend texture: %s" % friend_texture_path)
		return

	set_friend_texture(friend_texture)


func set_friend_texture(friend_texture: Texture2D) -> void:
	if friend_texture == null:
		return

	texture = friend_texture
	_apply_consistent_visual_size(friend_texture)


func _apply_consistent_visual_size(friend_texture: Texture2D) -> void:
	var texture_height := friend_texture.get_height()
	if texture_height <= 0:
		scale = _base_scale
		return

	var texture_scale_factor := _reference_texture_height / float(texture_height)
	scale = _base_scale * texture_scale_factor
