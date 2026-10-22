using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public sealed class OneShotSpriteEffect : MonoBehaviour
{
    // 动画切换到末尾透明帧后，通过动画事件回收特效。
    public void OnEffectFinished()
    {
        if (Application.isPlaying)
            Destroy(gameObject);
    }
}
