extends Node

var selected_day := 1
var selected_friend_id := "bear"
var settings_return_scene_path := "res://scenes/main/Main.tscn"

const FRIEND_IDLE_TEXTURE_PATHS := {
	"bear": "res://assets/images/friends/bear/bear_captain_idle.png",
	"rabbit": "res://assets/images/friends/rabbit/rabbit_engineer_idle.png",
	"cat": "res://assets/images/friends/cat/cat_inventor_idle.png",
}


func set_selected_day(day: int) -> void:
	selected_day = clampi(day, 1, 15)


func set_selected_friend(friend_id: String) -> void:
	if not FRIEND_IDLE_TEXTURE_PATHS.has(friend_id):
		push_error("Unknown friend id: %s" % friend_id)
		return

	selected_friend_id = friend_id


func get_selected_friend_idle_texture_path() -> String:
	return FRIEND_IDLE_TEXTURE_PATHS.get(selected_friend_id, FRIEND_IDLE_TEXTURE_PATHS["bear"])


func get_selected_level_scene_path() -> String:
	return "res://scenes/levels/level_%d.tscn" % selected_day


func get_selected_lesson_scene_path() -> String:
	var lesson_days := {
		1: "res://scenes/lesson/lesson1.tscn",
		2: "res://scenes/lesson/lesson2.tscn",
		4: "res://scenes/lesson/lesson4.tscn",
		7: "res://scenes/lesson/lesson7.tscn",
		8: "res://scenes/lesson/lesson8.tscn",
	}

	return lesson_days.get(selected_day, "")


func get_selected_start_scene_path() -> String:
	var lesson_path := get_selected_lesson_scene_path()
	if not lesson_path.is_empty():
		return lesson_path

	return get_selected_level_scene_path()


func set_settings_return_scene(scene_path: String) -> void:
	if scene_path.is_empty():
		settings_return_scene_path = "res://scenes/main/Main.tscn"
		return

	settings_return_scene_path = scene_path


func get_settings_return_scene() -> String:
	return settings_return_scene_path
