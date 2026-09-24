using System.IO;
using CinematicPoker.Game.Profiles;
using UnityEditor;
using UnityEngine;

namespace CinematicPoker.Editor
{
    /// <summary>
    /// Generates the prototype AIProfile ScriptableObjects: five distinct
    /// personalities with different skill, aggression, tightness, bluff
    /// frequency and decision noise (the Mates' Kitchen roster).
    /// The runtime prototype uses code presets with the same values, so these
    /// assets are for designer tweaking in the Inspector.
    /// </summary>
    public static class ProfileAssetGenerator
    {
        private const string Folder = "Assets/Profiles";

        private static readonly (string name, float skill, float aggr, float tight, float bluff, float noise)[] Roster =
        {
            // name        skill aggr  tight bluff noise
            ("Davo",       0.35f, 0.90f, 0.10f, 0.40f, 0.25f), // maniac
            ("Mick",       0.50f, 0.50f, 0.50f, 0.15f, 0.10f), // regular, tilts hard
            ("Shazza",     0.45f, 0.25f, 0.85f, 0.03f, 0.10f), // rock
            ("Bluey",      0.25f, 0.20f, 0.15f, 0.05f, 0.20f), // calling station
            ("Kev",        0.90f, 0.65f, 0.60f, 0.20f, 0.03f)  // the shark
        };

        [MenuItem("Cinematic Poker/Generate Prototype AI Profiles")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            foreach (var (name, skill, aggr, tight, bluff, noise) in Roster)
            {
                string path = $"{Folder}/AIProfile_{name}.asset";
                if (AssetDatabase.LoadAssetAtPath<AIProfileAsset>(path) != null) continue;

                var asset = ScriptableObject.CreateInstance<AIProfileAsset>();
                asset.skill = skill;
                asset.aggression = aggr;
                asset.tightness = tight;
                asset.bluffFrequency = bluff;
                asset.decisionNoise = noise;
                asset.tiltSensitivity = name == "Mick" ? 0.8f : 0.4f;
                asset.opponentAwareness = skill;
                asset.potOddsAccuracy = skill;
                asset.positionAwareness = skill;
                asset.handReadingAbility = skill;
                AssetDatabase.CreateAsset(asset, path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Prototype AI profiles generated in {Folder}.");
        }
    }
}
