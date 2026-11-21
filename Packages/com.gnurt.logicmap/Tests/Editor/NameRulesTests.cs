using System.Linq;
using NUnit.Framework;

namespace Gnurt.LogicMap.Tests
{
    public class NameRulesTests
    {
        [Test]
        public void Words_SplitsCaseDigitsAndSeparators()
        {
            CollectionAssert.AreEqual(new[] { "waiting", "room" }, NameRules.Words("0_WaitingRoom"));
            CollectionAssert.AreEqual(new[] { "ui", "manager" }, NameRules.Words("UIManager"));
            CollectionAssert.AreEqual(new[] { "map", "goc" }, NameRules.Words("Map 1_Goc"));
        }

        [TestCase("LoadingScene", true)]
        [TestCase("MainScene", true)]
        [TestCase("0_WaitingRoom", true)]
        [TestCase("Lobby", true)]
        [TestCase("Map 1", false)]
        [TestCase("7_LethalRebound", false)]
        [TestCase("GameScene", false)]
        public void GuessRole(string name, bool core) =>
            Assert.AreEqual(core ? SectionRole.Core : SectionRole.Gameplay, NameRules.GuessRole(name));

        [TestCase("Maps", true)]
        [TestCase("MiniGames", true)]
        [TestCase("Mini Games", true)]
        [TestCase("Levels", true)]
        [TestCase("UI", false)]
        [TestCase("Characters", false)]
        public void LooksLikeSectionFolder(string folder, bool expected) => Assert.AreEqual(expected, NameRules.LooksLikeSectionFolder(folder));

        [Test]
        public void NaturalSort_OrdersNumbersByValue()
        {
            var sorted = new[] { "Map 10", "Map 2", "map 1", "Map 1_v2", "Map 1" }.OrderBy(n => n, NameRules.Natural).ToArray();
            CollectionAssert.AreEqual(new[] { "map 1", "Map 1", "Map 1_v2", "Map 2", "Map 10" }, sorted);
        }
    }

    public class ClaudeCliLocatorTests
    {
        [Test]
        public void Windows_UsesPathThenKnownFolders()
        {
            var list = ClaudeCliLocator.Candidates(@"C:\Tools;""C:\Quoted Dir"";", @"C:\Users\me", @"C:\Users\me\AppData\Roaming", windows: true);
            Assert.AreEqual(@"C:\Tools\claude.exe", list[0]);
            Assert.AreEqual(@"C:\Tools\claude.cmd", list[1]);
            CollectionAssert.Contains(list, @"C:\Quoted Dir\claude.exe");
            CollectionAssert.Contains(list, System.IO.Path.Combine(@"C:\Users\me", ".local", "bin", "claude.exe"));
            CollectionAssert.Contains(list, System.IO.Path.Combine(@"C:\Users\me\AppData\Roaming", "npm", "claude.cmd"));
            Assert.AreEqual(list.Count, list.Distinct().Count(), "không lặp");
        }

        [Test]
        public void Unix_AddsHomebrewAndLocalBin()
        {
            var list = ClaudeCliLocator.Candidates("/usr/bin:/usr/local/bin", "/Users/me", null, windows: false);
            Assert.AreEqual("/usr/bin/claude", list[0].Replace('\\', '/'));
            Assert.IsTrue(list.Any(p => p.Replace('\\', '/') == "/opt/homebrew/bin/claude"));
            Assert.IsTrue(list.Any(p => p.Replace('\\', '/') == "/Users/me/.local/bin/claude"));
            Assert.AreEqual(1, list.Count(p => p.Replace('\\', '/') == "/usr/local/bin/claude"), "không lặp");
        }

        [TestCase("2.0.31 (Claude Code)\n", "2.0.31")]
        [TestCase("claude 1.2.3-beta.1", "1.2.3-beta.1")]
        [TestCase("command not found", null)]
        [TestCase(null, null)]
        public void ParseVersion(string output, string expected) => Assert.AreEqual(expected, ClaudeCliLocator.ParseVersion(output));
    }
}
