using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Test.Editor
{
    /// <summary>将已有怪物预制体加入原观察场景，不重新生成背景或主角。</summary>
    public static class MonsterSceneSetup
    {
        [MenuItem("测试/角色/验证怪物先移动再攻击")]
        public static void ValidateAttackApproach()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!EditorApplication.isPlaying || scene.path != "Assets/Test/Scenes/HeroPreview.unity")
                throw new InvalidOperationException("请在播放模式下打开主角观察场景。");
            MonsterPreview panel = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MonsterPreview>(true)).Single();
            var snapshots = panel.actors.Select(actor => new { actor, actor.selected, actor.paused, actor.repeat,
                actor.speed, actor.time, actor.movingToAttack, actor.approachTime,
                actor.returning, actor.attackCycleActive, actor.returnPosition,
                position = actor.animator.transform.position }).ToArray();
            bool originalLock = panel.lockToViewport;
            try
            {
                foreach (var snapshot in snapshots) snapshot.actor.paused = true;
                Vector3[] expectedTargets = { new Vector3(-2.29f, -0.36f, 0f),
                    new Vector3(-2.248f, -0.248f, 0f), new Vector3(-2.47f, -0.35f, 0f) };
                for (int i = 0; i < panel.actors.Length; i++)
                {
                    MonsterPreview.Actor actor = panel.actors[i];
                    if ((actor.attackPosition - expectedTargets[i]).sqrMagnitude > 0.000001f)
                        throw new InvalidOperationException("攻击坐标与截图不一致：" + actor.label);
                    Transform model = actor.animator.transform;
                    panel.SelectAction(actor, 0);
                    model.position = actor.attackPosition + Vector3.right * 2f;
                    Vector3 origin = model.position;
                    actor.repeat = false;
                    actor.speed = 1f;
                    panel.SelectAction(actor, 1);
                    var renderer = actor.animator.GetComponent<SpriteRenderer>();
                    var idleBinding = AnimationUtility.GetObjectReferenceCurveBindings(actor.clips[0]).Single();
                    var idleStart = AnimationUtility.GetObjectReferenceCurve(actor.clips[0], idleBinding)[0].value;
                    var walkBinding = AnimationUtility.GetObjectReferenceCurveBindings(actor.clips[2]).Single();
                    var walkFrames = AnimationUtility.GetObjectReferenceCurve(actor.clips[2], walkBinding);
                    if (!actor.movingToAttack || actor.time != 0f || renderer.sprite != walkFrames[0].value)
                        throw new InvalidOperationException("移动前提前播放攻击：" + actor.label);
                    actor.paused = true;
                    Vector3 beforePause = model.position;
                    panel.AdvancePlayback(1f);
                    if (model.position != beforePause) throw new InvalidOperationException("暂停时仍在移动。");
                    actor.paused = false;
                    actor.speed = 0.5f;
                    float moveSpeed = Mathf.Max(0.01f, actor.moveSpeed);
                    panel.AdvancePlayback(1f / moveSpeed);
                    if (Mathf.Abs(Vector3.Distance(model.position, actor.attackPosition) - 1.5f) > 0.001f
                        || !actor.movingToAttack || actor.time != 0f)
                        throw new InvalidOperationException("移动速度或攻击等待状态不正确：" + actor.label);
                    if (!walkFrames.Any(key => key.value == renderer.sprite))
                        throw new InvalidOperationException("去程未播放走路动画。");
                    float walkProgress = Mathf.Repeat(actor.approachTime, actor.clips[2].length);
                    UnityEngine.Object expectedWalk = walkFrames.Last(key => key.time <= walkProgress).value;
                    if (renderer.sprite != expectedWalk || walkProgress <= 0f)
                        throw new InvalidOperationException("走路未随位移时间推进。");
                    panel.lockToViewport = true;
                    Vector3 beforeLayout = model.position;
                    panel.SendMessage("LateUpdate");
                    if (model.position != beforeLayout) throw new InvalidOperationException("视口定位覆盖了攻击移动。");
                    panel.lockToViewport = false;
                    actor.speed = 1f;
                    panel.AdvancePlayback(10f / moveSpeed);
                    var attackBinding = AnimationUtility.GetObjectReferenceCurveBindings(actor.clips[1]).Single();
                    var attackStart = AnimationUtility.GetObjectReferenceCurve(actor.clips[1], attackBinding)[0].value;
                    if (actor.movingToAttack || actor.time != 0f || model.position != actor.attackPosition
                        || renderer.sprite != attackStart)
                        throw new InvalidOperationException("到达后未从攻击首帧开始：" + actor.label);
                    panel.AdvancePlayback(0.1f);
                    if (actor.time <= 0f) throw new InvalidOperationException("到达后攻击未推进。");
                    panel.AdvancePlayback(actor.clips[1].length);
                    if (!actor.returning || model.position != actor.attackPosition)
                        throw new InvalidOperationException("攻击完成后未进入返程。");
                    actor.paused = true;
                    panel.AdvancePlayback(1f);
                    if (model.position != actor.attackPosition) throw new InvalidOperationException("返程暂停无效。");
                    actor.paused = false;
                    actor.speed = 0.5f;
                    panel.AdvancePlayback(1f / moveSpeed);
                    if (Mathf.Abs(Vector3.Distance(model.position, actor.attackPosition) - 0.5f) > 0.001f)
                        throw new InvalidOperationException("返程倍速无效。");
                    if (!walkFrames.Any(key => key.value == renderer.sprite))
                        throw new InvalidOperationException("返程未播放走路动画。");
                    actor.speed = 1f;
                    panel.AdvancePlayback(10f / moveSpeed);
                    if (model.position != origin || actor.returning || actor.attackCycleActive || actor.selected != 0)
                        throw new InvalidOperationException("单次攻击未返回原位并待机。");
                    actor.repeat = true;
                    panel.SelectAction(actor, 1);
                    panel.AdvancePlayback(10f / moveSpeed);
                    panel.AdvancePlayback(actor.clips[1].length);
                    panel.SelectAction(actor, 1);
                    if (actor.returnPosition != origin) throw new InvalidOperationException("攻击中重播覆盖了出发位置。");
                    panel.AdvancePlayback(10f / moveSpeed);
                    panel.AdvancePlayback(actor.clips[1].length);
                    panel.AdvancePlayback(10f / moveSpeed);
                    if (model.position != origin || !actor.movingToAttack || actor.returning || actor.time != 0f)
                        throw new InvalidOperationException("循环攻击未在返回原位后重新出发。");
                    // 分别检查去程、攻击中、返程的面板暂停及重新出发。
                    for (int phase = 0; phase < 3; phase++)
                    {
                        panel.SelectAction(actor, 1);
                        panel.AdvancePlayback(phase == 0 ? 0.5f / moveSpeed : 10f / moveSpeed);
                        if (phase == 1) panel.AdvancePlayback(0.1f);
                        if (phase == 2)
                        {
                            panel.AdvancePlayback(actor.clips[1].length);
                            panel.AdvancePlayback(0.5f / moveSpeed);
                        }
                        panel.SetPaused(actor, true);
                        if (!actor.paused || model.position != origin || actor.returning
                            || !actor.movingToAttack || actor.time != 0f || renderer.sprite != idleStart)
                            throw new InvalidOperationException("暂停未立即回到原位并复位动作：" + actor.label);
                        panel.AdvancePlayback(1f);
                        panel.SendMessage("LateUpdate");
                        if (model.position != origin) throw new InvalidOperationException("暂停后位置未保持。");
                        panel.SetPaused(actor, false);
                        panel.AdvancePlayback(0.5f / moveSpeed);
                        if (!actor.movingToAttack || actor.time != 0f || model.position == origin)
                            throw new InvalidOperationException("取消暂停未重新出发。");
                    }
                    panel.SelectAction(actor, 1);
                    panel.SelectAction(actor, 0);
                    if (actor.movingToAttack) throw new InvalidOperationException("切换待机未取消移动。");
                    actor.paused = true;
                }
                panel.ReplayAll(true);
                if (panel.actors.Any(actor => !actor.movingToAttack || actor.time != 0f))
                    throw new InvalidOperationException("全部攻击未统一进入移动流程。");
                Debug.Log("怪物移动攻击验证通过：三个怪物去程和返程播放走路；各阶段暂停回原位显示待机、恢复重新出发；攻击、倍速、循环及重播正常。");
            }
            finally
            {
                panel.lockToViewport = originalLock;
                foreach (var snapshot in snapshots)
                {
                    panel.SelectAction(snapshot.actor, snapshot.selected);
                    snapshot.actor.paused = snapshot.paused;
                    snapshot.actor.repeat = snapshot.repeat;
                    snapshot.actor.speed = snapshot.speed;
                    snapshot.actor.time = snapshot.time;
                    snapshot.actor.movingToAttack = snapshot.movingToAttack;
                    snapshot.actor.approachTime = snapshot.approachTime;
                    snapshot.actor.returning = snapshot.returning;
                    snapshot.actor.attackCycleActive = snapshot.attackCycleActive;
                    snapshot.actor.returnPosition = snapshot.returnPosition;
                    snapshot.actor.animator.transform.position = snapshot.position;
                }
                panel.AdvancePlayback(0f);
            }
        }

        [MenuItem("测试/角色/验证场景角色位置可调整")]
        public static void ValidateEditablePositions()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!EditorApplication.isPlaying || scene.path != "Assets/Test/Scenes/HeroPreview.unity")
                throw new InvalidOperationException("请在播放模式下打开主角观察场景。");
            HeroPreview preview = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<HeroPreview>(true)).Single();
            MonsterPreview monsters = preview.GetComponent<MonsterPreview>();
            if (preview.lockToViewport || monsters.lockToViewport)
                throw new InvalidOperationException("请先关闭主角和怪物的视口站位锁定。");
            Transform[] models = new[] { preview.hero.transform }
                .Concat(monsters.actors.Select(actor => actor.animator.transform)).ToArray();
            Vector3[] original = models.Select(model => model.position).ToArray();
            Vector3 offset = new Vector3(0.4f, 0.2f, 0f);
            Camera camera = preview.previewCamera;
            float originalZoom = camera.orthographicSize;
            try
            {
                for (int i = 0; i < models.Length; i++) models[i].position = original[i] + offset;
                camera.orthographicSize = originalZoom + 0.25f;
                // 主动重复动画采样与定位逻辑，不依赖编辑器失焦时的刷新频率。
                for (int sample = 0; sample < 5; sample++)
                {
                    preview.SendMessage("Update");
                    preview.SendMessage("LateUpdate");
                    monsters.SendMessage("Update");
                    monsters.SendMessage("LateUpdate");
                    for (int i = 0; i < models.Length; i++)
                        if ((models[i].position - original[i] - offset).sqrMagnitude > 0.000001f)
                            throw new InvalidOperationException("角色位置被覆盖：" + models[i].name);
                }
                Debug.Log("角色站位验证通过：主角和三个怪物移动后，重复动画采样及镜头缩放不会覆盖 Transform。");
            }
            finally
            {
                // 只恢复本次播放验证的临时位移，不保存或修改场景文件。
                for (int i = 0; i < models.Length; i++)
                    if (models[i]) models[i].position = original[i];
                if (camera) camera.orthographicSize = originalZoom;
            }
        }

        [MenuItem("测试/角色/验证怪物观察场景并预览攻击")]
        public static void Validate()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Test/Scenes/HeroPreview.unity")
                throw new InvalidOperationException("请先打开主角观察场景。");
            MonsterPreview panel = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MonsterPreview>(true)).Single();
            if (!panel.previewCamera || panel.actors.Length != 3)
                throw new InvalidOperationException("怪物场景配置缺失。");
            int frames = 0;
            foreach (MonsterPreview.Actor actor in panel.actors)
            {
                if (!actor.animator || actor.clips.Length != 3 || actor.clips.Any(clip => !clip)
                    || actor.clips[0].name != "Idle_south-west" || actor.clips[1].name != "Attack_south-west"
                    || actor.clips[2].name != "Walk_south-west")
                    throw new InvalidOperationException("怪物动作引用不正确：" + actor.label);
                var sample = new GameObject("怪物动作验证");
                try
                {
                    var renderer = sample.AddComponent<SpriteRenderer>();
                    sample.AddComponent<Animator>().runtimeAnimatorController = actor.animator.runtimeAnimatorController;
                    foreach (AnimationClip clip in actor.clips)
                    {
                        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        foreach (ObjectReferenceKeyframe key in keys)
                        {
                            clip.SampleAnimation(sample, key.time + 0.1f / clip.frameRate);
                            if (!renderer.sprite || renderer.sprite != key.value)
                                throw new InvalidOperationException("动画采样失败：" + actor.label + "/" + clip.name);
                            if (EditorApplication.isPlaying)
                            {
                                actor.animator.speed = 0f;
                                actor.animator.Play(clip.name, 0, (key.time + 0.1f / clip.frameRate) / clip.length);
                                actor.animator.Update(0f);
                                if (actor.animator.GetComponent<SpriteRenderer>().sprite != key.value)
                                    throw new InvalidOperationException("场景动画状态采样失败：" + actor.label + "/" + clip.name);
                            }
                            frames++;
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(sample); }
            }
            if (EditorApplication.isPlaying) panel.ReplayAll(true);
            Debug.Log("怪物观察场景验证通过：3 个模型、9 个动作、" + frames + " 帧引用与采样正常。" +
                (EditorApplication.isPlaying ? "场景 Animator 逐帧检查通过，已触发全部怪物攻击。" : "进入播放模式后可验证场景 Animator 并预览攻击。"));
        }

        [MenuItem("测试/角色/将三个怪物加入观察场景")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Test/Scenes/HeroPreview.unity" || scene.isDirty)
                throw new InvalidOperationException("请在非播放模式下打开并保存主角观察场景。");
            HeroPreview preview = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<HeroPreview>(true)).Single();
            if (preview.GetComponent<MonsterPreview>())
                throw new InvalidOperationException("三个怪物已配置，不重复创建或覆盖手工站位。");
            string[] ids = { "GoblinWarrior", "GoblinThief", "GoblinChief" };
            string[] names = { "战士", "盗贼", "酋长" };
            // 参考图中右侧上、中下、右侧三个红框的脚底位置。
            Vector2[] feet = { new Vector2(0.72f, 0.36f), new Vector2(0.755f, 0.19f), new Vector2(0.865f, 0.31f) };
            Vector3[] attackPositions = { new Vector3(-2.29f, -0.36f, 0f),
                new Vector3(-2.248f, -0.248f, 0f), new Vector3(-2.47f, -0.35f, 0f) };
            var actors = new MonsterPreview.Actor[3];
            for (int i = 0; i < ids.Length; i++)
            {
                string root = "Assets/GameResources/Characters/" + ids[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/" + ids[i] + ".prefab");
                AnimationClip[] clips = new[] { "Idle_south-west", "Attack_south-west", "Walk_south-west" }
                    .Select(action => AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/Animations/" + action + ".anim")).ToArray();
                if (!prefab || clips.Any(clip => !clip)) throw new InvalidOperationException("怪物资源缺失：" + ids[i]);
                actors[i] = new MonsterPreview.Actor { label = names[i], clips = clips, footPosition = feet[i],
                    attackPosition = attackPositions[i] };
            }
            var panel = Undo.AddComponent<MonsterPreview>(preview.gameObject);
            panel.previewCamera = preview.previewCamera;
            panel.actors = actors;
            for (int i = 0; i < ids.Length; i++)
            {
                string path = "Assets/GameResources/Characters/" + ids[i] + "/" + ids[i] + ".prefab";
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                Undo.RegisterCreatedObjectUndo(model, "添加测试怪物");
                actors[i].animator = model.GetComponent<Animator>();
                var renderer = model.GetComponent<SpriteRenderer>();
                renderer.sortingOrder = Mathf.RoundToInt((1f - feet[i].y) * 100f);
                Vector3 foot = preview.previewCamera.ViewportToWorldPoint(new Vector3(feet[i].x, feet[i].y, 10f));
                Vector3 localFoot = MonsterPreview.GetLocalFoot(renderer.sprite);
                model.transform.position = new Vector3(foot.x - localFoot.x, foot.y - localFoot.y, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            }
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("三个怪物已加入观察场景：战士在右上、盗贼在右下、酋长在最右，支持独立待机与攻击调试。");
        }
    }
}
