using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Template.Tests
{
    /// <summary>Demo_Tilt loads standalone (no Boot) and the ball rests on the platform. Any logged error fails the test.</summary>
    public class DemoTiltSmokeTest
    {
        private const string DemoPath = "Assets/_Game/Scenes/Demo_Tilt.unity";

        [UnityTest]
        public IEnumerator DemoTilt_LoadsAndBallStaysOnPlatform()
        {
#if UNITY_EDITOR
            // Demo_Tilt is disabled in Build Settings (dev-only), so load it by path.
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                DemoPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only");
            yield break;
#endif
            var ball = GameObject.Find("Ball");
            Assert.IsNotNull(ball, "Ball missing in Demo_Tilt");
            yield return new WaitForSeconds(1f);
            Assert.Greater(ball.transform.position.y, 0f, "Ball fell through the platform");
            Assert.Less(Mathf.Abs(ball.transform.position.x) + Mathf.Abs(ball.transform.position.z), 0.5f,
                "Ball drifted without input");
        }
    }
}
