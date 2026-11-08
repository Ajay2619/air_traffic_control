using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="HighScores"/> against real PlayerPrefs, using unique map ids that are deleted afterwards.</summary>
    public sealed class HighScoresTests
    {
        private string _mapId;
        private string _otherMapId;

        [SetUp]
        public void SetUp()
        {
            string guid = System.Guid.NewGuid().ToString("N");
            _mapId = "unit_test_" + guid;
            _otherMapId = "unit_test_other_" + guid;
        }

        [TearDown]
        public void TearDown()
        {
            HighScores.Clear(_mapId);
            HighScores.Clear(_otherMapId);
        }

        [Test]
        public void Key_UsesPrefixAndMapId()
        {
            Assert.That(HighScores.Key("green_valley"), Is.EqualTo(HighScores.KeyPrefix + "green_valley"));
            Assert.That(HighScores.Key(null), Is.EqualTo(HighScores.KeyPrefix));
        }

        [Test]
        public void Get_UnknownMap_IsZero()
        {
            Assert.That(HighScores.Get(_mapId), Is.EqualTo(0));
        }

        [Test]
        public void Submit_FirstPositiveScore_IsNewBestAndStored()
        {
            Assert.That(HighScores.Submit(_mapId, 7), Is.True);
            Assert.That(HighScores.Get(_mapId), Is.EqualTo(7));
            Assert.That(PlayerPrefs.GetInt(HighScores.Key(_mapId), -1), Is.EqualTo(7));
        }

        [Test]
        public void Submit_ZeroOnFreshMap_IsNotABest()
        {
            Assert.That(HighScores.Submit(_mapId, 0), Is.False);
            Assert.That(PlayerPrefs.HasKey(HighScores.Key(_mapId)), Is.False);
        }

        [Test]
        public void Submit_HigherScore_Replaces()
        {
            HighScores.Submit(_mapId, 5);
            Assert.That(HighScores.Submit(_mapId, 9), Is.True);
            Assert.That(HighScores.Get(_mapId), Is.EqualTo(9));
        }

        [TestCase(5)]
        [TestCase(3)]
        [TestCase(0)]
        public void Submit_EqualOrLowerScore_IsIgnored(int score)
        {
            HighScores.Submit(_mapId, 5);
            Assert.That(HighScores.Submit(_mapId, score), Is.False);
            Assert.That(HighScores.Get(_mapId), Is.EqualTo(5));
        }

        [Test]
        public void Clear_ResetsToZero()
        {
            HighScores.Submit(_mapId, 12);
            HighScores.Clear(_mapId);
            Assert.That(HighScores.Get(_mapId), Is.EqualTo(0));
            Assert.That(PlayerPrefs.HasKey(HighScores.Key(_mapId)), Is.False);
        }

        [Test]
        public void Maps_AreIndependent()
        {
            HighScores.Submit(_mapId, 4);
            HighScores.Submit(_otherMapId, 11);
            Assert.That(HighScores.Get(_mapId), Is.EqualTo(4));
            Assert.That(HighScores.Get(_otherMapId), Is.EqualTo(11));
            HighScores.Clear(_otherMapId);
            Assert.That(HighScores.Get(_mapId), Is.EqualTo(4));
        }
    }
}
