using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>模型显示统一入口。挂在页面空节点上，拍摄空间独立于 UI 层级。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[AddComponentMenu("游戏/UI/场景模型显示")]
public sealed class SceneModelView : MonoBehaviour, IModelView
{
    [Header("初始显示")]
    [SerializeField] private ModelViewMode initialMode = ModelViewMode.None;
    [SerializeField] private GameObject avatarPrefab;
    [SerializeField] private AvatarViewSettings avatarSettings = new AvatarViewSettings();
    [SerializeField] private SceneModelViewSettings sceneSettings = new SceneModelViewSettings();
    [SerializeField] private Transform cameraAnchor;

    [Header("UI 输出")]
    [Tooltip("为空时优先使用同节点 RawImage，否则自动创建铺满入口的子节点。")]
    [SerializeField] private RawImage output;
    [Tooltip("透明 RT 的预乘透明显示材质；预制体已配置。")]
    [SerializeField] private Material displayMaterial;
    [Range(0.25f, 2f)] [SerializeField] private float resolutionScale = 1f;
    [Range(64, 4096)] [SerializeField] private int maxTextureSize = 2048;
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;

    private ModelViewCapture capture;
    private AvatarViewSpace avatar;
    private SceneModelViewSettings activeSceneSettings;
    private Transform activeAnchor;
    private Material runtimeMaterial;
    private Material originalMaterial;
    private Texture originalTexture;
    private bool originalEnabled;
    private bool ownsOutput;
    private bool outputInitialized;
    private bool initialized;
    private bool visible = true;
    private bool allocationFailed;

    public ModelViewMode Mode { get; private set; }
    public Camera CaptureCamera => capture?.Camera;
    public RenderTexture Texture => capture?.Texture;
    public GameObject AvatarInstance => avatar?.Instance;
    public RawImage Output => output;

    private void Start()
    {
        if (initialized) return;
        if (initialMode == ModelViewMode.Avatar && avatarPrefab != null) ShowAvatar(avatarPrefab, avatarSettings);
        else if (initialMode == ModelViewMode.Scene) ShowScene(sceneSettings, cameraAnchor);
    }

    public void ShowAvatar(GameObject prefab, AvatarViewSettings settings = null)
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab), "Avatar 模型预制体不能为空。");
        Clear();
        try
        {
            EnsureOutput();
            avatar = new AvatarViewSpace(prefab, settings ?? avatarSettings);
            capture = new ModelViewCapture(name);
            Mode = ModelViewMode.Avatar;
            ApplyVisibility();
        }
        catch
        {
            Clear();
            throw;
        }
    }

    public void ShowScene(SceneModelViewSettings settings = null, Transform anchor = null)
    {
        Clear();
        try
        {
            EnsureOutput();
            activeSceneSettings = (settings ?? sceneSettings).Copy();
            activeAnchor = anchor;
            capture = new ModelViewCapture(name);
            Mode = ModelViewMode.Scene;
            ApplyVisibility();
        }
        catch
        {
            Clear();
            throw;
        }
    }

    /// <summary>修改场景取景锚点。不会移动被拍摄模型。</summary>
    public void SetCameraAnchor(Transform anchor) => activeAnchor = anchor;

    /// <summary>设置区域拍摄层；Avatar 专用层会始终被排除。</summary>
    public void SetModelLayers(LayerMask layers)
    {
        if (activeSceneSettings != null) activeSceneSettings.modelLayers = layers;
    }

    /// <summary>仅在主动更换造型后调用，不会随动画逐帧重算。</summary>
    public void RefitAvatar() => avatar?.Fit();

    public void SetVisible(bool value)
    {
        visible = value;
        ApplyVisibility();
    }

    public void Clear()
    {
        initialized = true;
        if (output != null && capture != null)
        {
            output.enabled = false;
            output.texture = null;
        }
        capture?.Dispose();
        capture = null;
        avatar?.Dispose();
        avatar = null;
        activeSceneSettings = null;
        activeAnchor = null;
        allocationFailed = false;
        Mode = ModelViewMode.None;
    }

    private void EnsureOutput()
    {
        if (outputInitialized) return;
        if (output == null) output = GetComponent<RawImage>();
        if (output == null)
        {
            GameObject child = new GameObject("模型画面", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            output = child.GetComponent<RawImage>();
            output.rectTransform.anchorMin = Vector2.zero;
            output.rectTransform.anchorMax = Vector2.one;
            output.rectTransform.offsetMin = Vector2.zero;
            output.rectTransform.offsetMax = Vector2.zero;
            output.raycastTarget = false;
            ownsOutput = true;
        }
        originalTexture = output.texture;
        originalEnabled = output.enabled;
        originalMaterial = output.material;
        if (displayMaterial != null) output.material = displayMaterial;
        else
        {
            Shader shader = Shader.Find("Game/UI/SceneModelView");
            if (shader == null) throw new InvalidOperationException("缺少模型画面 Shader，请为入口配置显示材质。");
            runtimeMaterial = new Material(shader) { name = "模型画面材质_" + GetInstanceID() };
            output.material = runtimeMaterial;
        }
        output.enabled = false;
        outputInitialized = true;
    }

    private void OnEnable() => ApplyVisibility();

    private void OnDisable()
    {
        capture?.Pause();
        avatar?.SetVisible(false);
        if (output != null && Mode != ModelViewMode.None) output.enabled = false;
    }

    private void ApplyVisibility()
    {
        bool show = visible && isActiveAndEnabled;
        avatar?.SetVisible(show);
        if (!show)
        {
            capture?.Pause();
            if (output != null && Mode != ModelViewMode.None) output.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (capture == null || !visible || allocationFailed) return;
        if (output == null || capture.Camera == null)
        {
            Clear();
            return;
        }
        Canvas canvas = output.canvas;
        Rect rect = output.rectTransform.rect;
        if (!output.gameObject.activeInHierarchy || canvas == null || !canvas.isActiveAndEnabled || rect.width <= 0f || rect.height <= 0f)
        {
            capture.Pause();
            output.enabled = false;
            return;
        }
        float aspect = rect.width / rect.height;
        Camera camera = capture.Camera;
        camera.aspect = aspect;
        if (Mode == ModelViewMode.Avatar) avatar.Configure(camera, aspect);
        else ConfigureSceneCamera(camera);

        float scale = Mathf.Clamp(resolutionScale, 0.25f, 2f);
        if (canvas.renderMode != RenderMode.WorldSpace) scale *= canvas.scaleFactor;
        float width = rect.width * scale;
        float height = rect.height * scale;
        int limit = Mathf.Min(Mathf.Clamp(maxTextureSize, 64, 4096), SystemInfo.maxTextureSize);
        float factor = Mathf.Min(1f, limit / Mathf.Max(width, height));
        if (!capture.PrepareTexture(Mathf.Max(1, Mathf.RoundToInt(width * factor)),
            Mathf.Max(1, Mathf.RoundToInt(height * factor)), filterMode))
        {
            output.texture = null;
            output.enabled = false;
            allocationFailed = true;
            Debug.LogError("模型显示：创建 RT 失败，请重新调用显示接口重试。", this);
            return;
        }
        output.texture = capture.Texture;
        output.enabled = true;
        camera.enabled = true;
    }

    private void ConfigureSceneCamera(Camera camera)
    {
        SceneModelViewSettings settings = activeSceneSettings;
        camera.transform.SetPositionAndRotation(activeAnchor != null
            ? activeAnchor.TransformPoint(settings.cameraOffset) : settings.cameraOffset,
            (activeAnchor != null ? activeAnchor.rotation : Quaternion.identity) * Quaternion.Euler(settings.cameraEulerAngles));
        int avatarLayer = LayerMask.NameToLayer("AvatarPreview");
        camera.cullingMask = settings.modelLayers & (avatarLayer >= 0 ? ~(1 << avatarLayer) : -1);
        camera.orthographic = settings.orthographic;
        camera.orthographicSize = Mathf.Max(0.01f, settings.orthographicSize);
        camera.fieldOfView = Mathf.Clamp(settings.fieldOfView, 1f, 179f);
        camera.nearClipPlane = Mathf.Max(0.01f, settings.nearClipPlane);
        camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 0.01f, settings.farClipPlane);
    }

    private void OnDestroy()
    {
        Clear();
        if (output != null && outputInitialized)
        {
            if (ownsOutput) ModelViewCapture.DestroyOwned(output.gameObject);
            else
            {
                output.texture = originalTexture;
                output.material = originalMaterial;
                output.enabled = originalEnabled;
            }
        }
        ModelViewCapture.DestroyOwned(runtimeMaterial);
    }
}
