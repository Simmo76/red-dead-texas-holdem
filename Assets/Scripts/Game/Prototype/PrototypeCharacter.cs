using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CinematicPoker.Game.Prototype
{
    /// <summary>
    /// A Kenney mini-character (CC0, see ASSET_LICENSES.md) seated at the
    /// prototype table. Poses and one-shot reactions are driven directly with
    /// the Playables API, so no AnimatorController asset is needed — keeping
    /// the whole scene serialisation-free. Creation returns null when the
    /// model pack is absent and the caller falls back to a capsule.
    /// </summary>
    public sealed class PrototypeCharacter : MonoBehaviour
    {
        private const float TargetHeight = 1.05f; // metres, standing pose

        private string _modelName;
        private Animator _animator;
        private PlayableGraph _graph;
        private Coroutine _reaction;

        public static PrototypeCharacter Create(string modelName, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = PrototypeAssets.Model($"Characters/{modelName}");
            if (prefab == null) return null;

            GameObject go = Instantiate(prefab, position, rotation);
            go.name = $"Character_{modelName}";

            var character = go.AddComponent<PrototypeCharacter>();
            character._modelName = modelName;
            character.NormaliseScale();
            character._animator = go.GetComponentInChildren<Animator>();
            if (character._animator == null) character._animator = go.AddComponent<Animator>();
            character.PoseSit();
            return character;
        }

        /// <summary>Uniform-scales the model so its standing height matches the table proportions.</summary>
        private void NormaliseScale()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.01f) return;

            float scale = Mathf.Clamp(TargetHeight / bounds.size.y, 0.05f, 20f);
            transform.localScale = Vector3.one * scale;
        }

        /// <summary>Holds a frame of the "sit" clip as a static seated pose.</summary>
        public void PoseSit()
        {
            AnimationClip sit = PrototypeAssets.ModelClip($"Characters/{_modelName}", "sit");
            if (!PlayClip(sit)) return;
            _graph.Evaluate(sit.length * 0.5f);
            _graph.GetRootPlayable(0).SetSpeed(0d);
        }

        /// <summary>One-shot emote (yes/no), then back to the seated pose.</summary>
        public void React(bool positive)
        {
            AnimationClip clip = PrototypeAssets.ModelClip($"Characters/{_modelName}", positive ? "emote-yes" : "emote-no");
            if (!PlayClip(clip)) return;
            if (_reaction != null) StopCoroutine(_reaction);
            _reaction = StartCoroutine(ReturnToSitAfter(clip.length));
        }

        /// <summary>Busted out of the session.</summary>
        public void Die()
        {
            if (_reaction != null) { StopCoroutine(_reaction); _reaction = null; }
            PlayClip(PrototypeAssets.ModelClip($"Characters/{_modelName}", "die"));
        }

        private IEnumerator ReturnToSitAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _reaction = null;
            PoseSit();
        }

        private bool PlayClip(AnimationClip clip)
        {
            if (clip == null || _animator == null) return false;
            try
            {
                DestroyGraph();
                AnimationPlayableUtilities.PlayClip(_animator, clip, out _graph);
                var playable = _graph.GetRootPlayable(0);
                playable.SetDuration(clip.length);
                return true;
            }
            catch (System.Exception)
            {
                return false; // pose stays as imported; capsule-free but static
            }
        }

        private void DestroyGraph()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        private void OnDestroy() => DestroyGraph();
    }
}
