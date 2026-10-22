# 狼人雷电碎裂死亡

- 素材：用户提供的 1254×1254 RGBA 图集，已整理成 `DeathParticles.png`（256×256、4×4、每格 64×64，Point 过滤）。保留真实透明通道，不做黑色抠图；清除 alpha < 24 的微弱杂点。
- `SpriteDeathShatter.PlayDeath()`：冻结当前 Sprite，立即停止选敌与受击，用两层网格显示原图身体碎块和能量粒子。默认 0.72 秒结束并销毁怪物；重复调用不会重复生成。
- 默认适配本项目未旋转打包的 Full Rect 狼人帧。碎块是大小不一的矩形像素块，不是按头、四肢进行解剖拆分。
- `Assets/Scenes/DeathShatterPreview.unity`：独立预览场景。运行后自动循环；Space 重播，画面按钮切换 0.35 倍慢放。
- 狼人预设已挂载组件。初始血量 30，三段攻击分别扣 2、5、7 点，`EnemyTarget.TakeDamage` 在血量归零时自动调用 `PlayDeath()`；死亡立即停止参与命中，播完后销毁。
- `Tools/Effects/Death Shatter/Setup Preview` 创建材质、连接预设、创建独立场景。
- `Tools/Effects/Death Shatter/Render And Validate` 输出 138 帧 Unity 渲染及检查报告到 `Library/DeathShatterPreview`。检查死亡后不可受击、重复调用、30/60/144 FPS 结束及最终透明。
- 用户预览：项目根目录 `Previews/DeathShatter/preview.html`、MP4、GIF 和阶段图。
