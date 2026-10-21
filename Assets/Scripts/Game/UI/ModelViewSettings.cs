using System;
using UnityEngine;

/// <summary>模型显示模式。空模式不会分配拍摄资源。</summary>
public enum ModelViewMode
{
    None,
    Avatar,
    Scene
}

/// <summary>场景取景参数。位置和旋转相对于锚点，不受 UI 变换影响。</summary>
[Serializable]
public sealed class SceneModelViewSettings
{
    public LayerMask modelLayers = 1 << 8;
    public Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
    public Vector3 cameraEulerAngles;
    public bool orthographic = true;
    [Min(0.01f)] public float orthographicSize = 4f;
    [Range(1f, 179f)] public float fieldOfView = 30f;
    [Min(0.01f)] public float nearClipPlane = 0.1f;
    [Min(0.02f)] public float farClipPlane = 100f;

    internal SceneModelViewSettings Copy() => (SceneModelViewSettings)MemberwiseClone();
}

/// <summary>Avatar 采用正交相机。首次测量后固定取景，动画不会触发逐帧适配。</summary>
[Serializable]
public sealed class AvatarViewSettings
{
    public Vector3 modelEulerAngles;
    [Range(1f, 3f)] public float padding = 1.2f;
    [Range(0.25f, 4f)] public float zoom = 1f;
    [Tooltip("相对于归一化模型的镜头偏移。")]
    public Vector2 framingOffset;

    internal AvatarViewSettings Copy() => (AvatarViewSettings)MemberwiseClone();
}

/// <summary>页面只需持有该接口，无需直接管理相机和渲染纹理。</summary>
public interface IModelView
{
    ModelViewMode Mode { get; }
    void ShowAvatar(GameObject prefab, AvatarViewSettings settings = null);
    void ShowScene(SceneModelViewSettings settings = null, Transform anchor = null);
    void SetVisible(bool visible);
    void Clear();
}
