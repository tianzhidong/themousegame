extends Label

const STORY_TITLES := {
	1: "花园醒来啦",
	2: "农场大丰收",
	3: "湖畔奇妙夜",
	4: "野餐准备好",
	5: "小小修理站",
	6: "生日派对",
	7: "树屋探秘",
	8: "奇妙玩具屋",
	9: "实验室整理员",
	10: "风吹乱的小镇",
	11: "河畔寻宝记",
	12: "森林欢乐节",
}


func _ready() -> void:
	text = STORY_TITLES.get(GameState.selected_day, text)
