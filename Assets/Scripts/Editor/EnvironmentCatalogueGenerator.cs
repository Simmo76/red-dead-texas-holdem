using System.IO;
using CinematicPoker.Game.Environments;
using UnityEditor;
using UnityEngine;

namespace CinematicPoker.Editor
{
    /// <summary>
    /// Generates placeholder PokerEnvironmentDefinition assets for the initial
    /// environment catalogue. Real content (scenes, NPC pools, styles) is
    /// filled in per environment pack later — adding it never touches the
    /// engine, per the hard architectural requirement.
    /// </summary>
    public static class EnvironmentCatalogueGenerator
    {
        private const string Folder = "Assets/Environments";

        private static readonly (string id, string name, bool baseGame, string description)[] Catalogue =
        {
            ("MatesKitchen", "Mates' Kitchen", true,
                "Suburban Australian kitchen. Five friends with shared history. First vertical slice."),
            ("Western1860", "Frontier — 1860s", false,
                "Western saloon: gunslinger, sheriff, rancher, travelling gambler, saloon owner."),
            ("KalgoorlieShed", "Kalgoorlie Shed", false,
                "Backyard mining-town shed in WA: miners, drill operator, mechanic, FIFO worker."),
            ("Chicago1926", "Chicago — 1926", false,
                "Prohibition back-room game: businessman, nightclub owner, bootlegger, lawyer, newspaper man."),
            ("LasVegas", "Las Vegas High Roller", false,
                "Luxury casino with the game's strongest poker AI."),
            ("DesertCamp", "Desert Royal Camp", false,
                "Elegant, quiet, exclusive luxury desert game on the Arabian Peninsula."),
            ("Disco1978", "Disco — 1978", false,
                "After-hours nightclub: club owner, DJ, bartender, musician, promoter, regular."),
            ("VelvetMansion1974", "Velvet Mansion — 1974", false,
                "Original 1970s Hollywood mansion-party fantasy (no third-party IP)."),
            ("TokyoAfterMidnight", "Tokyo — After Midnight", false,
                "Private organised-crime poker room; social hierarchy matters. Sumo-sized rig support."),
            ("Spaceport2384", "Spaceport — 2384", false,
                "Original space-western cantina; alien species with species-specific probabilistic tells."),
            ("PostWarWA2041", "Western Australia — 2041", false,
                "Post-war outback roadhouse; original setting unless the Gallows Gold licence is secured.")
        };

        [MenuItem("Cinematic Poker/Generate Placeholder Environment Definitions")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            foreach (var (id, displayName, baseGame, description) in Catalogue)
            {
                string path = $"{Folder}/{id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<PokerEnvironmentDefinition>(path);
                if (existing != null) continue;

                var def = ScriptableObject.CreateInstance<PokerEnvironmentDefinition>();
                def.environmentId = id;
                def.displayName = displayName;
                def.description = description;
                def.includedInBaseGame = baseGame;
                def.sceneAddress = $"env/{id}/scene"; // Addressables key convention
                AssetDatabase.CreateAsset(def, path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Placeholder environment definitions generated in {Folder}.");
        }
    }
}
