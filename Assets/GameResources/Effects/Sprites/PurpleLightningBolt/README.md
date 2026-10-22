# 紫色雷电飞弹：飞行与消散

两个独立预设，均为 14 FPS，可分别实例化，无需 Animator 参数或状态切换。

| 用途 | 预设 | 播放行为 |
| --- | --- | --- |
| 飞行 | `Assets/Prefabs/Effects/PurpleLightningBolt_Flight.prefab` | 第 1～8 帧持续循环，由调用方结束或销毁 |
| 消散 | `Assets/Prefabs/Effects/PurpleLightningBolt_Dissolve.prefab` | 从第 9 帧立即开始，播放至第 16 帧；约 0.5 秒后自动销毁 |

- 两个预设分别使用同名独立 Animator Controller，每个控制器只有一个动画状态。
- 飞行预设没有 `OneShotSpriteEffect`；消散预设使用该组件，在透明末帧事件中销毁自身。
- 原 `PurpleLightningBolt.prefab` 已更名为飞行预设，并保留其 GUID；原合并控制器已移除。
- 飞行预设附带 LightningProjectile。独立实例默认原地循环，调用 Launch 后执行最多 2.5 单位的飞行、命中判定与消散；角色第一段攻击已接入。消散预设保持独立。
- 统一帧尺寸 444×222、100 PPU、中心锚点（222,111），完整画幅为 4.44×2.22 Unity 单位，Sorting Order 为 20。两个预设在相同位置、旋转和缩放下对齐。
- Point 过滤、无 Mipmap、无压缩、Full Rect、透明 Alpha。未逐帧裁切或居中，保留素材原有运动；仅清除 Alpha≤1 的残留，末帧彻底透明。
- `Source/purple_lightning_bolt_source.png` 保留原图。原图 1774×887，取整网格切分后向右/下补透明像素，未拉伸。
- 构建菜单：`Tools/Effects/Build Purple Lightning Bolt`，目标预设或控制器已存在时拒绝覆盖。
- 验证菜单：`Tools/Effects/Validate Purple Lightning Bolt`，渲染与验证结果输出至 `Library/PurpleLightningBoltValidation`。

## 在飞行结束的位置播放消散

```csharp
// 飞行时由业务逻辑移动 flight.transform。
GameObject flight = Instantiate(flightPrefab, position, Quaternion.identity);

// 命中或结束时创建独立消散实例，保持同一位置、朝向和缩放。
GameObject dissolve = Instantiate(dissolvePrefab, flight.transform.position,
    flight.transform.rotation, flight.transform.parent);
dissolve.transform.localScale = flight.transform.localScale;
SpriteRenderer from = flight.GetComponent<SpriteRenderer>();
SpriteRenderer to = dissolve.GetComponent<SpriteRenderer>();
to.flipX = from.flipX;
to.flipY = from.flipY;
to.color = from.color;
to.sortingLayerID = from.sortingLayerID;
to.sortingOrder = from.sortingOrder;
Destroy(flight);
// dissolve 播放完毕会自行销毁。
```

消散从固定的第 9 帧开始，原素材按第 8→9 帧衔接设计。在任意飞行帧结束时，电弧形状可能有变化；两个预设不会自动匹配当前电弧形态。
