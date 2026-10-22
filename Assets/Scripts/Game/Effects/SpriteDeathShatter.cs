using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 捕获当前帧，分别生成身体碎片和能量粒子网格。
[DisallowMultipleComponent]
public sealed class SpriteDeathShatter : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Material bodyMaterial;
    [SerializeField] private Material particleMaterial;
    [SerializeField, Min(0.1f)] private float duration = 0.72f;
    [SerializeField] private int seed = 37;
    [SerializeField] private bool destroyAfterDeath = false;
    private readonly List<Mesh> meshes = new List<Mesh>();
    private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    private MaterialPropertyBlock properties;
    private float elapsed;
    public bool HasStarted { get; private set; }
    public bool IsComplete { get; private set; }
    public float Duration => duration;

    [ContextMenu("播放死亡碎裂")]
    public void PlayDeath()
    {
        if (HasStarted) return;
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        if (visual == null || visual.sprite == null || bodyMaterial == null || particleMaterial == null) return;
        Sprite sprite = visual.sprite;
        // 当前实现要求精灵未进行图集旋转。
        if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
        {
            Debug.LogError("死亡碎裂要求使用未旋转的图集精灵。", this);
            return;
        }
        properties = new MaterialPropertyBlock();
        CreateMesh(sprite, false);
        CreateMesh(sprite, true);
        HasStarted = true;
        var animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        var arc = GetComponent<ElectricArcEffect>();
        if (arc != null) arc.Stop();
        visual.enabled = false;
        Sample(0);
    }

    private void Update() { if (HasStarted && !IsComplete) Tick(Time.deltaTime); }

    private void Tick(float delta)
    {
        elapsed += Mathf.Max(0, delta);
        Sample(elapsed / duration);
        if (elapsed < duration) return;
        IsComplete = true;
        foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
        if (destroyAfterDeath && Application.isPlaying) Destroy(gameObject);
    }

    // 支持按进度采样表现。
    public void Sample(float progress)
    {
        if (!HasStarted) return;
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Progress", Mathf.Clamp01(progress));
            renderer.SetPropertyBlock(properties);
        }
    }

    private void CreateMesh(Sprite sprite, bool particles)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var centers = new List<Vector4>();
        var cells = new List<Vector4>();
        var triangles = new List<int>();
        var random = new System.Random(seed + (particles ? 101 : 0));
        float ppu = sprite.pixelsPerUnit;
        float facing = visual.flipX ? -1 : 1;
        float vertical = visual.flipY ? -1 : 1;
        Rect rect = sprite.textureRect;
        Vector2 pivot = sprite.pivot;
        Action<float,float,float,float,Rect> quad = (x,y,w,h,textureRect) =>
        {
            int first = vertices.Count;
            float a = (float)random.NextDouble(), b = (float)random.NextDouble();
            var center = new Vector4((x+w/2)*facing,(y+h/2)*vertical,a,b);
            for (int i=0;i<4;i++)
            {
                float u = i==1 || i==2 ? 1 : 0, v = i>=2 ? 1 : 0;
                vertices.Add(new Vector3((x+u*w)*facing,(y+v*h)*vertical,0));
                uv.Add(new Vector2(textureRect.x+u*textureRect.width,textureRect.y+v*textureRect.height));
                centers.Add(center);
                cells.Add(new Vector4(u,v,w*ppu,h*ppu));
            }
            triangles.AddRange(new[] { first,first+1,first+2,first,first+2,first+3 });
        };
        if (!particles)
        {
            for (int y=0;y<(int)rect.height;)
            {
                int h = Math.Min(random.Next(12,23),(int)rect.height-y);
                for (int x=0;x<(int)rect.width;)
                {
                    int w = Math.Min(random.Next(10,23),(int)rect.width-x);
                    quad((x-pivot.x)/ppu,(y-pivot.y)/ppu,w/ppu,h/ppu,
                        new Rect((rect.x+x)/sprite.texture.width,(rect.y+y)/sprite.texture.height,
                            (float)w/sprite.texture.width,(float)h/sprite.texture.height));
                    x += w;
                }
                y += h;
            }
        }
        else
        {
            for (int i=0;i<22;i++)
            {
                int tile = i<6 ? 4+i%4 : 8+i%8;
                float size = i<6 ? 0.16f : 0.09f;
                float x = ((float)random.NextDouble()-0.5f)*0.65f;
                float y = 0.20f+(float)random.NextDouble()*1.2f;
                quad(x-size/2,y-size/2,size,size,new Rect(tile%4/4f,(3-tile/4)/4f,0.25f,0.25f));
            }
        }
        var mesh = new Mesh { name = particles ? "Death energy particles" : "Death body fragments" };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,centers); mesh.SetUVs(2,cells);
        mesh.SetTriangles(triangles,0);
        // 扩展裁剪边界以容纳着色器位移。
        mesh.bounds = new Bounds(new Vector3(0,0.8f,0),new Vector3(8,8,2));
        meshes.Add(mesh);
        var go = new GameObject(mesh.name);
        go.layer = visual.gameObject.layer;
        go.transform.SetParent(visual.transform,false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = particles ? particleMaterial : bodyMaterial;
        renderer.sortingLayerID = visual.sortingLayerID;
        renderer.sortingOrder = visual.sortingOrder+(particles ? 1 : 0);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        properties.Clear();
        properties.SetColor("_Tint",visual.color);
        if (!particles) properties.SetTexture("_MainTex",sprite.texture);
        renderer.SetPropertyBlock(properties);
        renderers.Add(renderer);
    }

    private void OnDestroy()
    {
        foreach (var mesh in meshes)
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
    }
}
