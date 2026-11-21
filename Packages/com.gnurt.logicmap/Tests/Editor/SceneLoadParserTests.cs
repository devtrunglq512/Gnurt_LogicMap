using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Gnurt.LogicMap.Tests
{
    public class SceneLoadParserTests
    {
        static List<SceneLoadCall> Loads(string code, Dictionary<string, string> constants = null, IEnumerable<string> methods = null)
        {
            var facts = SceneLoadParser.Parse(code, methods);
            var all = new Dictionary<string, string>(facts.Constants);
            if (constants != null)
                foreach (var pair in constants) all[pair.Key] = pair.Value;
            foreach (var call in facts.Loads) SceneLoadParser.Classify(call, all);
            return facts.Loads;
        }

        static SceneLoadCall Single(string body, Dictionary<string, string> constants = null)
        {
            var loads = Loads("class A { void Go() { " + body + " } }", constants);
            Assert.AreEqual(1, loads.Count, "số lệnh chuyển scene");
            return loads[0];
        }

        [Test]
        public void Literal_IsSceneName()
        {
            var call = Single("SceneManager.LoadScene(\"Map 1\");");
            Assert.AreEqual(TargetKind.Name, call.targetKind);
            Assert.AreEqual("Map 1", call.target);
            Assert.AreEqual("LoadScene", call.method);
            Assert.AreEqual("Go", call.member);
        }

        [Test]
        public void VerbatimAndEscapedLiterals()
        {
            Assert.AreEqual(@"Scenes\Menu", Single("SceneManager.LoadScene(@\"Scenes\\Menu\");").target);
            Assert.AreEqual("Say \"hi\"", Single("SceneManager.LoadScene(\"Say \\\"hi\\\"\");").target);
        }

        [Test]
        public void Number_IsBuildIndex()
        {
            var call = Single("SceneManager.LoadSceneAsync(2, LoadSceneMode.Single);");
            Assert.AreEqual(TargetKind.BuildIndex, call.targetKind);
            Assert.AreEqual(2, call.buildIndex);
        }

        [Test]
        public void Concatenation_BecomesPattern()
        {
            var call = Single("SceneManager.LoadScene(\"Map \" + level);");
            Assert.AreEqual(TargetKind.Pattern, call.targetKind);
            Assert.AreEqual("Map *", call.target);
        }

        [Test]
        public void Interpolation_BecomesPattern()
        {
            Assert.AreEqual("Level_*_v*", Single("SceneManager.LoadScene($\"Level_{index + 1}_v{(a ? \"x\" : \"y\")}\");").target);
        }

        [Test]
        public void StringFormat_BecomesPattern()
        {
            var call = Single("SceneManager.LoadScene(string.Format(\"Map {0}\", i));");
            Assert.AreEqual(TargetKind.Pattern, call.targetKind);
            Assert.AreEqual("Map *", call.target);
        }

        [Test]
        public void Constants_AreResolved()
        {
            var loads = Loads(@"
                public static class SceneNames { public const string Menu = ""MainMenu""; }
                class A {
                    void Back() { SceneManager.LoadScene(SceneNames.Menu); }
                    void Shop() { SceneManager.LoadScene(Remote.ShopScene); }
                }",
                new Dictionary<string, string> { { "Remote.ShopScene", "Shop" } });
            Assert.AreEqual("MainMenu", loads[0].target);
            Assert.AreEqual(TargetKind.Name, loads[0].targetKind);
            Assert.AreEqual("Shop", loads[1].target);
        }

        [Test]
        public void NameofAndEnumToString_UseTheName()
        {
            Assert.AreEqual("Lobby", Single("SceneManager.LoadScene(nameof(Scenes.Lobby));").target);
            Assert.AreEqual("Lobby", Single("SceneManager.LoadScene(SceneId.Lobby.ToString());").target);
        }

        [Test]
        public void NextAndReload_AreRecognised()
        {
            Assert.AreEqual(TargetKind.Next, Single("SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);").targetKind);
            Assert.AreEqual(TargetKind.Reload, Single("SceneManager.LoadScene(SceneManager.GetActiveScene().name);").targetKind);
        }

        [Test]
        public void Variable_IsUnknown()
        {
            var call = Single("SceneManager.LoadScene(sceneName);");
            Assert.AreEqual(TargetKind.Unknown, call.targetKind);
            Assert.AreEqual("sceneName", call.target);
        }

        [Test]
        public void IgnoresDeclarationsCommentsAndStrings()
        {
            var loads = Loads(@"
                class Loader {
                    // SceneManager.LoadScene(""Commented"");
                    /* SceneManager.LoadScene(""Block""); */
                    public void LoadScene(string name) { Debug.Log(""LoadScene(\""Nope\"")""); }
                    public IEnumerator LoadSceneAsync(string name, bool additive = false) { yield return null; }
                    Task<AsyncOperation> LoadLevel(int i) => null;
                }");
            Assert.AreEqual(0, loads.Count);
        }

        [Test]
        public void FindsCallsInLambdasAwaitAndYield()
        {
            var loads = Loads(@"
                class A {
                    void Start() { button.onClick.AddListener(() => SceneManager.LoadScene(""Shop"")); }
                    async void B() { await LoadSceneAsync(""Map 2""); }
                    IEnumerator C() { yield return SceneManager.LoadSceneAsync(""Map 3""); }
                }");
            CollectionAssert.AreEqual(new[] { "Shop", "Map 2", "Map 3" }, loads.Select(l => l.target).ToArray());
            CollectionAssert.AreEqual(new[] { "Start", "B", "C" }, loads.Select(l => l.member).ToArray());
        }

        [Test]
        public void ConditionsAreGuessed()
        {
            var loads = Loads(@"
                class Result {
                    void OnNext(int stars) {
                        if (stars >= 3)
                        {
                            SceneManager.LoadScene(""Reward"");
                        }
                        else if (lives == 0) SceneManager.LoadScene(""GameOver"");
                        else SceneManager.LoadScene(""Menu"");
                    }
                    void Pick(int mode) {
                        switch (mode) {
                            case 1: SceneManager.LoadScene(""Easy""); break;
                            case 2:
                                Log();
                                SceneManager.LoadScene(""Hard"");
                                break;
                        }
                    }
                }");
            CollectionAssert.AreEqual(new[] { "stars >= 3", "lives == 0", "else", "case 1", "case 2" },
                loads.Select(l => l.condition).ToArray());
        }

        [Test]
        public void LineNumbersAndCode()
        {
            var loads = Loads("class A {\n  void Go() {\n    SceneManager.LoadScene(\"X\"); // về menu\n  }\n}");
            Assert.AreEqual(3, loads[0].line);
            Assert.AreEqual("SceneManager.LoadScene(\"X\");", loads[0].code);
        }

        [Test]
        public void DetectsDontDestroyOnLoad()
        {
            Assert.IsTrue(SceneLoadParser.Parse("class M { void Awake() { DontDestroyOnLoad(gameObject); } }").DontDestroyOnLoad);
            Assert.IsFalse(SceneLoadParser.Parse("class M { // DontDestroyOnLoad(gameObject);\n }").DontDestroyOnLoad);
        }

        [Test]
        public void ExtraMethods_AreRead()
        {
            var loads = Loads("class A { void Go() { Router.GoToScene(\"Shop\"); } }",
                methods: SceneLoadParser.DefaultMethods.Concat(new[] { "GoToScene" }));
            Assert.AreEqual("Shop", loads.Single().target);
        }
    }
}
