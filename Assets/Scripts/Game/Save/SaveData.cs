using System;
using System.Collections.Generic;

namespace CinematicPoker.Game.Save
{
    /// <summary>
    /// Everything persisted on device. Progression is fully independent of
    /// internet access; there is no account and no server.
    /// Serialized with JsonUtility, so: public fields, [Serializable] types.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;

        // Player
        public long bankroll = 5000;
        public string playerName = "Player";

        // Progression
        public List<string> unlockedEnvironmentIds = new List<string>();
        public List<string> unlockedCharacterIds = new List<string>(); // Impossible Table roster
        public List<string> completedObjectiveIds = new List<string>();

        // Player poker statistics (local only)
        public int handsPlayed;
        public int handsWon;
        public int showdownsWon;
        public int biggestPotWon;

        // NPC memory that persists between sessions
        public List<NpcPersistentState> npcStates = new List<NpcPersistentState>();

        // Settings
        public int qualityMode = 1;       // QualityMode enum index
        public int playMode = 1;          // PlayMode enum index (default Cinematic)
        public bool improveMyGame;        // coaching opt-in ("Just Play" by default)
        public float musicVolume = 0.35f;
        public float sfxVolume = 1f;

        // Environment download state (Addressables keys already cached)
        public List<string> downloadedEnvironmentIds = new List<string>();

        // Mid-session snapshot (resume at the table after suspend/kill)
        public SessionSnapshot session;
    }

    [Serializable]
    public sealed class NpcPersistentState
    {
        public string npcId;
        public float relationshipWithPlayer; // -1..1
        public int handsAgainstPlayer;
        public int bluffsCaughtByPlayer;
        public List<string> memorableMoments = new List<string>();
    }

    /// <summary>
    /// Snapshot of an in-progress session, written between hands (and safe to
    /// write between actions later). Restores the table exactly enough that
    /// "Open app → Continue → back at the table" works offline.
    /// </summary>
    [Serializable]
    public sealed class SessionSnapshot
    {
        public string environmentId;
        public int handNumber;
        public int dealerSeat;
        public long smallBlind;
        public long bigBlind;
        public List<SeatSnapshot> seats = new List<SeatSnapshot>();
    }

    [Serializable]
    public sealed class SeatSnapshot
    {
        public int seat;
        public string npcId; // empty = human
        public long stack;
        public bool eliminated;
        public float tilt;
        public float confidence;
    }
}
