using System.Collections.Generic;
using CinematicPoker.Engine.Poker;
using UnityEngine;

namespace CinematicPoker.Game.Prototype
{
    /// <summary>
    /// Runtime loader for the CC0 art and audio under Assets/Resources/Poker
    /// (Kenney boardgame pack + casino audio, Poly Haven textures — see
    /// ASSET_LICENSES.md). Everything is optional: every getter may return
    /// null and the prototype falls back to its primitive-and-text look, so
    /// the scene never depends on serialized asset references.
    /// </summary>
    public static class PrototypeAssets
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, AudioClip[]> Clips = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<int, Material> CardMaterials = new Dictionary<int, Material>();
        private static Material _cardBackMaterial;

        // ------------------------------------------------------------ textures

        public static Texture2D CardTexture(Card card) =>
            LoadTexture($"Poker/Cards/{SuitName(card.Suit)}_{RankName(card.Rank)}");

        public static Texture2D CardBack => LoadTexture("Poker/Cards/back");
        public static Texture2D Felt => LoadTexture("Poker/Textures/felt");
        public static Texture2D Wood => LoadTexture("Poker/Textures/wood");

        private static Texture2D LoadTexture(string path)
        {
            if (!Textures.TryGetValue(path, out Texture2D tex))
            {
                tex = Resources.Load<Texture2D>(path);
                Textures[path] = tex; // cache misses too
            }
            return tex;
        }

        private static string SuitName(Suit suit) => suit switch
        {
            Suit.Clubs => "clubs",
            Suit.Diamonds => "diamonds",
            Suit.Hearts => "hearts",
            _ => "spades"
        };

        private static string RankName(Rank rank) =>
            rank == Rank.Ten ? "10" : Card.RankSymbol(rank);

        // ----------------------------------------------------------- materials

        /// <summary>Unlit transparent material showing a card face, or null if the art is missing.</summary>
        public static Material CardFaceMaterial(Card card)
        {
            if (CardMaterials.TryGetValue(card.Index, out Material cached)) return cached;
            Texture2D tex = CardTexture(card);
            Material mat = tex == null ? null : MakeSpriteMaterial(tex);
            CardMaterials[card.Index] = mat;
            return mat;
        }

        public static Material CardBackMaterial()
        {
            if (_cardBackMaterial == null && CardBack != null)
                _cardBackMaterial = MakeSpriteMaterial(CardBack);
            return _cardBackMaterial;
        }

        private static Material MakeSpriteMaterial(Texture2D tex)
        {
            // Sprites/Default renders unlit with alpha in both built-in and URP.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { mainTexture = tex };
        }

        /// <summary>Applies a tinted texture to a lit renderer if the texture exists.</summary>
        public static void ApplyTexture(GameObject go, Texture2D tex, Color tint, Vector2 tiling)
        {
            if (go == null || tex == null) return;
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Material mat = renderer.sharedMaterial;
            mat.color = tint;
            mat.mainTexture = tex;
            mat.mainTextureScale = tiling;
        }

        // --------------------------------------------------------------- audio

        /// <summary>
        /// Random variant from a clip group, e.g. "card-slide" resolves
        /// card-slide-1/-2/-3 (or a single un-numbered clip). Null when the
        /// audio pack is absent.
        /// </summary>
        public static AudioClip Sound(string group)
        {
            if (!Clips.TryGetValue(group, out AudioClip[] variants))
            {
                var found = new List<AudioClip>();
                AudioClip single = Resources.Load<AudioClip>($"Poker/Audio/{group}");
                if (single != null) found.Add(single);
                for (int i = 1; i <= 9; i++)
                {
                    AudioClip clip = Resources.Load<AudioClip>($"Poker/Audio/{group}-{i}");
                    if (clip == null) break;
                    found.Add(clip);
                }
                variants = found.ToArray();
                Clips[group] = variants;
            }
            return variants.Length == 0 ? null : variants[Random.Range(0, variants.Length)];
        }
    }
}
