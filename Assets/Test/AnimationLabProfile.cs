using UnityEngine;

namespace MG01.Test
{
    /// <summary>独立的动画表现参数，不修改原始角色动画。</summary>
    [CreateAssetMenu(menuName = "测试/动画表现配置", fileName = "酋长跳跃配置")]
    public sealed class AnimationLabProfile : ScriptableObject
    {
        [Tooltip("是否为酋长攻击叠加额外跳跃高度")]
        public bool enableJump = true;
        [Range(0f, 3f), Tooltip("在原图跳跃之上增加的世界空间高度；100 像素约为 1 单位")]
        public float extraHeight = 0.65f;
        [Range(0f, 1f), Tooltip("起跳开始时的攻击进度")]
        public float takeoff = 0.25f;
        [Range(0f, 1f), Tooltip("到达最高点时的攻击进度")]
        public float apex = 0.45f;
        [Range(0f, 1f), Tooltip("落地时的攻击进度，需要在地面特效出现前归零")]
        public float landing = 0.68f;

        public void ClampValues()
        {
            extraHeight = Mathf.Clamp(extraHeight, 0f, 3f);
            takeoff = Mathf.Clamp(takeoff, 0f, 0.98f);
            apex = Mathf.Clamp(apex, takeoff + 0.01f, 0.99f);
            landing = Mathf.Clamp(landing, apex + 0.01f, 1f);
        }

        public float EvaluateHeight(float progress)
        {
            if (!enableJump || progress <= takeoff || progress >= landing) return 0f;
            if (progress <= apex)
            {
                float t = Mathf.InverseLerp(takeoff, apex, progress);
                return extraHeight * (1f - (1f - t) * (1f - t));
            }
            float fall = Mathf.InverseLerp(apex, landing, progress);
            return extraHeight * (1f - fall * fall);
        }
    }
}
