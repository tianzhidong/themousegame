# 第11天素材：河畔寻宝记

与 `assets/images/backgrounds/day11_bg.png` 配套。8 张透明 PNG 图集，52 个独立 Godot `AtlasTexture` 资源。将对应 `.tres` 拖入 Texture 属性即可使用。

| 任务组 | 资源 | 数量 |
| --- | --- | --- |
| 双击展开线索卷轴 | `scroll_*_closed/open` | 5 对开合状态 |
| 双击移开石头 | `rock_*` | 5 |
| 拖拽收纳宝石 | `gem_*` | 5 |
| 拖拽拨开落叶堆 | `leaf_pile_*` | 5 |
| 点击收集金币 | `coin_*` | 5 |
| 双击打开漂流瓶 | `bottle_*_closed/open` | 5 对开合状态 |
| 点击收集地图碎片 | `map_fragment_01` 至 `05` | 5 |
| 拖拽拼图 | 复用上述5块地图碎片 | 5 |

配件：`gem_collection_tray`（5个空收藏格）、`map_assembly_board`（空拼图板）、`coin_pouch`（金币袋）、`map_fragment_roll`（瓶中纸卷的可选独立显示素材）、`treasure_map_complete`（完整地图参考）。

最终大宝箱仅有一个，`treasure_chest_closed` 与 `treasure_chest_open` 是同一宝箱的两种状态，属于完成8组任务后的奖励，不增加任务组。

## 卷轴对应关系

| 卷轴/宝石颜色 | 提示石头 |
| --- | --- |
| 红 | 苔藓圆石 `rock_moss_round` |
| 蓝 | 白色斜纹石 `rock_white_stripe` |
| 黄 | 双尖峰石 `rock_twin_peak` |
| 绿 | 扁平椭圆石 `rock_flat_oval` |
| 紫 | 带粉色晶体的石头 `rock_pink_crystal` |

卷轴用“对应石头 → 下方宝石”的图案说明藏物关系，没有文字。真实物品需要分布到长背景不同位置，滚轮用于上下寻找，不设独立滚轮机关。遮挡物应放在隐藏物上层；移开石头、拨开落叶只需移动独立素材，不需要修改背景。

## 状态与拼图

- 卷轴、漂流瓶的开合状态分别统一虚拟画布尺寸并对齐底部；宝箱状态同样对齐底部。状态切换时保留节点位置与缩放，显示细节可在接入时微调。
- 地图保持森林小路、落叶空地、木桥与南端小岛的地理关系，红色路线终点在小岛。
- 5块碎片从同一张完整地图定义水平矩形裁切区域，没有重新绘制，可按照 `manifest.json` 中的 `assembly_offset` 无缝拼合。瓶子内部纸卷是示意，点击收集时显示实际对应碎片。
- 接入时给重复物品少量随机旋转、缩放或亮度变化；卷轴提示与石头的识别特征必须保留。

完整预览：`output/day11_assets/preview.html`。资源区域、状态对齐、对应关系和任务清单：`output/day11_assets/manifest.json`。

使用内置 image_gen 生成；原始提示词：`output/day11_assets/prompts.json`；透明背景清理提示词：`output/day11_assets/cleanup_prompts.json`；生成来源：`output/day11_assets/generated_sources.json`。

## 关卡接入

已接入 `scenes/levels/level_11.tscn`，逻辑为 `scripts/days/Day11Treasure.cs`，8组任务按 `data/days/day_11.json` 的顺序进行。

- `Day11Bg/TreasureObjects` 使用原背景像素坐标，所有卷轴、石头、落叶、瓶子及宝箱随背景一起滚动。可在 Godot 编辑器中直接调整各 Sprite2D 的位置、尺寸；代码读取保存后的节点布局。
- `AdventureHud/GemTray` 是固定在屏幕下方的收藏格；`AdventureHud/MapPuzzle` 保存5块拼图和对应目标位置，可编辑。
- 双击卷轴弹出放大的线索，点击“记住线索，继续寻找”返回。打开过的卷轴可以再次点击查看。
- 宝石及拼图只有放入对应位置才计数；落叶需要实际拖离原位再松开。无效放置会回到原处。
- 完成8组后，上下滚动找到小岛上的唯一宝箱，双击打开，随后显示原有结算界面。
- 场景根节点 Inspector 可调整滚动步长、滚动时间、吸附距离、落叶拖动距离、石头移动偏移、动作时长及随机变化幅度。

打开 `level_11.tscn` 后按 F6 可直接试玩，无需额外配置 Inspector。界面效果和实际操作由用户试玩确认。
