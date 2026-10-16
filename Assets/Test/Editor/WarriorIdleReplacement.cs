using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Test.Editor
{
    /// <summary>使用新待机图集更新原动画，保留所有资源标识和场景引用。</summary>
    public static class WarriorIdleReplacement
    {
        private const string Root = "Assets/GameResources/Characters/GoblinWarrior/";

        [MenuItem("测试/角色/应用战士新版呼吸待机")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式。");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存当前场景。");
            string texturePath = Root + "Sprites/Idle_south-west.png";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/Idle_south-west.anim");
            if (!clip || sprites.Length != 8) throw new InvalidOperationException("战士待机片段或 8 帧精灵缺失。");
            clip.frameRate = 5f;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                sprites.Select((sprite, i) => new ObjectReferenceKeyframe { time = i * 0.2f, value = sprite }).ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.stopTime = 1.6f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);

            // 首帧保持相同格子坐标与轴点，预制体继续使用原精灵，不触碰攻击动作或站位。
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "GoblinWarrior.prefab");
            if (!prefab || prefab.GetComponent<SpriteRenderer>().sprite != sprites[0])
                throw new InvalidOperationException("战士默认精灵引用异常。");
            if (Mathf.Abs(clip.length - 1.6f) > 0.001f)
                throw new InvalidOperationException("新版待机时长不正确。");
            AssetDatabase.SaveAssets();
            Debug.Log("战士新版呼吸待机已应用：8 帧、每秒 5 帧、1.6 秒循环，原动画及精灵引用保持不变。");
        }
    }
}
