extends ItemList

const CLICK_SOUND := preload("res://assets/audio/ui/ui_click.ogg")

@export var rows := 3
@export var columns := 5
@export var spacing := Vector2(24.0, 24.0)
@export var button_text_prefix := "第"
@export var button_text_suffix := "日"

var _buttons: Array[Button] = []
var _click_player: AudioStreamPlayer


func _ready() -> void:
	_click_player = AudioStreamPlayer.new()
	_click_player.stream = CLICK_SOUND
	add_child(_click_player)

	clear()
	_create_buttons()
	resized.connect(_layout_buttons)
	call_deferred("_layout_buttons")


func _create_buttons() -> void:
	for button in _buttons:
		button.queue_free()

	_buttons.clear()

	for index in range(rows * columns):
		var button := Button.new()
		button.name = "ItemButton%d" % (index + 1)
		button.text = "%s%d%s" % [button_text_prefix, index + 1, button_text_suffix]
		button.clip_text = true
		button.button_down.connect(_play_click_sound)
		add_child(button)
		_buttons.append(button)


func _layout_buttons() -> void:
	if rows <= 0 or columns <= 0:
		return

	var total_spacing := Vector2(
		spacing.x * float(columns - 1),
		spacing.y * float(rows - 1)
	)
	var button_size := Vector2(
		maxf((size.x - total_spacing.x) / float(columns), 1.0),
		maxf((size.y - total_spacing.y) / float(rows), 1.0)
	)

	for index in range(_buttons.size()):
		var row := int(index / columns)
		var column := index % columns
		var button := _buttons[index]
		button.position = Vector2(
			float(column) * (button_size.x + spacing.x),
			float(row) * (button_size.y + spacing.y)
		)
		button.size = button_size


func _play_click_sound() -> void:
	_click_player.play()
