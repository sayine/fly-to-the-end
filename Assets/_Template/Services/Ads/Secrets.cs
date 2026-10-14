using UnityEngine;

namespace Template.Services.Ads
{
    /// <summary>
    /// Per-game secrets. The real asset lives at Assets/_Game/Configs/Resources/Secrets.asset and is
    /// git-ignored (CLAUDE.md rule 8). Copy Assets/_Game/Configs/Secrets.example.asset there
    /// (Tools/Template/Setup/Create Secrets From Example) and fill in the IDs.
    /// </summary>
    [CreateAssetMenu(menuName = "Template/Configs/Secrets", fileName = "Secrets")]
    public sealed class Secrets : ScriptableObject
    {
        public const string ResourcesPath = "Secrets";

        // The MAX SDK key lives in AppLovin > Integration Manager (AppLovinSettings), not here.
        [Header("MAX ad unit IDs (iOS)")]
        [SerializeField] private string _interstitialId;
        [SerializeField] private string _rewardedId;
        [SerializeField] private string _bannerId;
        [Header("Debug (development builds only)")]
        [Tooltip("Google UMP test device hashed ID (printed by the Google CMP SDK in the Xcode log). Forces the consent form on this device.")]
        [SerializeField] private string _googleTestDeviceHashedId;

        public string InterstitialId => _interstitialId;
        public string RewardedId => _rewardedId;
        public string BannerId => _bannerId;
        public string GoogleTestDeviceHashedId => _googleTestDeviceHashedId;

        /// <summary>Returns null when the git-ignored asset is missing (fresh clone): services fall back to mocks/no ads.</summary>
        public static Secrets Load() => Resources.Load<Secrets>(ResourcesPath);
    }
}
