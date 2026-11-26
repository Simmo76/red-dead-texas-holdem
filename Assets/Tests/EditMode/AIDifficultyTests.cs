using CinematicPoker.Engine.AI;
using NUnit.Framework;

namespace CinematicPoker.Tests.EditMode
{
    // Regression guard for PR #208 removal and product-owner request to restore HARD.
    [TestFixture]
    public sealed class AIDifficultyTests
    {
        [Test]
        public void Names_include_easy_normal_and_hard()
        {
            Assert.That(AIDifficulty.Names, Is.EqualTo(new[] { "EASY", "NORMAL", "HARD" }));
        }

        [Test]
        public void Normal_leaves_competence_unchanged()
        {
            AIProfile authored = AIProfile.Regular();
            authored.Skill = 0.62;
            authored.DecisionNoise = 0.11;

            AIProfile scaled = AIDifficulty.Apply(authored, AIDifficulty.Normal);

            Assert.That(scaled.Skill, Is.EqualTo(0.62).Within(1e-9));
            Assert.That(scaled.DecisionNoise, Is.EqualTo(0.11).Within(1e-9));
            Assert.That(scaled.Aggression, Is.EqualTo(authored.Aggression));
        }

        [Test]
        public void Easy_reduces_skill_and_adds_noise()
        {
            AIProfile authored = AIProfile.Regular();
            authored.Skill = 0.8;
            authored.DecisionNoise = 0.1;

            AIProfile scaled = AIDifficulty.Apply(authored, AIDifficulty.Easy);

            Assert.That(scaled.Skill, Is.EqualTo(0.36).Within(1e-9));
            Assert.That(scaled.DecisionNoise, Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void Hard_boosts_skill_and_cuts_noise()
        {
            AIProfile authored = AIProfile.Regular();
            authored.Skill = 0.5;
            authored.DecisionNoise = 0.2;
            authored.TiltSensitivity = 0.5;

            AIProfile scaled = AIDifficulty.Apply(authored, AIDifficulty.Hard);

            Assert.That(scaled.Skill, Is.EqualTo(0.775).Within(1e-9));
            Assert.That(scaled.DecisionNoise, Is.EqualTo(0.07).Within(1e-9));
            Assert.That(scaled.TiltSensitivity, Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void ClampIndex_accepts_restored_hard_save_slot()
        {
            Assert.That(AIDifficulty.ClampIndex(2), Is.EqualTo(AIDifficulty.Hard));
            Assert.That(AIDifficulty.ClampIndex(99), Is.EqualTo(AIDifficulty.Hard));
            Assert.That(AIDifficulty.ClampIndex(-1), Is.EqualTo(AIDifficulty.Easy));
        }

        [Test]
        public void Apply_does_not_mutate_source_profile()
        {
            AIProfile authored = AIProfile.Maniac();
            double skillBefore = authored.Skill;

            _ = AIDifficulty.Apply(authored, AIDifficulty.Easy);

            Assert.That(authored.Skill, Is.EqualTo(skillBefore));
        }
    }
}