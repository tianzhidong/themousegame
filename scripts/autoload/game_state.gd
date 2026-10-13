extends Node

var selected_day := 1
var selected_friend_id := "bear"
var selected_day5_module := 1
var day5_repaired_parts: Dictionary = {}
const DAY5_TASK_TYPES := {
	1: "machineC", 2: "machineD", 3: "machineE", 4: "machineB",
	5: "machineA", 6: "machineF", 7: "machineG", 8: "machineH"
}
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


func set_selected_day5_module(module_index: int) -> void:
	selected_day5_module = clampi(module_index, 1, 8)


func get_selected_day5_module() -> int:
	return selected_day5_module


func get_day5_task_type(module_index: int) -> String:
	return DAY5_TASK_TYPES.get(module_index, "")


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
