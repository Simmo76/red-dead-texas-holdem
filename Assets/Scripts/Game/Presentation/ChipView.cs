using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// A pooled stack of chips. Chip visuals change per environment (ChipStyle)
    /// but always map to the same logical value system in the engine.
    /// The mesh is the low-poly Poker Chips pack model. Colour is visual only
    /// and never changes <see cref="Value"/> or the stack-height scale.
    /// </summary>
    public sealed class ChipView : MonoBehaviour
    {
        public long Value { get; private set; }

        // Classic colour bands for the five imported materials.
        // white < 5, red < 10, blue < 25, green < 100, black.
        private static readonly long[] DenominationThresholds = { 5, 10, 25, 100 };

        private static readonly string[] MaterialPaths =
        {
            "Poker/Chips/Materials/Low_Poly/Pocker_Chip_LP_White",
            "Poker/Chips/Materials/Low_Poly/Pocker_Chip_LP_Red",
            "Poker/Chips/Materials/Low_Poly/Pocker_Chip_LP_Blue",
            "Poker/Chips/Materials/Low_Poly/Pocker_Chip_LP_Green",
            "Poker/Chips/Materials/Low_Poly/Pocker_Chip_LP_Black"
        };

        private const string ModelPath = "Poker/Chips/Models/Pocker_Chip_LP";

        // FBX bounds: disc of diameter ~4.368 in the XY plane, thin on Z.
        private const float SourceDiameter = 4.36804f;

        private Transform _visual;
        private int _colorIndex = -1;

        public void SetValue(long value)
        {
            Value = value;
            // Stack height/denomination visuals resolved by the table view.
            float height = Mathf.Clamp(Mathf.Log10(Mathf.Max(1, value)) * 0.01f, 0.005f, 0.08f);
            transform.localScale = new Vector3(1f, height * 100f, 1f);
            EnsureVisual(ColorIndex(value));
        }

        private static int ColorIndex(long value)
        {
            int index = 0;
            long amount = value < 0 ? 0 : value;
            for (int i = 0; i < DenominationThresholds.Length; i++)
            {
                if (amount >= DenominationThresholds[i]) index = i + 1;
            }
            return index;
        }

        private void EnsureVisual(int colorIndex)
        {
            if (_visual != null && _colorIndex == colorIndex) return;
            _colorIndex = colorIndex;

            if (_visual != null)
            {
                Destroy(_visual.gameObject);
                _visual = null;
            }

            GameObject model = Resources.Load<GameObject>(ModelPath);
            if (model == null) return;

            GameObject instance = Instantiate(model, transform, false);
            instance.name = "Chip";
            _visual = instance.transform;
            // Lay the disc flat and fit it to a unit diameter so the scale
            // above still reads as stack height.
            _visual.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one * (1f / SourceDiameter);

            Material mat = Resources.Load<Material>(MaterialPaths[colorIndex]);
            Renderer chipRenderer = instance.GetComponentInChildren<Renderer>();
            ApplyMaterial(chipRenderer, mat);
        }

        /// <summary>
        /// Uses the imported material when its shader is available. The pack
        /// ships Built-in Standard materials; under URP those shaders do not
        /// draw, so the same textures are copied onto the pipeline Lit shader.
        /// </summary>
        private static void ApplyMaterial(Renderer chipRenderer, Material source)
        {
            if (chipRenderer == null || source == null) return;
            Shader shader = source.shader;
            bool broken = shader == null
                || shader.name == "Hidden/InternalErrorShader"
                || !shader.isSupported;
            if (!broken)
            {
                chipRenderer.sharedMaterial = source;
                return;
            }

            Shader replacement = Shader.Find("Universal Render Pipeline/Lit");
            if (replacement == null) replacement = Shader.Find("Standard");
            if (replacement == null)
            {
                chipRenderer.sharedMaterial = source;
                return;
            }

            Material mat = new Material(replacement);
            Texture albedo = source.GetTexture("_MainTex");
            if (albedo != null)
            {
                mat.mainTexture = albedo;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
            }
            Texture bump = source.GetTexture("_BumpMap");
            if (bump != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", bump);
                mat.EnableKeyword("_NORMALMAP");
            }
            chipRenderer.material = mat;
        }
    }
}
