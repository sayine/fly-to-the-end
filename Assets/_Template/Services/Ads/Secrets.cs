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

        [SerializeField] private string _maxSdkKey;
        [Header("iOS ad units")]
        [SerializeField] private string _interstitialId;
        [SerializeField] private string _rewardedId;
        [SerializeField] private string _bannerId;

        public string MaxSdkKey => _maxSdkKey;

        public string InterstitialId => _interstitialId;
        public string RewardedId => _rewardedId;
        public string BannerId => _bannerId;

        /// <summary>Returns null when the git-ignored asset is missing (fresh clone): services fall back to mocks/no ads.</summary>
        public static Secrets Load() => Resources.Load<Secrets>(ResourcesPath);
    }
}
