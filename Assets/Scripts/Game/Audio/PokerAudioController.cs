using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Characters;
using CinematicPoker.Game.Presentation;
using UnityEngine;

namespace CinematicPoker.Game.Audio
{
    /// <summary>
    /// Shared table audio: cards, chips, and spatialised NPC speech (positioned
    /// at character heads). Environment ambience/music layers are configured
    /// from the environment's AudioProfile/MusicProfile and kept subtle so
    /// cards, chips and voices stay in front.
    /// </summary>
    public sealed class PokerAudioController : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private AudioSource tableSource;
        [SerializeField] private AudioSource voiceSource; // repositioned per speaker

        [Header("Shared clips")]
        [SerializeField] private AudioClip[] cardDealClips;
        [SerializeField] private AudioClip[] chipClips;
        [SerializeField] private AudioClip[] foldClips;

        private void OnEnable()
        {
            if (controller != null)
                controller.EngineEvent += OnEngineEvent;
            DialogueBus.Spoken += OnDialogue;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.EngineEvent -= OnEngineEvent;
            DialogueBus.Spoken -= OnDialogue;
        }

        private void OnEngineEvent(PokerEvent evt)
        {
            switch (evt)
            {
                case CardDealt _:
                case FlopDealt _:
                case TurnDealt _:
                case RiverDealt _:
                    PlayRandom(cardDealClips);
                    break;
                case BetPlaced _:
                case PlayerCalled _:
                case PlayerRaised _:
                case PlayerAllIn _:
                case PotAwarded _:
                    PlayRandom(chipClips);
                    break;
                case PlayerFolded _:
                    PlayRandom(foldClips);
                    break;
            }
        }

        private void OnDialogue(string speaker, string line, Transform voiceAnchor)
        {
            // Voice-over playback hooks in later; spatial position comes from
            // the speaking character's head.
            if (voiceSource != null && voiceAnchor != null)
                voiceSource.transform.position = voiceAnchor.position;
        }

        private void PlayRandom(AudioClip[] clips)
        {
            if (tableSource == null || clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
                tableSource.PlayOneShot(clip);
        }
    }
}
