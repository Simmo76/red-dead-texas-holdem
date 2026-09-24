using System;
using System.Collections.Generic;
using UnityEngine;

namespace CinematicPoker.Game.Environments
{
    /// <summary>
    /// Loads and activates environments. Owns the catalogue of installed
    /// PokerEnvironmentDefinitions and tracks download state for optional
    /// premium packs (via Addressables). Contains no poker logic whatsoever.
    /// </summary>
    public sealed class PokerEnvironmentManager : MonoBehaviour
    {
        [SerializeField] private PokerEnvironmentDefinition[] catalogue;

        public PokerEnvironmentDefinition Active { get; private set; }

        public event Action<PokerEnvironmentDefinition> EnvironmentActivated;

        public IReadOnlyList<PokerEnvironmentDefinition> Catalogue => catalogue;

        public PokerEnvironmentDefinition Find(string environmentId)
        {
            foreach (PokerEnvironmentDefinition def in catalogue)
                if (def != null && def.environmentId == environmentId)
                    return def;
            return null;
        }

        /// <summary>
        /// Activate an environment. The vertical slice loads scenes directly;
        /// premium packs will stream via Addressables using def.sceneAddress.
        /// </summary>
        public void Activate(PokerEnvironmentDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            Active = def;
            // Scene loading is intentionally thin for now:
            // Addressables.LoadSceneAsync(def.sceneAddress) once content packs exist.
            EnvironmentActivated?.Invoke(def);
        }
    }
}
