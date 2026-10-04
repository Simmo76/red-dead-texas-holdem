using System.Collections.Generic;

namespace CinematicPoker.Engine.Telemetry
{
    /// <summary>
    /// The offline-first sink: serialises every event to a JSON line and holds
    /// it in a bounded in-memory queue. The Unity layer periodically drains the
    /// queue and appends the lines to a .jsonl file in persistentDataPath (and,
    /// once a vendor is wired up, forwards them). Bounded so a marathon
    /// offline session can never grow memory without limit — the oldest lines
    /// are dropped first and counted.
    /// </summary>
    public sealed class BufferedAnalyticsSink : IAnalyticsSink
    {
        public const int DefaultCapacity = 5000;

        private readonly object _gate = new object();
        private readonly Queue<string> _lines = new Queue<string>();
        private readonly int _capacity;

        public long DroppedCount { get; private set; }

        public int Count
        {
            get { lock (_gate) return _lines.Count; }
        }

        public BufferedAnalyticsSink(int capacity = DefaultCapacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public void Record(AnalyticsEvent analyticsEvent)
        {
            string line = AnalyticsJson.Serialize(analyticsEvent);
            lock (_gate)
            {
                _lines.Enqueue(line);
                while (_lines.Count > _capacity)
                {
                    _lines.Dequeue();
                    DroppedCount++;
                }
            }
        }

        /// <summary>Returns all buffered lines and clears the buffer.</summary>
        public IReadOnlyList<string> DrainLines()
        {
            lock (_gate)
            {
                var drained = _lines.ToArray();
                _lines.Clear();
                return drained;
            }
        }
    }
}
