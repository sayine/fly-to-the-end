using NUnit.Framework;
using Template.Services.Ads;

namespace Template.Tests
{
    public class AdPolicyTests
    {
        private static AdPolicy Policy(int session) => new AdPolicy(AdRules.Default, session);

        [Test]
        public void FirstSession_Before90s_IsBlocked()
        {
            Assert.AreEqual(AdBlockReason.TooEarlyInFirstSessions, Policy(1).CheckInterstitial(30f));
            Assert.AreEqual(AdBlockReason.TooEarlyInFirstSessions, Policy(1).CheckInterstitial(89.9f));
        }

        [Test]
        public void FirstSession_After90s_IsAllowed()
        {
            Assert.IsTrue(Policy(1).CanShowInterstitial(90f));
        }

        [Test]
        public void SecondSession_IsAllowedImmediately()
        {
            Assert.IsTrue(Policy(2).CanShowInterstitial(1f));
        }

        [Test]
        public void Cooldown_45s_BetweenInterstitials()
        {
            var p = Policy(3);
            p.NotifyInterstitialShown(100f);
            Assert.AreEqual(AdBlockReason.Cooldown, p.CheckInterstitial(120f));
            Assert.AreEqual(AdBlockReason.Cooldown, p.CheckInterstitial(144.9f));
            Assert.IsTrue(p.CanShowInterstitial(145f));
        }

        [Test]
        public void GameplayActive_BlocksEvenWhenOtherwiseAllowed()
        {
            var p = Policy(5);
            p.GameplayActive = true;
            Assert.AreEqual(AdBlockReason.GameplayActive, p.CheckInterstitial(1000f));
            p.GameplayActive = false;
            Assert.IsTrue(p.CanShowInterstitial(1000f));
        }

        [Test]
        public void Rewarded_ResetsInterstitialCooldown_WhenConfigured()
        {
            var p = Policy(3);
            p.NotifyRewardedShown(200f);
            Assert.AreEqual(AdBlockReason.Cooldown, p.CheckInterstitial(210f));
            Assert.IsTrue(p.CanShowInterstitial(245f));
        }

        [Test]
        public void Rewarded_DoesNotResetCooldown_WhenDisabled()
        {
            var rules = AdRules.Default;
            rules.RewardedResetsInterstitialCooldown = false;
            var p = new AdPolicy(rules, 3);
            p.NotifyRewardedShown(200f);
            Assert.IsTrue(p.CanShowInterstitial(201f));
        }

        [Test]
        public void CustomRules_AreRespected()
        {
            var rules = new AdRules { InterstitialMinSession = 3, InterstitialMinSecondsInSession = 10f, InterstitialCooldownSeconds = 5f };
            var p = new AdPolicy(rules, 2);
            Assert.IsFalse(p.CanShowInterstitial(9f));
            Assert.IsTrue(p.CanShowInterstitial(10f));
            p.NotifyInterstitialShown(10f);
            Assert.IsFalse(p.CanShowInterstitial(14f));
            Assert.IsTrue(p.CanShowInterstitial(15f));
        }
    }
}
