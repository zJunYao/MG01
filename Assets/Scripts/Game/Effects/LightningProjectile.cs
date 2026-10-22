using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class LightningProjectile : MonoBehaviour
{
    [SerializeField] private GameObject dissolvePrefab;
    [SerializeField, Min(0.01f)] private float speed = 6f;
    [SerializeField] private Vector2 headOffset = new Vector2(1.33f, -0.07f);
    private SpriteRenderer visual;
    private float direction;
    private float maxDistance;
    private Vector3 launchPosition;
    private bool launched;
    public System.Action Completed;
    public float TravelledDistance { get; private set; }
    public bool Finished { get; private set; }
    public Vector3 HeadPosition => transform.TransformPoint(new Vector3(headOffset.x * direction, headOffset.y, 0f));

    // 未发射时仅循环展示；伤害由当前项目的战斗逻辑结算。
    public void Launch(Vector3 muzzle, float facing, float distance)
    {
        if (launched) return;
        visual = GetComponent<SpriteRenderer>();
        direction = facing < 0f ? -1f : 1f;
        visual.flipX = direction < 0f;
        maxDistance = Mathf.Max(0f, distance);
        transform.position += muzzle - HeadPosition;
        launchPosition = transform.position;
        launched = true;
        Tick(0f);
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (!launched || Finished || deltaTime < 0f) return;
        float step = Mathf.Min(Mathf.Max(0f, maxDistance - TravelledDistance), Mathf.Max(0.01f, speed) * deltaTime);
        TravelledDistance = Mathf.Min(maxDistance, TravelledDistance + step);
        // 从起点重建位置，避免逐帧累加误差。
        transform.position = launchPosition + Vector3.right * (direction * TravelledDistance);
        if (TravelledDistance >= maxDistance - 0.00001f) Finish();
    }

    private void Finish()
    {
        if (Finished) return;
        Finished = true;
        if (dissolvePrefab != null)
        {
            GameObject ending = Instantiate(dissolvePrefab, transform.position, transform.rotation);
            ending.layer = gameObject.layer;
            ending.transform.localScale = transform.lossyScale;
            SpriteRenderer renderer = ending.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = visual.flipX;
                renderer.flipY = visual.flipY;
                renderer.color = visual.color;
                renderer.sortingLayerID = visual.sortingLayerID;
                renderer.sortingOrder = visual.sortingOrder;
            }
        }
        visual.enabled = false;
        Completed?.Invoke();
        Destroy(gameObject);
    }
}
