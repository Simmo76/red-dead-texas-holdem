using System;
using System.IO;
using CinematicPoker.Engine.Telemetry;
using UnityEngine;

namespace CinematicPoker.Game.Services
{
    /// <summary>
    /// Game-side entry point for analytics. Creates the tracker on first use,
    /// starts a session automatically on app launch, and persists buffered
    /// events as JSON Lines in persistentDataPath/analytics/ — so player
    /// behaviour is captured from day one, entirely on-device and free.
    ///
    /// When Unity Analytics (or another vendor) is adopted, its adapter is one
    /// extra IAnalyticsSink added here; no gameplay code changes, and the
    /// backlog of .jsonl files can be replayed for history.
    /// </summary>
    public static class TelemetryService
    {
        private static AnalyticsTracker _tracker;
        private static BufferedAnalyticsSink _buffer;

        public static AnalyticsTracker Tracker
        {
            get
            {
                if (_tracker == null)
                    Initialise();
                return _tracker;
            }
        }

        private static void Initialise()
        {
            _buffer = new BufferedAnalyticsSink();
            _tracker = new AnalyticsTracker(PlayerAccountService.LoadOrCreate().PlayerId);
            _tracker.AddSink(_buffer);
            _tracker.StartSession(new System.Collections.Generic.Dictionary<string, object>
            {
                { AnalyticsSchema.Params.AppVersion, Application.version },
                { AnalyticsSchema.Params.Platform, Application.platform.ToString() }
            });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallFlusher()
        {
            var go = new GameObject("TelemetryFlusher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<TelemetryFlusher>();
        }

        /// <summary>Appends all buffered events to today's .jsonl file. Safe to call at any time.</summary>
        public static void Flush()
        {
            if (_buffer == null)
                return;

            var lines = _buffer.DrainLines();
            if (lines.Count == 0)
                return;

            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "analytics");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"events-{DateTime.UtcNow:yyyyMMdd}.jsonl");
                File.AppendAllLines(path, lines);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Telemetry flush failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Flushes periodically and whenever the app pauses or quits — mobile
        /// apps are killed without warning, so losing more than the flush
        /// interval of data should be impossible.
        /// </summary>
        private sealed class TelemetryFlusher : MonoBehaviour
        {
            private const float FlushIntervalSeconds = 30f;
            private float _nextFlushTime;

            private void Awake()
            {
                // Touch the tracker so the session starts even before the first explicit Track call.
                _ = Tracker;
                _nextFlushTime = Time.realtimeSinceStartup + FlushIntervalSeconds;
            }

            private void Update()
            {
                if (Time.realtimeSinceStartup >= _nextFlushTime)
                {
                    Flush();
                    _nextFlushTime = Time.realtimeSinceStartup + FlushIntervalSeconds;
                }
            }

            private void OnApplicationPause(bool paused)
            {
                if (paused)
                {
                    // Backgrounding on mobile may be the last code that ever runs.
                    Tracker.EndSession();
                    Flush();
                }
                else
                {
                    Tracker.StartSession();
                }
            }

            private void OnApplicationQuit()
            {
                Tracker.EndSession();
                Flush();
            }
        }
    }
}
