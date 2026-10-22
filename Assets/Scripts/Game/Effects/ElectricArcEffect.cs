using UnityEngine;

// 角色通用电弧表现，由业务层显式调用Play触发。
[DisallowMultipleComponent]
public sealed class ElectricArcEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private GameObject effectPrefab;
    [SerializeField, Min(0.05f)] private float duration = 1.5f;
    [SerializeField] private Vector2 bodyCenter = new Vector2(0f, 0.8f);
    [SerializeField] private Vector2 bodySize = new Vector2(1.3f, 1.75f);
    [SerializeField, Range(0.1f, 0.5f)] private float arcSize = 0.34f;
    [SerializeField, Min(0.04f)] private float flashInterval = 0.25f;
    [SerializeField, Min(0.01f)] private float flashSeconds = 0.1f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.15f;
    [SerializeField] private int sortingOffset = 1;

    private static readonly int ElapsedId = Shader.PropertyToID("_Elapsed");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int BodySizeId = Shader.PropertyToID("_BodySize");
    private static readonly int ArcSizeId = Shader.PropertyToID("_ArcSize");
    private static readonly int IntervalId = Shader.PropertyToID("_Interval");
    private static readonly int FlashSecondsId = Shader.PropertyToID("_FlashSeconds");
    private static readonly int FacingId = Shader.PropertyToID("_Facing");

    private GameObject instance;
    private MeshRenderer effectRenderer;
    private MaterialPropertyBlock properties;
    private float elapsed;
    private float remaining;

    public bool IsPlaying => remaining > 0f;
    public float RemainingSeconds => remaining;

    private void Awake()
    {
        if (visual == null)
        {
            Transform child = transform.Find("Visual");
            visual = child != null ? child.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        }
        properties = new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        if (instance != null) Destroy(instance);
    }

    [ContextMenu("播放电弧")]
    public void Play() => Play(duration);

    public void Play(float seconds)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || seconds <= 0f || visual == null || effectPrefab == null) return;
        if (instance == null)
        {
            instance = Instantiate(effectPrefab, transform, false);
            instance.name = "ElectricArcVisual";
            instance.layer = visual.gameObject.layer;
            effectRenderer = instance.GetComponent<MeshRenderer>();
            if (effectRenderer == null)
            {
                Destroy(instance);
                return;
            }
        }
        // 重复触发刷新同一实例并重播首次闪光。
        elapsed = 0f;
        remaining = seconds;
        UpdateVisual();
    }

    public void Stop()
    {
        remaining = 0f;
        elapsed = 0f;
        if (effectRenderer != null) effectRenderer.enabled = false;
    }

    private void LateUpdate() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (!IsPlaying) return;
        if (visual == null || effectRenderer == null)
        {
            Stop();
            return;
        }
        elapsed += Mathf.Max(0f, deltaTime);
        remaining -= Mathf.Max(0f, deltaTime);
        if (remaining <= 0f) Stop();
        else UpdateVisual();
    }

    private void UpdateVisual()
    {
        float facing = visual.flipX ? -1f : 1f;
        Vector2 size = new Vector2(Mathf.Max(0.1f, bodySize.x), Mathf.Max(0.1f, bodySize.y));
        instance.transform.localPosition = new Vector3(bodyCenter.x * facing, bodyCenter.y, 0f);
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = new Vector3(size.x, size.y, 1f);
        effectRenderer.sortingLayerID = visual.sortingLayerID;
        effectRenderer.sortingOrder = visual.sortingOrder + sortingOffset;
        effectRenderer.enabled = visual.enabled && visual.gameObject.activeInHierarchy;
        properties.SetFloat(ElapsedId, elapsed);
        properties.SetFloat(FadeId, (fadeOutSeconds > 0f ? Mathf.Clamp01(remaining / fadeOutSeconds) : 1f) * visual.color.a);
        properties.SetFloat(SeedId, (GetInstanceID() & 65535) * 0.0137f);
        properties.SetVector(BodySizeId, new Vector4(size.x, size.y, 0f, 0f));
        properties.SetFloat(ArcSizeId, arcSize);
        properties.SetFloat(IntervalId, flashInterval);
        properties.SetFloat(FlashSecondsId, flashSeconds);
        properties.SetFloat(FacingId, facing);
        effectRenderer.SetPropertyBlock(properties);
    }
}
