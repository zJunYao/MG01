using System;
using UnityEngine;

namespace MG01.Test
{
    /// <summary>三个怪物的独立动作与站位观察，不依赖战斗框架。</summary>
    public sealed class MonsterPreview : MonoBehaviour
    {
        [Serializable]
        public sealed class Actor
        {
            public string label;
            public Animator animator;
            public AnimationClip[] clips;
            [Tooltip("仅在启用视口站位锁定时使用；默认通过怪物 Transform 调整位置。")]
            public Vector2 footPosition;
            [Tooltip("攻击前需要移动到的世界坐标，对应角色 Transform 的 Position。")]
            public Vector3 attackPosition;
            [Min(0.01f), Tooltip("攻击前的移动速度，单位为世界单位/秒；受播放倍速影响。")]
            public float moveSpeed = 4f;
            [NonSerialized] public int selected;
            [NonSerialized] public bool paused;
            [NonSerialized] public bool repeat = true;
            [NonSerialized] public float speed = 1f;
            [NonSerialized] public float time;
            [NonSerialized] public Vector2 localFoot;
            [NonSerialized] public bool movingToAttack;
            [NonSerialized] public float approachTime;
            [NonSerialized] public bool returning;
            [NonSerialized] public bool attackCycleActive;
            [NonSerialized] public Vector3 returnPosition;
        }

        public Camera previewCamera;
        public Actor[] actors;
        [Tooltip("启用后每帧按 Foot Position 恢复怪物站位；关闭时可直接在 Scene 中移动怪物并保存场景。")]
        public bool lockToViewport = false;
        public int SelectedActor { get; private set; } = -1;

        private void Start()
        {
            foreach (Actor actor in actors)
            {
                SpriteRenderer renderer = actor.animator.GetComponent<SpriteRenderer>();
                actor.localFoot = GetLocalFoot(renderer.sprite);
                Sample(actor);
            }
        }

        // 使用待机网格脚底作为固定锚点，攻击特效扩大时不会把怪物整体向下挤。
        public static Vector2 GetLocalFoot(Sprite sprite)
        {
            Vector2 min = sprite.bounds.min;
            Vector2 max = sprite.bounds.max;
            Vector2[] vertices = sprite.vertices;
            if (vertices.Length > 0)
            {
                min = max = vertices[0];
                foreach (Vector2 vertex in vertices)
                {
                    min = Vector2.Min(min, vertex);
                    max = Vector2.Max(max, vertex);
                }
            }
            return new Vector2((min.x + max.x) * 0.5f, min.y);
        }

        private void Update()
        {
            AdvancePlayback(Time.unscaledDeltaTime);
        }

        public void AdvancePlayback(float deltaTime)
        {
            foreach (Actor actor in actors)
            {
                if (!actor.animator.isActiveAndEnabled) continue;
                if (actor.movingToAttack || actor.returning)
                {
                    if (!actor.paused)
                    {
                        actor.approachTime += deltaTime * actor.speed;
                        Transform model = actor.animator.transform;
                        Vector3 destination = actor.returning ? actor.returnPosition : actor.attackPosition;
                        model.position = Vector3.MoveTowards(model.position, destination,
                            Mathf.Max(0.01f, actor.moveSpeed) * deltaTime * actor.speed);
                        if ((model.position - destination).sqrMagnitude <= 0.00000001f)
                        {
                            model.position = destination;
                            actor.time = 0f;
                            if (actor.returning)
                            {
                                actor.returning = false;
                                actor.approachTime = 0f;
                                actor.movingToAttack = actor.repeat;
                                if (!actor.repeat)
                                {
                                    actor.selected = 0;
                                    actor.attackCycleActive = false;
                                }
                            }
                            else actor.movingToAttack = false;
                        }
                    }
                    Sample(actor);
                    continue;
                }
                if (!actor.paused)
                {
                    actor.time += deltaTime * actor.speed;
                    float length = actor.clips[actor.selected].length;
                    if (actor.time >= length)
                    {
                        if (IsAttack(actor.clips[actor.selected]) && actor.attackCycleActive)
                        {
                            actor.time = length;
                            actor.returning = true;
                            actor.approachTime = 0f;
                        }
                        else
                        {
                            actor.time = actor.repeat ? Mathf.Repeat(actor.time, length) : length;
                            if (!actor.repeat) actor.paused = true;
                        }
                    }
                }
                Sample(actor);
            }
        }

        private void LateUpdate()
        {
            if (!lockToViewport || !previewCamera || !previewCamera.orthographic) return;
            foreach (Actor actor in actors)
            {
                // 攻击流程使用世界坐标，不能被可选的视口站位覆盖。
                if (actor.attackCycleActive || IsAttack(actor.clips[actor.selected])) continue;
                Transform model = actor.animator.transform;
                Vector3 target = previewCamera.ViewportToWorldPoint(new Vector3(actor.footPosition.x,
                    actor.footPosition.y, model.position.z - previewCamera.transform.position.z));
                Vector3 foot = model.TransformPoint(actor.localFoot);
                model.position += new Vector3(target.x - foot.x, target.y - foot.y, 0f);
            }
        }

        private static void Sample(Actor actor)
        {
            if (!actor.animator.isActiveAndEnabled) return;
            actor.animator.speed = 0f;
            bool moving = actor.movingToAttack || actor.returning;
            // 暂停回原位显示待机；实际去程和返程才播放走路。
            AnimationClip walk = moving && !actor.paused
                ? Array.Find(actor.clips, candidate => candidate.name.StartsWith("Walk", StringComparison.Ordinal)) : null;
            AnimationClip clip = moving ? (walk ? walk : actor.clips[0]) : actor.clips[actor.selected];
            float sampleTime = moving ? Mathf.Repeat(actor.approachTime, clip.length) : actor.time;
            actor.animator.Play(clip.name, 0, Mathf.Min(sampleTime / clip.length, 0.999999f));
            actor.animator.Update(0f);
        }

        private static bool IsAttack(AnimationClip clip)
        {
            return clip.name.StartsWith("Attack", StringComparison.Ordinal);
        }

        public void SelectAction(Actor actor, int index)
        {
            bool attack = IsAttack(actor.clips[index]);
            // 攻击流程内重播仍返回本轮出发点，不把攻击点或返程途中位置记作原位置。
            if (attack && !actor.attackCycleActive) actor.returnPosition = actor.animator.transform.position;
            actor.attackCycleActive = attack;
            actor.returning = false;
            actor.selected = index;
            actor.time = 0f;
            actor.approachTime = 0f;
            actor.paused = false;
            actor.movingToAttack = attack;
            Sample(actor);
        }

        public void SetPaused(Actor actor, bool paused)
        {
            if (actor.paused == paused) return;
            actor.paused = paused;
            if (paused && actor.attackCycleActive)
            {
                // 面板暂停立即回到出发点；恢复时重新去往攻击点，不在原站位续播攻击。
                actor.animator.transform.position = actor.returnPosition;
                actor.returning = false;
                actor.movingToAttack = true;
                actor.time = 0f;
                actor.approachTime = 0f;
            }
            Sample(actor);
        }

        public void DrawActorSelector()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(SelectedActor == -1, "主角", GUI.skin.button)) SelectedActor = -1;
            for (int i = 0; i < actors.Length; i++)
                if (GUILayout.Toggle(SelectedActor == i, actors[i].label, GUI.skin.button)) SelectedActor = i;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("怪物全部待机")) ReplayAll(false);
            if (GUILayout.Button("怪物全部攻击")) ReplayAll(true);
            GUILayout.EndHorizontal();
        }

        public void ReplayAll(bool attack)
        {
            foreach (Actor actor in actors)
            {
                SelectAction(actor, attack ? 1 : 0);
            }
        }

        public void DrawSelectedControls()
        {
            Actor actor = actors[SelectedActor];
            GUILayout.Label(actor.label + " · 左下方向");
            for (int i = 0; i < actor.clips.Length; i++)
            {
                string label = actor.clips[i].name.StartsWith("Attack") ? "攻击" :
                    actor.clips[i].name.StartsWith("Walk") ? "走路" : "待机";
                if (GUILayout.Toggle(actor.selected == i, label, GUI.skin.button) && actor.selected != i)
                {
                    SelectAction(actor, i);
                }
            }
            SetPaused(actor, GUILayout.Toggle(actor.paused, "暂停播放（回到原位）"));
            actor.repeat = GUILayout.Toggle(actor.repeat, "循环预览（包含攻击）");
            GUILayout.Label("播放速度：" + actor.speed.ToString("0.00") + " 倍");
            actor.speed = GUILayout.HorizontalSlider(actor.speed, 0.1f, 2f);
            AnimationClip clip = actor.clips[actor.selected];
            if (actor.paused && actor.attackCycleActive)
                GUILayout.Label("已暂停并回到原位，继续时重新出发。");
            else if (actor.movingToAttack)
                GUILayout.Label("正在前往攻击位置，到达后播放攻击。");
            if (actor.returning)
                GUILayout.Label("攻击完成，正在返回出发位置。");
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !actor.movingToAttack && !actor.returning;
            GUILayout.Label("帧：" + Mathf.Min(Mathf.FloorToInt(actor.time * clip.frameRate) + 1,
                Mathf.RoundToInt(clip.length * clip.frameRate)));
            float seek = GUILayout.HorizontalSlider(actor.time, 0f, clip.length);
            if (!Mathf.Approximately(seek, actor.time)) { actor.time = seek; actor.paused = true; Sample(actor); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上一帧")) Step(actor, -1);
            if (GUILayout.Button("下一帧")) Step(actor, 1);
            GUI.enabled = previousEnabled;
            if (GUILayout.Button("重播")) SelectAction(actor, actor.selected);
            GUILayout.EndHorizontal();
            GUILayout.Label("移动 → 攻击 → 返回；暂停立即回到原位。\n循环时重复完整流程，各角色互不影响。", GUILayout.Height(48));
        }

        private static void Step(Actor actor, int direction)
        {
            actor.paused = true;
            actor.time = Mathf.Clamp(actor.time + direction / actor.clips[actor.selected].frameRate,
                0f, actor.clips[actor.selected].length);
            Sample(actor);
        }
    }
}
