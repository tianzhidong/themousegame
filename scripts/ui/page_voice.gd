extends AudioStreamPlayer

const PAGE_VOICE_GROUP := "page_voice_players"

@export var start_delay_seconds := 0.35
@export var preceding_sound_path: NodePath
@export var post_sound_delay_seconds := 1.0

var _cancelled := false


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	add_to_group(PAGE_VOICE_GROUP)
	# Opening a new page or dialog cancels the previous page's pending or playing voice.
	for other in get_tree().get_nodes_in_group(PAGE_VOICE_GROUP):
		if other != self:
			other.cancel_voice()
	var audio_manager := get_node_or_null("/root/AudioManager")
	if audio_manager != null:
		audio_manager.call("StopTaskHint")
	get_window().focus_exited.connect(cancel_voice)
	call_deferred("_play_page_voice")


func _play_page_voice() -> void:
	await get_tree().create_timer(start_delay_seconds).timeout
	if not _can_play():
		return
	if not preceding_sound_path.is_empty():
		var preceding := get_node_or_null(preceding_sound_path) as AudioStreamPlayer
		while is_instance_valid(preceding) and preceding.playing:
			await get_tree().process_frame
			if not _can_play():
				return
		await get_tree().create_timer(post_sound_delay_seconds).timeout
		if not _can_play():
			return
	if stream != null:
		play()


func _can_play() -> bool:
	return is_inside_tree() and not _cancelled and get_window().has_focus()


func cancel_voice() -> void:
	_cancelled = true
	stop()


func _exit_tree() -> void:
	cancel_voice()
