using System;
using NUnit.Framework;
using Template.Services.Save;

namespace Template.Tests
{
    public class SaveServiceTests
    {
        [Serializable]
        private class Progress
        {
            public int Level;
            public string Skin;
        }

        [Test]
        public void InMemory_RoundTripsTypedData()
        {
            var save = new InMemorySaveService();
            save.Save("progress", new Progress { Level = 7, Skin = "red" });
            var loaded = save.Load<Progress>("progress");
            Assert.AreEqual(7, loaded.Level);
            Assert.AreEqual("red", loaded.Skin);
        }

        [Test]
        public void Load_MissingKey_ReturnsNewInstance()
        {
            var save = new InMemorySaveService();
            Assert.IsNotNull(save.Load<Progress>("missing"));
            Assert.IsFalse(save.HasKey("missing"));
        }

        [Test]
        public void PlayerData_Defaults_HaveSoundAndHapticsOn()
        {
            var data = new PlayerData();
            Assert.IsTrue(data.SoundEnabled);
            Assert.IsTrue(data.HapticsEnabled);
            Assert.AreEqual(0, data.SessionCount);
        }
    }
}
