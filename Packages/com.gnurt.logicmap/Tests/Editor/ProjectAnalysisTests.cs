using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Gnurt.LogicMap.Tests
{
    /// <summary>Dữ liệu quét giả lập cho test: scene, prefab, script dựng bằng tay.</summary>
    static class Fake
    {
        public static string T(string english, string vietnamese) => english;

        public static SectionInfo Scene(string name, int buildIndex, params string[] scripts) => new SectionInfo
        {
            path = $"Assets/Scenes/{name}.unity",
            name = name,
            kind = SectionKind.Scene,
            inBuild = buildIndex >= 0,
            buildIndex = buildIndex,
            scripts = scripts.Select(ScriptPath).ToList(),
        };

        public static SectionInfo Prefab(string name, params string[] scripts) => new SectionInfo
        {
            path = $"Assets/MiniGames/{name}.prefab",
            name = name,
            kind = SectionKind.Prefab,
            scripts = scripts.Select(ScriptPath).ToList(),
        };

        public static ScriptInfo Script(string name, bool ddol, params SceneLoadCall[] loads) => new ScriptInfo
        {
            path = ScriptPath(name),
            name = name,
            assembly = "Assembly-CSharp",
            dontDestroyOnLoad = ddol,
            loads = loads.ToList(),
        };

        public static SceneLoadCall Load(string target, TargetKind kind = TargetKind.Name, string member = "Go", int line = 1, string condition = null) => new SceneLoadCall
        {
            method = "LoadScene",
            target = target,
            targetKind = kind,
            buildIndex = kind == TargetKind.BuildIndex ? int.Parse(target) : -1,
            member = member,
            line = line,
            condition = condition,
            code = $"SceneManager.LoadScene({target});",
            argument = target,
        };

        public static string ScriptPath(string name) => $"Assets/Scripts/{name}.cs";

        /// <summary>Game chia theo scene: Loading → Menu → Map 1..3 → về Menu. Map 4 chưa vào build.</summary>
        public static ProjectScan SceneGame() => new ProjectScan
        {
            projectName = "Demo",
            sections =
            {
                Scene("Loading", 0, "Boot", "AudioManager"),
                Scene("Menu", 1, "MenuUI"),
                Scene("Map 1", 2, "GameUI"),
                Scene("Map 2", 3, "GameUI"),
                Scene("Map 3", 4, "GameUI"),
                Scene("Map 4", -1, "GameUI"),
            },
            scripts =
            {
                Script("Boot", false, Load("Menu")),
                Script("MenuUI", false, Load("Map *", TargetKind.Pattern, "OnPlay")),
                Script("GameUI", false, Load("Menu", member: "OnHome")),
                Script("AudioManager", true),
                Script("GameManager", true, Load("", TargetKind.Next, "NextLevel")),
            },
        };

        /// <summary>Game chia theo prefab: scene Game chứa 3 minigame prefab.</summary>
        public static ProjectScan PrefabGame()
        {
            var game = Scene("Game", 1, "MinigameHost");
            game.prefabs.AddRange(new[] { "Assets/MiniGames/Cut.prefab", "Assets/MiniGames/Jump.prefab", "Assets/MiniGames/Swim.prefab" });
            return new ProjectScan
            {
                sections = { Scene("Lobby", 0, "LobbyUI"), game, Prefab("Cut"), Prefab("Jump"), Prefab("Swim") },
                scripts = { Script("LobbyUI", false, Load("Game")), Script("MinigameHost", false) },
            };
        }
    }

    public class ProjectAnalysisTests
    {
        [Test]
        public void SceneGame_IsSplitByScene()
        {
            var analysis = new ProjectAnalysis(Fake.SceneGame());
            Assert.AreEqual(ProjectStructure.Scenes, analysis.Structure);
            Assert.AreEqual("Loading", analysis.Entry.name);
            CollectionAssert.AreEqual(new[] { "Loading", "Menu" }, analysis.Sections(SectionRole.Core).Select(s => s.name).ToArray());
            CollectionAssert.AreEqual(new[] { "Map 1", "Map 2", "Map 3", "Map 4" }, analysis.Sections(SectionRole.Gameplay).Select(s => s.name).ToArray());
        }

        [Test]
        public void PrefabGame_IsSplitByPrefab()
        {
            var analysis = new ProjectAnalysis(Fake.PrefabGame());
            Assert.AreEqual(ProjectStructure.Prefabs, analysis.Structure);
            var cut = analysis.Find("Assets/MiniGames/Cut.prefab");
            Assert.AreEqual("Game", analysis.ContainersOf(cut).Single().name);
        }

        [Test]
        public void Pattern_MatchesOnlyScenesInBuild()
        {
            var analysis = new ProjectAnalysis(Fake.SceneGame());
            var play = analysis.Loads.Single(l => l.Script.name == "MenuUI");
            CollectionAssert.AreEqual(new[] { "Map 1", "Map 2", "Map 3" }, play.Targets.Select(t => t.name).ToArray());
        }

        [Test]
        public void SharedScript_CountsForEverySectionUsingIt()
        {
            var analysis = new ProjectAnalysis(Fake.SceneGame());
            var homeLoads = analysis.Loads.Where(l => l.Script.name == "GameUI").ToList();
            CollectionAssert.AreEquivalent(new[] { "Map 1", "Map 2", "Map 3", "Map 4" }, homeLoads.Select(l => l.Source.name).ToArray());
            Assert.IsTrue(homeLoads.All(l => l.Targets.Single().name == "Menu"));
        }

        [Test]
        public void DontDestroyOnLoadScript_IsGlobalSource()
        {
            var analysis = new ProjectAnalysis(Fake.SceneGame());
            var next = analysis.Loads.Single(l => l.Script.name == "GameManager");
            Assert.IsNull(next.Source);
            Assert.IsEmpty(next.Targets, "scene kế tiếp chỉ biết khi có scene nguồn");
        }

        [Test]
        public void Resolve_NamesPathsIndexesNextAndReload()
        {
            var analysis = new ProjectAnalysis(Fake.SceneGame());
            var map1 = analysis.Find("Assets/Scenes/Map 1.unity");
            string Resolve(string target, TargetKind kind) =>
                string.Join(",", analysis.Resolve(Fake.Load(target, kind), map1).Select(s => s.name));

            Assert.AreEqual("Menu", Resolve("menu", TargetKind.Name));
            Assert.AreEqual("Menu", Resolve("Scenes/Menu", TargetKind.Name));
            Assert.AreEqual("Menu", Resolve("Assets/Scenes/Menu.unity", TargetKind.Name));
            Assert.AreEqual("", Resolve("Other/Menu", TargetKind.Name));
            Assert.AreEqual("Menu", Resolve("1", TargetKind.BuildIndex));
            Assert.AreEqual("Map 2", Resolve("", TargetKind.Next));
            Assert.AreEqual("Map 1", Resolve("", TargetKind.Reload));
            Assert.AreEqual("", Resolve("x", TargetKind.Unknown));
        }

        [Test]
        public void Scan_SurvivesJsonRoundTrip()
        {
            var scan = Fake.SceneGame();
            var copy = JsonUtility.FromJson<ProjectScan>(JsonUtility.ToJson(scan));
            Assert.AreEqual(scan.sections.Count, copy.sections.Count);
            Assert.AreEqual(TargetKind.Pattern, copy.scripts[1].loads[0].targetKind);
            Assert.AreEqual("Map *", copy.scripts[1].loads[0].target);
            Assert.AreEqual(-1, copy.sections[5].buildIndex);
        }
    }

    public class FlowGraphsTests
    {
        [Test]
        public void Overview_StartsAtFirstSceneAndFoldsMapsWithSameFlow()
        {
            var analysis = new ProjectAnalysis(Fake.SceneGame());
            var graph = FlowGraphs.Overview(analysis, Fake.T);

            Assert.IsNotNull(graph.Edges.SingleOrDefault(e => e.From == FlowGraphs.EntryId && e.To == "Assets/Scenes/Loading.unity"));
            Assert.AreEqual("Boot", graph.Edges.Single(e => e.From == "Assets/Scenes/Loading.unity").Label);

            // Map 1..3 cùng từ Menu tới và cùng về Menu: gộp thành một ô. Map 4 ngoài build nên không có mặt.
            var group = graph.Nodes.Single(n => n.Kind == NodeKind.Group);
            CollectionAssert.AreEqual(new[] { "Map 1", "Map 2", "Map 3" }, group.Items.Select(i => i.Title).ToArray());
            Assert.IsNull(graph.Find("Assets/Scenes/Map 1.unity"));
            Assert.IsNull(graph.Find("Assets/Scenes/Map 4.unity"));
            Assert.IsNotNull(graph.Edges.SingleOrDefault(e => e.From == "Assets/Scenes/Menu.unity" && e.To == group.Id));
            Assert.IsNotNull(graph.Edges.SingleOrDefault(e => e.From == group.Id && e.To == "Assets/Scenes/Menu.unity"));

            // Script DontDestroyOnLoad có lệnh chuyển scene chưa rõ đích.
            Assert.IsNotNull(graph.Edges.SingleOrDefault(e => e.From == "script:Assets/Scripts/GameManager.cs" && e.To == FlowGraphs.UnknownId));
        }

        [Test]
        public void Overview_PrefabGameShowsContainsEdges()
        {
            var analysis = new ProjectAnalysis(Fake.PrefabGame());
            var graph = FlowGraphs.Overview(analysis, Fake.T);
            var group = graph.Nodes.Single(n => n.Kind == NodeKind.Group);
            Assert.AreEqual(NodeTone.Prefab, group.Tone);
            var contains = graph.Edges.Single(e => e.To == group.Id);
            Assert.AreEqual("Assets/Scenes/Game.unity", contains.From);
            Assert.AreEqual(EdgeStyle.Dashed, contains.Style);
        }

        [Test]
        public void Section_ShowsEntriesExitsAndGroupsManyTargets()
        {
            var analysis = new ProjectAnalysis(Fake.SceneGame());
            var menu = analysis.Find("Assets/Scenes/Menu.unity");
            var graph = FlowGraphs.Section(analysis, menu, Fake.T);

            var self = graph.Find(menu.path);
            Assert.IsTrue(self.Highlight);
            CollectionAssert.AreEquivalent(
                new[] { "in:Assets/Scenes/Loading.unity", "in:Assets/Scenes/Map 1.unity", "in:Assets/Scenes/Map 2.unity", "in:Assets/Scenes/Map 3.unity", "in:Assets/Scenes/Map 4.unity" },
                graph.Edges.Where(e => e.To == self.Id).Select(e => e.From).ToArray());

            var call = graph.Find("call:Assets/Scripts/MenuUI.cs|OnPlay");
            Assert.AreEqual(NodeKind.Process, call.Kind);
            Assert.AreEqual("OnPlay()", call.Title);
            var target = graph.Find(graph.Edges.Single(e => e.From == call.Id).To);
            Assert.AreEqual(NodeKind.Group, target.Kind);
            Assert.AreEqual(3, target.Items.Count);
        }

        [Test]
        public void Section_FunctionWithSeveralTargetsIsDecision()
        {
            var scan = Fake.SceneGame();
            scan.scripts.Single(s => s.name == "GameUI").loads.Add(Fake.Load("Loading", member: "OnHome", line: 9, condition: "lives == 0"));
            var analysis = new ProjectAnalysis(scan);
            var graph = FlowGraphs.Section(analysis, analysis.Find("Assets/Scenes/Map 2.unity"), Fake.T);

            var decision = graph.Find("call:Assets/Scripts/GameUI.cs|OnHome");
            Assert.AreEqual(NodeKind.Decision, decision.Kind);
            var branches = graph.Edges.Where(e => e.From == decision.Id).ToList();
            Assert.AreEqual(2, branches.Count);
            Assert.AreEqual("lives == 0", branches.Single(e => e.To == "out:Assets/Scenes/Loading.unity").Label);
        }
    }
}
