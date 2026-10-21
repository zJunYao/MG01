using System;
using UnityEngine;

/// <summary>单个视图独占的相机和 RT；不依赖 UI 或页面业务。</summary>
internal sealed class ModelViewCapture : IDisposable
{
    public GameObject Root { get; }
    public Camera Camera { get; }
    public RenderTexture Texture { get; private set; }

    public ModelViewCapture(string ownerName)
    {
        Root = new GameObject("模型拍摄_" + ownerName);
        Root.SetActive(false);
        Camera = Root.AddComponent<Camera>();
        Camera.enabled = false;
        Camera.clearFlags = CameraClearFlags.SolidColor;
        Camera.backgroundColor = Color.clear;
        Camera.allowHDR = false;
        Camera.allowMSAA = false;
        Camera.stereoTargetEye = StereoTargetEyeMask.None;
        Camera.depth = -100f;
        Root.SetActive(true);
    }

    public bool PrepareTexture(int width, int height, FilterMode filterMode)
    {
        if (Texture == null || Texture.width != width || Texture.height != height)
        {
            ReleaseTexture();
            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = Root.name + "_RT",
                hideFlags = HideFlags.DontSave,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                wrapMode = TextureWrapMode.Clamp
            };
        }
        Texture.filterMode = filterMode;
        if (!Texture.IsCreated() && !Texture.Create())
        {
            ReleaseTexture();
            return false;
        }
        Camera.targetTexture = Texture;
        return true;
    }

    public void Pause()
    {
        if (Camera != null) Camera.enabled = false;
    }

    public void Dispose()
    {
        ReleaseTexture();
        if (Root != null)
        {
            Root.SetActive(false);
            DestroyOwned(Root);
        }
    }

    private void ReleaseTexture()
    {
        Pause();
        if (Camera != null) Camera.targetTexture = null;
        if (Texture == null) return;
        Texture.Release();
        DestroyOwned(Texture);
        Texture = null;
    }

    internal static void DestroyOwned(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(target);
        else UnityEngine.Object.DestroyImmediate(target);
    }
}
