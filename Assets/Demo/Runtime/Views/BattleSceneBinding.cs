using System;
using System.Linq;
using UnityEngine;

namespace MG01.Demo.Views
{
    /// <summary>由观察场景提取的运行绑定，不带观察脚本或采样逻辑。</summary>
    public sealed class BattleSceneBinding : MonoBehaviour
    {
        [Serializable] public sealed class Actor
        {
            public string key;
            public Animator animator;
            public AnimationClip[] clips;
            public Vector3 home;
            public Vector3 approach;
            public Vector3 scale;
            public AnimationClip Find(string name) => clips.FirstOrDefault(clip => clip != null && clip.name == name);
        }
        public Camera battleCamera;
        public Actor[] actors;
        public int modelLayer = 8;
        private Transform battleBackground;
        private bool? preparation;
        private void Awake()
        {
            battleBackground = transform.Find("BattleBackground");
        }
        private Vector3 DisplayPosition(Actor actor)
        {
            if (preparation != true) return actor.home;
            float x = actor.key == "hero" ? -3.42f : actor.key == "warrior" ? 0f : actor.key == "thief" ? 1.1f : 2.1f;
            return new Vector3(x, .4f, 0f);
        }
        /// <summary>备战使用UIStage的场景图，透明模型画面只叠加角色；执行阶段显示战斗背景。</summary>
        public void SetPreparation(bool preparing)
        {
            // 仅在阶段切换时移动角色，战斗期间由演出驱动靠近和返回原场景站位。
            if (preparation != preparing)
            {
                preparation = preparing;
                foreach (var actor in actors) actor.animator.transform.position = DisplayPosition(actor);
            }
            if (battleBackground != null) battleBackground.gameObject.SetActive(!preparing);
            battleCamera.enabled = !preparing;
        }
        public Actor Get(string key) => actors.Single(actor => actor.key == key);
        public void ResetActors()
        {
            foreach (var actor in actors)
            { actor.animator.gameObject.SetActive(true); actor.animator.enabled = true; actor.animator.transform.position = DisplayPosition(actor); actor.animator.transform.localScale = actor.scale; actor.animator.GetComponent<SpriteRenderer>().color = Color.white; }
        }
    }
}
