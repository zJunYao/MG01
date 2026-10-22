using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MG01.Demo.Views
{
    /// <summary>运行时Playable播放；不使用观察工具的逐帧动画采样。</summary>
    public sealed class BattleActorPlayer : IDisposable
    {
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private readonly Animator animator;
        public string ClipName { get; private set; }
        public BattleActorPlayer(Animator animator) { this.animator = animator; animator.fireEvents = false; }
        public void Play(AnimationClip clip, float speed = 1)
        {
            if (clip == null) throw new InvalidOperationException("缺少动作片段");
            if (graph.IsValid()) graph.Destroy();
            graph = PlayableGraph.Create("DemoActor_" + animator.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            playable = AnimationClipPlayable.Create(graph, clip); playable.SetSpeed(speed);
            var output = AnimationPlayableOutput.Create(graph, "ActorOutput", animator); output.SetSourcePlayable(playable);
            ClipName = clip.name; graph.Play();
        }
        public bool Valid => animator != null && animator.isActiveAndEnabled && graph.IsValid();
        public double Time => Valid ? playable.GetTime() : 0;
        public void Dispose() { if (graph.IsValid()) graph.Destroy(); }
    }
}
