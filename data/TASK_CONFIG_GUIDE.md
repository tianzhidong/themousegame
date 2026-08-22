# 每日任务配置说明

每日任务保存在 `res://data/days/day_XX.json`。通常只需要修改对应天数的 JSON，不需要修改 C#。

## 文件和基础字段

```json
{
  "day_id": 3,
  "day_title": "花园醒来啦",
  "task_mode": "sequence",
  "time_limit_seconds": 900,
  "tasks": []
}
```

- `day_id`：天数，必须与文件名一致。例如 `day_03.json` 必须填写 `3`。
- `day_title`：当天标题。
- `task_mode`：`sequence` 表示依次完成；`parallel` 表示多个任务同时激活。
- `time_limit_seconds`：训练时间配置，建议不超过 `900` 秒。目前用于记录配置，不会到时判定失败。

Godot 保存 JSON 时可能把整数写成 `1.0`。任务加载器同时兼容 `1` 和 `1.0`，但不接受 `1.5` 这样的非整数值。

## 如何修改任务

1. 修改任务栏文字：修改任务的 `text`。
2. 修改任务图标：修改 `icon_path`，必须使用存在的 `res://` 图片路径。
3. 修改目标类型：修改 `target_type`，例如 `flower`、`butterfly`、`light`。
4. 修改目标颜色：修改 `target_color`，例如 `red`、`blue`、`yellow`；空字符串表示不限颜色。
5. 修改任务数量：修改 `target_count`，必须大于 0。现有任务栏每行有 5 个状态槽，建议配置为 1–5。
6. 修改计数模式：`per_action` 表示每次正确操作计数；`distinct_object` 表示同一个 `object_id` 最多计数一次。
7. 增加新任务：复制一个任务对象，修改唯一 `id`，并把 `order` 调整为从 1 开始且连续、不重复。
8. 增加新的一天：复制现有 JSON 为 `day_XX.json`，修改 `day_id`、标题和任务。对应 `level_XX.tscn` 仍需存在并实例化现有 `taskbar.tscn`。
9. 图片和音频路径：使用 `res://` 开头的完整项目路径。没有引导资源时，`hint_image_path` 和 `hint_audio_path` 填空字符串。

## 任务字段

- `id`：任务唯一 ID。
- `text`：顶部当前任务区域显示的文字。
- `icon_path`：当前任务区域和任务行使用的图标。
- `operation`：`move`、`click`、`drag`、`double_click`、`wheel` 之一。
- `target_type`：目标对象类型；空字符串表示不限。
- `target_color`：目标颜色；空字符串表示不限。
- `target_tag`：目标标签；空字符串表示不限。
- `target_ids`：只允许指定对象 ID；空数组表示不限 ID。
- `target_count`：完成所需进度。
- `progress_style`：`petals`、`dots`、`bar`。当前 UI 的 `petals` 和 `dots` 都复用已有 5 个状态槽；当前没有独立进度条节点。
- `progress_mode`：`per_action` 或 `distinct_object`。
- `order`：匹配优先级和顺序，必须从 1 连续排列。
- `hint_image_path`：引导图片路径，可为空。
- `hint_audio_path`：引导音频路径，可为空。任务栏只保存路径，不负责播放引导。

## 为什么其他对象不会响应

交互物在播放动画、音效或改变状态之前，会先把 `operation`、`object_id`、`object_type`、`object_color` 和 `object_tags` 交给统一的 `InteractionGate`。只有与当前 Active 任务全部条件匹配时，`TaskManager` 才接受事件并增加进度。验证失败的对象仍然显示，但不会播放成功表现，也不会影响进度。

## JSON 错误排查

运行时查看 Godot Output。错误会包含配置文件路径和字段名。重点检查：

- 逗号、引号和括号是否完整。
- `day_id` 是否与文件名一致。
- `id` 和 `order` 是否重复。
- `order` 是否从 1 连续排列。
- `target_count` 是否大于 0。
- 操作、进度样式和计数模式是否拼写正确。
- 所有非空资源路径是否真实存在。

配置验证失败时，系统进入空任务安全状态，不会继续显示上一天的任务。

## 完整的第 3 天示例

```json
{
  "day_id": 3,
  "day_title": "花园醒来啦",
  "task_mode": "sequence",
  "time_limit_seconds": 900,
  "tasks": [
    {
      "id": "click_red_flowers",
      "text": "点击红色花朵",
      "icon_path": "res://assets/images/objects/lesson/red_flower_open.png",
      "operation": "click",
      "target_type": "flower",
      "target_color": "red",
      "target_tag": "garden_flower",
      "target_ids": [],
      "target_count": 5,
      "progress_style": "petals",
      "progress_mode": "distinct_object",
      "order": 1,
      "hint_image_path": "",
      "hint_audio_path": "res://assets/audio/voice/lesson/单击左键.ogg"
    },
    {
      "id": "click_blue_butterflies",
      "text": "点击蓝色蝴蝶",
      "icon_path": "res://assets/images/objects/lesson/butterfly.png",
      "operation": "click",
      "target_type": "butterfly",
      "target_color": "blue",
      "target_tag": "garden_butterfly",
      "target_ids": [],
      "target_count": 3,
      "progress_style": "dots",
      "progress_mode": "distinct_object",
      "order": 2,
      "hint_image_path": "",
      "hint_audio_path": ""
    }
  ]
}
```
