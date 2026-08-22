extends Sprite2D


func _ready() -> void:
	var friend_texture := load(GameState.get_selected_friend_idle_texture_path()) as Texture2D
	if friend_texture == null:
		push_error("Failed to load selected friend texture: %s" % GameState.get_selected_friend_idle_texture_path())
		return

	texture = friend_texture
