using UnityEngine;

namespace MG01.Test
{
    /// <summary>stage 独立展示测试：启动区域拍摄，并让四个模型自然循环待机。</summary>
    public sealed class StagePreview : MonoBehaviour
    {
        public SceneModelView modelView;
        public Animator[] actors;

        private void Start()
        {
            modelView.ShowScene(new SceneModelViewSettings
            {
                modelLayers = LayerMask.GetMask("ModelPreview"),
                orthographicSize = 5.4f,
                cameraOffset = new Vector3(0f, 0f, -10f)
            });
            for (int i = 0; i < actors.Length; i++)
            {
                Animator actor = actors[i];
                actor.applyRootMotion = false;
                actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                actor.speed = 1f;
                actor.Play(i == 0 ? "Idle_south-east" : "Idle_south-west", 0, 0f);
            }
        }
    }
}
