# Day12 主页编辑

打开 `scenes/levels/level_12.tscn`，全部8组40项都在这一个场景里完成。原来的8个项目场景已删除，修改前的文件保存于 `output/day12_assets/legacy_scene_backup`，后缀为 `.bak`。

## 调整位置

- `Day12Bg/Attractions` 下8个建筑保持原来调整的位置和大小。建筑和人物随背景一起滚动。
- 每个建筑的 `Targets/Target1` 至 `Target5` 是5个可操作物品。移动 Target 调整整个物品，缩放或移动其 `ObjectSprite` 调整图片。
- 彩蛋和礼盒的 `RevealedSprite` 是打开后的图片，已按关闭素材的实际底部中心对齐。调整 `ObjectSprite` 的位置后，运行时打开素材会跟随关闭素材的落脚点；打开动画也保持底部固定。可临时显示 `RevealedSprite` 调整大小、旋转，调整完恢复隐藏；礼盒中的 `GiftItem` 和前挡板 `BoxFront` 已保留，会随打开的礼盒一起移动。
- 美食街和火车的 `DropTargets/DropTarget1` 至 `DropTarget5` 对应同编号物品。移动 DropTarget 可整体调整接收位置；`DropArea` 决定接收范围，`LandingPoint` 决定放下后的中心位置，`GuideIcon` 是匹配小图案。
- 美食街的 `Plate` 是可单独调整的餐盘。火车的 `FestivalTrain/FestivalTrainForeground` 是你提供的透明车厢前景，保留原来画面中的位置和大小，随火车一起移动。行李拖动时在前景上层，放好后下部被前景遮挡。旧的 `CarriageFronts` 已替换；图片使用英文名 `festival_train_foreground.png`，原图保留。
- 调整行李露出高度时，移动对应 `DropTargets/DropTarget1` 至 `DropTarget5` 下的 `LandingPoint`：向上移动会露出更多。`DropArea` 和 `GuideIcon` 保留你调整过的位置，图案位于前景上方。
- 树屋的 `ObjectSprite` 是关闭的窗户，`RevealedSprite` 保留你调整后的动物位置。`DoorLeft`、`DoorRight` 是开窗动画，`WindowInterior` 是窗内暗部，也是实际双击范围；各层关闭画面和窗扇已按该层校正后的窗洞对齐。整体移动一扇窗请移动对应 `Target`，其中的窗洞、窗扇和动物会一起移动。单独修改窗洞形状后，需要同步修改关闭画面与窗扇。
- 水族馆动物的 `ObjectSprite/BubbleOrigin` 是吐泡泡的位置，可单独移动到嘴边。
- 彩带根据当前物品位置生成，无须另设固定坐标。食物和行李成功放置后，彩带出现在落点。

请保留节点名称以及建筑上的 `task_id`。运行时读取场景中实际位置，不重建排版。默认有少量随机大小、旋转和轻微摆动，可在 Level12 根节点调整参数。

## 操作

- 点击5个气球；点击5种乐器，可继续点击已完成的乐器演奏。
- 双击5颗彩蛋、5个礼盒。
- 将5份食物、5件行李拖到图案相同的餐盘、车厢。
- 双击树屋5扇窗户，打开窗户找出动物；点击水族馆5种动物吐泡泡，完成后仍可再次点击。
- 全程滚轮滚动同一张主页。滚动只用于浏览，不再自动计入树屋或水族馆任务。
- 40项完成后使用原有结果界面；设置、返回和主页任务栏继续使用原有入口。

空底图位于 `assets/images/objects/day12/park_bases`，原始建筑素材保留。图片由原生 image_gen 编辑；完整提示词和生成记录在 `output/day12_assets/park_base_generation_prompts.md`、`park_base_manifest.json`。窗户与前挡板使用 Godot 原生图层，未重新绘制建筑。请勿重跑一次性迁移脚本，以免覆盖后续手工修改。
