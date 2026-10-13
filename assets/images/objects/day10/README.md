# Day10 素材

主题：风后整理小镇庭院。与 `assets/images/backgrounds/day10_bg.png` 搭配。

8 张透明 PNG 图集，50 个独立的 Godot `AtlasTexture` 资源。直接将对应 `.tres` 拖到 Godot 的 Texture 属性即可使用。

| 任务组 | 资源前缀 | 数量 |
| --- | --- | --- |
| 点击清理落叶 | `leaf_` | 5 |
| 点击清理纸团 | `paper_ball_` | 5 |
| 拖拽收纳园艺工具 | `tool_` | 5 |
| 双击打开种子袋 | `seed_packet_` | 5 对，共 10 个状态 |
| 拖拽种花 | `seeds_` | 5 |
| 点击浇水后长出的花朵 | `flower_` | 5 |
| 拖拽安装指路牌 | `sign_` | 5 |
| 双击启动风车 | `pinwheel_` | 5 组叶轮和支杆，共 10 个部件 |

种子袋、花种和花朵按红色郁金香、黄色向日葵、蓝色花、粉色雏菊、紫色花对应。花朵没有另画花盆，可叠加到背景的现有空花盆里。背景有 7 个空花盆，任务选用其中 5 个。

种子袋关闭和打开状态使用相同显示尺寸，并对齐底部。风车叶轮使用正方形显示区域，以中心为旋转轴，支杆保持静止。路牌分别采用花朵、面包、湖水、滑梯、树木图案，无文字。

工具中的 `tool_watering_can.tres` 也可复用于浇水反馈。落叶和纸团可在完成点击后隐藏，无需另备消失状态。

物品尺寸、旋转、亮度等随机变化由后续关卡接入时设置；本次仅准备素材资源。

完整预览：`output/day10_assets/preview.html`。资源区域、对齐信息：`output/day10_assets/manifest.json`。生成方式：内置 image_gen；提示词：`output/day10_assets/prompts.json`，透明背景处理提示词：`output/day10_assets/cleanup_prompts.json`。
