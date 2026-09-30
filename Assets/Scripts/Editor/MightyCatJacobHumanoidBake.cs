using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CinematicPoker.Editor
{
    /// <summary>
    /// Optional Unity muscle-space bake of The Mighty Cat pack onto Jacob.
    ///
    /// Menu: Cinematic Poker → Bake Mighty Cat onto Jacob (Humanoid)
    ///
    /// The shipped web GLB is produced by tools/bake_mighty_cat_humanoid.py
    /// (facing-space aim retarget, no rest-pose apply). Use this menu when
    /// the Editor is available and both SK_Jacob and the Mighty Cat FBX
    /// clips are imported as Humanoid: it samples each clip on Jacob and
    /// writes Generic curves for a later GLB/FBX export.
    ///
    /// Clip list matches https://anims.themightycat.com/?asset=poker-blackjack
    /// </summary>
    public static class MightyCatJacobHumanoidBake
    {
        private const string OutputFolder = "Assets/Animations/MightyCatJacob";

        private static readonly (string FileHint, string OutputName, string Kind)[] Takes =
        {
            ("AS_Blackjack_Player_01", "IdleBlackjack01", "idle"),
            ("AS_Blackjack_Player_02", "IdleBlackjack02", "idle"),
            ("AS_Blackjack_Player_03", "IdleBlackjack03", "idle"),
            ("AS_Poker_Player_01", "IdlePoker01", "idle"),
            ("AS_Poker_Player_02", "IdlePoker02", "idle"),
            ("AS_Poker_Player_03", "IdlePoker03", "idle"),
            ("AS_Reactions_Player01_Win_01", "Win01", "react"),
            ("AS_Reactions_Player01_Win_02", "Win02", "react"),
            ("AS_Reactions_Player01_Win_03", "Win03", "react"),
            ("AS_Reactions_Player01_Lose_01", "Lose01", "react"),
            ("AS_Reactions_Player01_Lose_02", "Lose02", "react"),
            ("AS_Reactions_Player02_Lose_01", "Lose03", "react"),
            ("AS_Reactions_Player02_Lose_02", "Lose04", "react"),
        };

        [MenuItem("Cinematic Poker/Bake Mighty Cat onto Jacob (Humanoid)")]
        public static void Bake()
        {
            GameObject jacobPrefab = FindJacobPrefab();
            if (jacobPrefab == null)
            {
                EditorUtility.DisplayDialog("Mighty Cat bake",
                    "Could not find SK_Jacob / Jacob in the project. Import the Cowboy 1 pack first.",
                    "OK");
                return;
            }

            EnsureHumanoid(jacobPrefab);
            var clips = new List<(AnimationClip clip, string output, string kind)>();
            foreach (var take in Takes)
            {
                AnimationClip src = FindClip(take.FileHint);
                if (src == null)
                {
                    Debug.LogWarning($"Mighty Cat bake: missing {take.FileHint}");
                    continue;
                }
                EnsureHumanoid(src);
                clips.Add((src, take.OutputName, take.Kind));
            }

            if (clips.Count == 0)
            {
                EditorUtility.DisplayDialog("Mighty Cat bake",
                    "No Mighty Cat clips found. Import PokerBlackjackAndCardGamesAnimationPack first.",
                    "OK");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(jacobPrefab);
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(jacobPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var controller = new AnimatorControllerStub();
            try
            {
                foreach (var (src, output, kind) in clips)
                {
                    AnimationClip baked = BakeClip(animator, src, kind);
                    baked.name = output;
                    string path = $"{OutputFolder}/{output}.anim";
                    AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(baked), path);
                    Debug.Log($"Mighty Cat bake: wrote {path} ({baked.length:0.00}s)");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                controller.Dispose();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Mighty Cat bake",
                $"Baked {clips.Count} clips into {OutputFolder}.\n\n" +
                "Next: export Jacob with those clips to GLB (FBX Exporter or Blender) and drop them into wwwroot/models/characters/western/mighty-cat.glb.",
                "OK");
        }

        private static AnimationClip BakeClip(Animator animator, AnimationClip source, string kind)
        {
            var baked = new AnimationClip { frameRate = 30, name = source.name };
            var bindings = new List<Transform>();
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (t == animator.transform)
                    continue;
                bindings.Add(t);
            }

            float start = 0f;
            float end = source.length;
            if (kind == "idle" && end > 5f)
            {
                start = Mathf.Min(end * 0.45f, end - 4f);
                end = Mathf.Min(end, start + 4f);
            }

            var paths = bindings.Select(t => AnimationUtility.CalculateTransformPath(t, animator.transform)).ToArray();
            var rotX = paths.Select(_ => new AnimationCurve()).ToArray();
            var rotY = paths.Select(_ => new AnimationCurve()).ToArray();
            var rotZ = paths.Select(_ => new AnimationCurve()).ToArray();
            var rotW = paths.Select(_ => new AnimationCurve()).ToArray();

            var playableClip = source;
            int steps = Mathf.Max(2, Mathf.RoundToInt((end - start) * baked.frameRate));
            for (int i = 0; i < steps; i++)
            {
                float t = start + (end - start) * i / (steps - 1);
                source.SampleAnimation(animator.gameObject, t);
                float key = (t - start);
                for (int b = 0; b < bindings.Count; b++)
                {
                    Quaternion q = bindings[b].localRotation;
                    rotX[b].AddKey(key, q.x);
                    rotY[b].AddKey(key, q.y);
                    rotZ[b].AddKey(key, q.z);
                    rotW[b].AddKey(key, q.w);
                }
            }

            for (int b = 0; b < bindings.Count; b++)
            {
                string p = paths[b];
                baked.SetCurve(p, typeof(Transform), "m_LocalRotation.x", rotX[b]);
                baked.SetCurve(p, typeof(Transform), "m_LocalRotation.y", rotY[b]);
                baked.SetCurve(p, typeof(Transform), "m_LocalRotation.z", rotZ[b]);
                baked.SetCurve(p, typeof(Transform), "m_LocalRotation.w", rotW[b]);
            }
            baked.EnsureQuaternionContinuity();
            return baked;
        }

        private static GameObject FindJacobPrefab()
        {
            foreach (string hint in new[] { "SK_Jacob", "Jacob", "Cowboy" })
            {
                foreach (string guid in AssetDatabase.FindAssets($"{hint} t:Prefab t:Model"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null && go.GetComponentInChildren<Animator>() != null)
                        return go;
                    if (go != null && path.IndexOf("Jacob", StringComparison.OrdinalIgnoreCase) >= 0)
                        return go;
                }
            }
            return null;
        }

        private static AnimationClip FindClip(string fileHint)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{fileHint} t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf(fileHint, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null && !clip.name.StartsWith("__preview", StringComparison.Ordinal))
                    return clip;
            }
            return null;
        }

        private static void EnsureHumanoid(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return;
            if (importer.animationType == ModelImporterAnimationType.Human)
                return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        private sealed class AnimatorControllerStub : IDisposable
        {
            public void Dispose() { }
        }
    }
}
