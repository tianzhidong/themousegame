extends Label

const STORY_TITLES := {
	1: "你好，森林小镇",
	2: "蝴蝶带路",
	3: "花园醒来啦",
	4: "找找是谁藏起来了",
	5: "野餐篮准备好了吗",
	6: "朋友们的礼物",
	7: "一起布置小屋",
	8: "高高的树屋",
	9: "神秘礼物盒",
	10: "准备森林野餐",
	11: "湖边寻宝记",
	12: "风吹乱了小镇",
}


func _ready() -> void:
	text = STORY_TITLES.get(GameState.selected_day, text)
