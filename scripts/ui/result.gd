extends Node2D

## Plays the friends entrance without changing the result panel layout.
## Friends is rendered inside FriendsClip, whose bounds match the Area2D
## collision rectangle, so the part outside the display region is clipped.

@export var entrance_duration := 0.85
@export var bottom_padding := 24.0

@onready var _friends_clip: Control = get_node_or_null("Area2D/CollisionShape2D/FriendsClip") as Control
@onready var _friends: Sprite2D = get_node_or_null("Area2D/CollisionShape2D/FriendsClip/Friends") as Sprite2D
@onready var _confetti: GPUParticles2D = get_node_or_null("GPUParticles2D") as GPUParticles2D
@onready var _congratulation_sound: AudioStreamPlayer = get_node_or_null("CongratulationSound") as AudioStreamPlayer

var _rest_position := Vector2.ZERO
var _entrance_tween: Tween
var _celebration_started := false


func _ready() -> void:
	if _friends_clip == null or _friends == null:
		push_error("Result scene is missing FriendsClip or Friends; the friends entrance animation was skipped.")
		return

	_rest_position = _friends.position
	var texture_height := 0.0
	if _friends.texture != null:
		texture_height = float(_friends.texture.get_height())

	# With Sprite2D centered, this places the complete image below the clipped area.
	_friends.position = Vector2(
		_rest_position.x,
		_friends_clip.size.y + texture_height * 0.5 + bottom_padding
	)
	call_deferred("_play_friends_entrance")


func _play_friends_entrance() -> void:
	if _friends == null:
		return

	_play_confetti()

	if _entrance_tween != null and _entrance_tween.is_valid():
		_entrance_tween.kill()

	_entrance_tween = create_tween()
	_entrance_tween.set_trans(Tween.TRANS_BACK)
	_entrance_tween.set_ease(Tween.EASE_OUT)
	_entrance_tween.tween_property(_friends, "position", _rest_position, entrance_duration)


func _play_confetti() -> void:
	if _celebration_started:
		return
	_celebration_started = true

	if _confetti != null:
		_confetti.restart()
		_confetti.emitting = true

	if _congratulation_sound != null:
		_congratulation_sound.play()
