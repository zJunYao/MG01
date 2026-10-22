# 雷云特效 Thundercloud

- 预制体：`Assets/Prefabs/Effects/Thundercloud.prefab`
- 动画：`Assets/Animations/Effects/Thundercloud.anim`
- 控制器：`Assets/Animations/Effects/Thundercloud.controller`
- 将预制体拖入场景，进入 Play 模式自动播放一次，结束后自动销毁。
- 也可以在运行时使用 `Instantiate(thundercloudPrefab, position, Quaternion.identity)` 播放。
- 23 帧有效画面，每帧 181 × 362，RGBA 透明背景；总时长 1.72 秒。
- 100 PPU，底部中心锚点，完整画幅为 1.81 × 3.62 Unity 单位。Transform.position 为画幅底部中心，可用于对齐落雷位置。
- Point 过滤、无 Mipmap、无压缩，保留像素风和透明光晕。SpriteRenderer 默认排序值为 20。
- 最后切换透明帧，通过项目现有的 OneShotSpriteEffect 组件清理对象。
- `thundercloud_source.png` 保留原始 6 列 × 4 行序列图；最后一个格子为空，不作为有效帧。
- Unity 菜单 `Tools/Effects/Validate Thundercloud` 可检查导入和逐帧播放，并在 Library/ThundercloudValidation 输出验证结果和渲染图。
- `Tools/Effects/Build Thundercloud` 用于从帧素材重新构建；已有动画或预制体时会拒绝覆盖。

## 第三段攻击接入

- Role.prefab 第三段攻击开始时，从第 1 帧播放完整聚云、蓄电过程，以 1.425 倍速将素材的 1.14 秒触地帧对齐人物 0.8 秒砸地帧；`OnThirdAttackThundercloud` 在砸地当帧校准落雷画面。连续按 J 三次即可完成三段连击。
- 起手时在预计落点预播雷云；砸地时以角色位置为圆心，在 XY 平面重新检测半径 4.5 Unity 单位的圆与敌人受击框是否相交，并校正落点，包含身后敌人和边界。怪物身体已进入范围时，即使脚底点仍在圆外，也会选中并触发受击。
- 范围内每个有效敌人脚下各生成一个雷云；无有效敌人时，在第三段攻击开始时的面朝方向、距离角色 4.5 Unity 单位处生成一个。
- 敌人根节点挂载 `EnemyTarget`，每个敌人挂一次。`Feet` 可指定脚底子节点；未指定时将敌人根节点视为脚底。无需 Collider 或 Enemy Tag。
- 敌人死亡或不可被选中时，禁用 EnemyTarget 或敌人对象即可排除；砸地前进出范围会影响本次选敌。
- 聚云期间保持预计落点，砸地时更新到有效敌人脚下，此后固定于世界坐标。若敌人数量改变，复用已聚集的云，补足或移除多余云。砸地前中断攻击会清除预播雷云，已释放的落雷独立播放完毕。
- `Thundercloud Ground Offset = -0.42` 将距画幅底边约 42 像素的触地光斑对齐地面，偏移随 `Thundercloud Scale` 同步缩放。完整攻击用雷云约 1.21 秒（起手至消散）后清理，独立预制体保留原速完整动画。
- 砸地命中时调用 EnemyTarget.ReceiveHit，触发怪物受击动画；当前没有血量/扣血系统。
- `Tools/Effects/Connect Third Attack Thundercloud` 可重新连接角色预制体和动画事件；`Tools/Effects/Validate Third Attack in Play Mode` 可运行范围与事件验证。
