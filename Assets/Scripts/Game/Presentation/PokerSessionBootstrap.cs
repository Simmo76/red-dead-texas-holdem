using System.Collections.Generic;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Environments;
using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// Builds a session from an environment definition: one human plus up to
    /// five NPCs from the environment's pool, then hands off to the
    /// PokerTableController. This is the "Open app → Continue → back at the
    /// table" entry point for the primitive prototype scene.
    /// </summary>
    public sealed class PokerSessionBootstrap : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private PokerEnvironmentManager environmentManager;
        [SerializeField] private string environmentId = "MatesKitchen";
        [SerializeField] private int npcCount = 5;
        [SerializeField] private bool startOnAwake = true;

        private void Start()
        {
            if (startOnAwake)
                StartNewSession();
        }

        public void StartNewSession()
        {
            PokerEnvironmentDefinition env = environmentManager != null
                ? environmentManager.Find(environmentId)
                : null;

            TableRules rules = env != null && env.pokerDifficulty != null
                ? new TableRules(env.pokerDifficulty.smallBlind, env.pokerDifficulty.bigBlind, env.pokerDifficulty.startingStack)
                : TableRules.Default;

            var human = new HumanPlayer("human", "James", seat: 0, stack: rules.StartingStack);

            var npcs = new List<AIPlayer>();
            int count = Mathf.Clamp(npcCount, 1, 5);
            // Table nicknames shuffled each session so any personality can wear any name.
            string[] fallbackNames = { "Nickey 2-guns", "Tommy Gunn", "Stevie Wanders", "Jimmy two-hands", "Simmo Says" };
            for (int i = fallbackNames.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (fallbackNames[i], fallbackNames[j]) = (fallbackNames[j], fallbackNames[i]);
            }
            for (int i = 0; i < count; i++)
            {
                var profileAsset = env != null && env.npcPool != null && i < env.npcPool.Length ? env.npcPool[i] : null;
                AIProfile profile = profileAsset != null && profileAsset.aiProfile != null
                    ? profileAsset.aiProfile.ToProfile()
                    : DefaultProfile(i);
                string npcName = profileAsset != null ? profileAsset.displayName : fallbackNames[i];

                npcs.Add(new AIPlayer($"npc{i}", npcName, seat: i + 1, stack: rules.StartingStack,
                    profile: profile, seed: Random.Range(int.MinValue, int.MaxValue)));
            }

            if (env != null)
                environmentManager.Activate(env);

            controller.StartSession(rules, human, npcs);
        }

        private static AIProfile DefaultProfile(int index) => (index % 5) switch
        {
            0 => AIProfile.CallingStation(),
            1 => AIProfile.Rock(),
            2 => AIProfile.Maniac(),
            3 => AIProfile.Professional(),
            _ => AIProfile.Regular()
        };
    }
}
