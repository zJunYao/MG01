using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Avatar 展示空间。将模型归一化后放入相隔 100 单位的拍摄单元，
/// 专用层与受限相机视锥共同隔离各窗口。原始预制体不受修改。
/// </summary>
internal sealed class AvatarViewSpace : IDisposable
{
    private static readonly HashSet<int> OccupiedSlots = new HashSet<int>();
    private readonly int slot;
    private readonly GameObject root;
    private readonly AvatarViewSettings settings;
    private Vector3 center;
    private Vector3 size;
    public GameObject Instance { get; }
    public int Layer { get; }

    public AvatarViewSpace(GameObject prefab, AvatarViewSettings options)
    {
        Layer = LayerMask.NameToLayer("AvatarPreview");
        if (Layer < 0) throw new InvalidOperationException("缺少 AvatarPreview 展示层。");
        settings = options.Copy();
        slot = 0;
        while (OccupiedSlots.Contains(slot)) ++slot;
        OccupiedSlots.Add(slot);
        root = new GameObject("角色展示空间_" + slot);
        root.SetActive(false);
        root.transform.position = new Vector3((slot % 64) * 100f, (slot / 64) * 100f, 10000f);
        try
        {
            Instance = UnityEngine.Object.Instantiate(prefab, root.transform, false);
            Instance.name = prefab.name + "_展示实例";
            Instance.transform.localPosition = Vector3.zero;
            Instance.transform.localRotation = Quaternion.Euler(settings.modelEulerAngles);
            foreach (Transform child in Instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = Layer;
            // 展示资源不应附带额外相机、监听器或参与场景物理交互。
            foreach (Camera camera in Instance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (AudioListener listener in Instance.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (Collider collider in Instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Collider2D collider in Instance.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (Rigidbody body in Instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            foreach (Rigidbody2D body in Instance.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            foreach (Animator animator in Instance.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
            Instance.SetActive(true);
            root.SetActive(true);
            Fit();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Fit()
    {
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (Renderer renderer in Instance.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        if (!found) throw new InvalidOperationException("Avatar 展示模型没有可用于取景的 Renderer。");
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest < 0.0001f) throw new InvalidOperationException("Avatar 展示模型尺寸为零，无法自动取景。");
        float scale = 2f / longest;
        Vector3 origin = root.transform.position;
        Instance.transform.position = origin + (Instance.transform.position - bounds.center) * scale;
        Instance.transform.localScale *= scale;
        center = origin;
        size = bounds.size * scale;
    }

    public void Configure(Camera camera, float aspect)
    {
        camera.cullingMask = 1 << Layer;
        camera.orthographic = true;
        float halfHeight = Mathf.Max(size.y * 0.5f, size.x * 0.5f / aspect);
        // 同时限制横向与纵向视野，确保极端 UI 比例也不会拍到相邻单元。
        float maxHalfHeight = 20f / Mathf.Max(1f, aspect);
        camera.orthographicSize = Mathf.Clamp(halfHeight * Mathf.Clamp(settings.padding, 1f, 3f)
            / Mathf.Clamp(settings.zoom, 0.25f, 4f), 0.001f, maxHalfHeight);
        Vector2 offset = Vector2.ClampMagnitude(settings.framingOffset, 5f);
        camera.transform.SetPositionAndRotation(center + new Vector3(offset.x, offset.y, -10f), Quaternion.identity);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 30f;
    }

    public void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }

    public void Dispose()
    {
        if (root != null)
        {
            root.SetActive(false);
            ModelViewCapture.DestroyOwned(root);
        }
        OccupiedSlots.Remove(slot);
    }
}
