# 受击带电效果

使用用户最终选择的紫蓝色八宫格电弧，保留原配色与粗细。原图位于 `Source/electric_arc_source.png`；`ElectricArcAtlas.png` 为重新居中打包的 1024×512 RGBA 图集，每格四周留透明边距。

## 当前行为

- 狼人预设根节点挂载 `ElectricArcEffect`，自动订阅 `EnemyTarget.HitReceived`。三段攻击的实际命中都会触发。
- 每次受击持续 1.5 秒；重复受击刷新持续时间，复用同一个显示对象。
- Shader 在身体上、中、下区域绘制三组短促闪烁电弧；颜色来自贴图，各实例使用独立随机种子。
- 使用缩放游戏时间。暂停游戏时间时电弧节奏和剩余时长一起停止。
- 到期后关闭 Renderer；怪物或组件禁用时清理状态。EnemyTarget 失效后在下一次 LateUpdate 停止。
- 原有 Walk / Hit 动画、攻击判定和位移逻辑继续运行；本功能是带电视觉，不会锁定移动或攻击。

## 主要资源

- `Assets/Scripts/ElectricArcEffect.cs`：受击订阅、持续时间、显示层与每实例参数。
- `Assets/Shaders/Effects/ElectricArc.shader`：内置管线，加法混合，独立矩形网格上绘制三组电弧。
- `Assets/Materials/Effects/ElectricArc.mat`：所有实例共用的材质。
- `Assets/Prefabs/Effects/ElectricArc.prefab`：无 Collider 的 Quad，默认不显示，在首次播放时实例化。
- `Assets/Prefabs/Monsters/Werewolf.prefab`：已配置组件及 SortingGroup，Demo 中的预设实例自动继承。

## 可调参数

| Inspector 字段 | 默认值 | 说明 |
| --- | --- | --- |
| Duration | 1.5 | 受击带电持续秒数 |
| Body Center | (0, 0.8) | 相对角色根节点的电弧区域中心 |
| Body Size | (1.3, 1.75) | 稳定的电弧覆盖宽高，随角色 Transform 缩放 |
| Arc Size | 0.34 | 单条电弧相对覆盖区域高度的大小 |
| Flash Interval | 0.25 | 闪烁间隔秒数 |
| Flash Seconds | 0.1 | 单次亮起秒数，Shader 限制不超过间隔的 80% |
| Fade Out Seconds | 0.15 | 结束淡出秒数 |
| Sorting Offset | 1 | 相对主体 SpriteRenderer 的组内排序偏移 |
| Play On Hit | true | 是否自动响应同一物体的 EnemyTarget 受击事件 |

电弧只按身体范围分布，不精确沿逐帧透明轮廓爬行。低帧率下短闪可能跳过；需要更稳定显示时可以适当增加 Flash Seconds。

## 复用到主角或其他怪物

在角色根节点添加 `ElectricArcEffect`，指定原 `Visual` 上的 SpriteRenderer，以及 `ElectricArc.prefab`。按身体而非武器伸展范围配置中心和宽高。该组件没有 EnemyTarget 的强制依赖，主角可关闭 Play On Hit，并调用 `Play()`、`Play(seconds)` 或 `Stop()`。

需要多个角色整体遮挡时，在角色根节点设置 SortingGroup，组排序采用原主体的排序。狼人已配置；根节点的统一缩放和 Visual.flipX 均受支持。

## 重建与验证

- `Tools/Effects/Setup Electric Arc`：配置导入参数、生成共用材质和效果预设、接入狼人；保留已有组件调参。
- `Tools/Effects/Validate Electric Arc`：验证实际渲染像素变化、持续时间、重复受击复用、禁用清理、翻转缩放、不同实例共享材质、主角复用和真实 LateUpdate 到期。报告及截图输出到 `Library/ElectricArcValidation`。
- `Tools/Combat/Validate Three Attacks`：现有攻击回归检查同时验证命中后的带电状态，输出到 `Library/CombatAttackValidation`。
- Play 模式下可从组件右键菜单选择 `Play Electric Arc` 单独预览。

素材处理脚本：`temp_images/prepare_electric_arc.py`。原始上传文件已经复制进项目，运行游戏不依赖临时上传路径。
