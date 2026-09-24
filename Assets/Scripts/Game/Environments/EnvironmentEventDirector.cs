using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace CinematicPoker.Game.Environments
{
    /// <summary>
    /// Schedules non-poker background events (a horse arrives, the fridge
    /// compressor kicks in, rain intensifies...). Purely atmospheric:
    /// these events must NEVER mutate poker state, and events marked
    /// betweenHandsOnly wait until no hand is in progress.
    /// </summary>
    public sealed class EnvironmentEventDirector : MonoBehaviour
    {
        [SerializeField] private AmbientEventProfile profile;

        /// <summary>Set by the table controller so we never interrupt a decision.</summary>
        public bool HandInProgress { get; set; }

        /// <summary>Scene hooks (VFX, audio, animation) subscribe per event id.</summary>
        public event Action<string> AmbientEventTriggered;

        private readonly List<float> _nextFireTimes = new List<float>();
        private Random _rng;

        public void Configure(AmbientEventProfile newProfile, int seed)
        {
            profile = newProfile;
            _rng = new Random(seed);
            _nextFireTimes.Clear();
            if (profile == null) return;
            for (int i = 0; i < profile.events.Length; i++)
                _nextFireTimes.Add(Time.time + SampleInterval(profile.events[i]));
        }

        private void Update()
        {
            if (profile == null || _rng == null) return;

            for (int i = 0; i < _nextFireTimes.Count; i++)
            {
                if (Time.time < _nextFireTimes[i]) continue;

                AmbientEventDefinition evt = profile.events[i];
                if (evt.betweenHandsOnly && HandInProgress)
                    continue; // try again next frame; fires between hands

                _nextFireTimes[i] = Time.time + SampleInterval(evt);
                if (_rng.NextDouble() <= evt.weight)
                    AmbientEventTriggered?.Invoke(evt.eventId);
            }
        }

        private float SampleInterval(AmbientEventDefinition evt)
        {
            // Exponential-ish jitter around the average so events never feel metronomic.
            double jitter = 0.5 + _rng.NextDouble();
            return (float)(evt.averageIntervalMinutes * 60.0 * jitter);
        }
    }
}
