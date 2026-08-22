extends Label

const DAY_TEXT_TEMPLATE := "第%d天"
const STORY_VOICE_PATH := "res://assets/audio/voice/stories/story%d.wav"
const STORY_THEME_PATH := "res://assets/audio/voice/stories/day%d_story.wav"

@export var post_voice_delay := 1.0
@export var white_transition_duration := 0.45
@export var story_image_path: NodePath = NodePath("../../Day1")
@export var next_button_path: NodePath = NodePath("../../NextButton")
@export var story_title_path: NodePath = NodePath("../Label2")
@export var white_overlay_path: NodePath = NodePath("../WhiteOverlay")

@onready var _story_image: CanvasItem = get_node_or_null(story_image_path) as CanvasItem
@onready var _next_button: CanvasItem = get_node_or_null(next_button_path) as CanvasItem
@onready var _story_title: Label = get_node_or_null(story_title_path) as Label
@onready var _white_overlay: ColorRect = get_node_or_null(white_overlay_path) as ColorRect
@onready var _voice_player: AudioStreamPlayer = get_node_or_null("../StoryVoice") as AudioStreamPlayer
@onready var _story_theme_player: AudioStreamPlayer = get_node_or_null("../StoryTheme") as AudioStreamPlayer

var _story_theme_started := false


func _ready() -> void:
	text = DAY_TEXT_TEMPLATE % GameState.selected_day
	_show_title_screen()

	await _wait_for_story_voice()
	await get_tree().create_timer(post_voice_delay).timeout
	if not is_inside_tree():
		return

	await _transition_to_story()


func _show_title_screen() -> void:
	visible = true
	if _story_title != null:
		_story_title.visible = true

	_set_story_visible(false)

	if _white_overlay != null:
		_white_overlay.visible = false
		_white_overlay.modulate.a = 0.0


func _wait_for_story_voice() -> void:
	if _voice_player == null:
		push_error("StoryVoice node was not found in the stories scene")
		return

	var voice_path := STORY_VOICE_PATH % GameState.selected_day
	var voice_stream := load(voice_path) as AudioStream
	if voice_stream == null:
		push_error("Failed to load story voice: %s" % voice_path)
		return

	_voice_player.stream = voice_stream
	_voice_player.volume_db = 0.0
	_voice_player.play()

	while is_inside_tree() and _voice_player.playing:
		await get_tree().process_frame


func _transition_to_story() -> void:
	if _white_overlay == null:
		_hide_title_screen()
		_start_story()
		return

	_white_overlay.visible = true
	_white_overlay.modulate.a = 0.0

	var fade_to_white: Tween = create_tween()
	fade_to_white.tween_property(_white_overlay, "modulate:a", 1.0, white_transition_duration)
	await fade_to_white.finished

	if not is_inside_tree():
		return

	_hide_title_screen()
	_start_story()

	var fade_to_story: Tween = create_tween()
	fade_to_story.tween_property(_white_overlay, "modulate:a", 0.0, white_transition_duration)
	await fade_to_story.finished

	if is_inside_tree():
		_white_overlay.visible = false


func _hide_title_screen() -> void:
	visible = false
	if _story_title != null:
		_story_title.visible = false


func _start_story() -> void:
	_set_story_visible(true)
	_play_story_theme()


func _play_story_theme() -> void:
	if _story_theme_started:
		return
	_story_theme_started = true

	if _story_theme_player == null:
		push_error("StoryTheme node was not found in the stories scene")
		return

	var theme_path := STORY_THEME_PATH % GameState.selected_day
	var theme_stream := load(theme_path) as AudioStream
	if theme_stream == null:
		push_error("Failed to load story theme: %s" % theme_path)
		return

	_story_theme_player.stream = theme_stream
	_story_theme_player.volume_db = 0.0
	_story_theme_player.play()


func _set_story_visible(is_visible: bool) -> void:
	if _story_image != null:
		_story_image.visible = is_visible

	if _next_button != null:
		_next_button.visible = is_visible
