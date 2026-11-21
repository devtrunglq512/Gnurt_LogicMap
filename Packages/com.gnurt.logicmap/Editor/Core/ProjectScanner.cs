using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Quét project (Build Settings, scene, prefab, script) và giữ kết quả.
    /// Bản sao lưu ở Library/ nên mở lại Unity không phải quét lại; muốn cập nhật thì bấm Quét lại.
    /// </summary>
    internal static class ProjectScanner
    {
        const string CachePath = "Library/Gnurt/LogicMap/Scan.json";
        static readonly GLogger Log = GLog.Tag("LogicMap");

        static ProjectScan _scan;
        static ProjectAnalysis _analysis;
        static bool _loaded;

        /// <summary>Bắn khi có kết quả quét mới.</summary>
        public static event Action Changed;

        public static ProjectScan Current
        {
            get
            {
                EnsureLoaded();
                return _scan;
            }
        }

        public static ProjectAnalysis Analysis
        {
            get
            {
                EnsureLoaded();
                return _analysis;
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(CachePath)) return;
                var scan = JsonUtility.FromJson<ProjectScan>(File.ReadAllText(CachePath));
                if (scan != null && scan.version == ProjectScan.CurrentVersion) Apply(scan);
            }
            catch (Exception e)
            {
                Log.Warning("Không đọc được kết quả quét cũ, sẽ quét lại: " + e.Message);
            }
        }

        /// <summary>Quét lại cả project (có thanh tiến trình, bấm Cancel để dừng).</summary>
        public static void Rescan()
        {
            ProjectScan scan;
            try
            {
                scan = Scan(LogicMapSettings.Instance);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (scan == null) return; // người dùng huỷ

            _loaded = true;
            Apply(scan);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                File.WriteAllText(CachePath, JsonUtility.ToJson(scan));
            }
            catch (Exception e)
            {
                Log.Warning("Không lưu được kết quả quét: " + e.Message);
            }
            Changed?.Invoke();
        }

        static void Apply(ProjectScan scan)
        {
            _scan = scan;
            _analysis = new ProjectAnalysis(scan);
        }

        static bool Progress(string info, float value) =>
            EditorUtility.DisplayCancelableProgressBar("Gnurt LogicMap", info, value);

        /// <summary>Quét và trả kết quả. null nếu người dùng huỷ.</summary>
        static ProjectScan Scan(LogicMapSettings settings)
        {
            var scan = new ProjectScan
            {
                projectName = PlayerSettings.productName,
                scannedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            var sections = new Dictionary<string, SectionInfo>();

            // 1. Scene trong Build Settings. Số thứ tự chỉ đếm scene đang bật, giống SceneManager.
            int buildIndex = 0;
            foreach (var entry in EditorBuildSettings.scenes)
            {
                string path = entry.path;
                if (string.IsNullOrEmpty(path) || sections.ContainsKey(path) || !File.Exists(path)) continue;
                int index = entry.enabled ? buildIndex++ : -1;
                if (settings.IsIgnored(path)) continue;
                sections[path] = new SectionInfo
                {
                    path = path,
                    name = Path.GetFileNameWithoutExtension(path),
                    kind = SectionKind.Scene,
                    inBuild = entry.enabled,
                    buildIndex = index,
                };
            }

            // 2. Scene chưa vào build nhưng nằm cùng thư mục với scene trong build (map đang làm, bản thử...).
            var sceneFolders = sections.Values.Select(s => FolderOf(s.path)).Where(f => f.StartsWith("Assets")).Distinct().ToArray();
            if (sceneFolders.Length > 0)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", sceneFolders))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (sections.ContainsKey(path) || !sceneFolders.Contains(FolderOf(path)) || settings.IsIgnored(path)) continue;
                    sections[path] = new SectionInfo { path = path, name = Path.GetFileNameWithoutExtension(path), kind = SectionKind.Scene };
                }
            }

            // 3. Prefab đóng vai map/minigame: mỗi prefab nằm ngay trong thư mục đã chọn là một mục.
            var prefabFolders = new List<string>();
            foreach (string folder in settings.prefabFolders.Select(LogicMapSettings.NormalizeFolder))
                if (folder != null && AssetDatabase.IsValidFolder(folder) && !prefabFolders.Contains(folder)) prefabFolders.Add(folder);
            if (settings.autoDetectPrefabFolders)
                foreach (string folder in DetectPrefabFolders(settings))
                    if (!prefabFolders.Contains(folder)) prefabFolders.Add(folder);
            scan.prefabFolders = prefabFolders;

            foreach (string folder in prefabFolders)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (sections.ContainsKey(path) || FolderOf(path) != folder || settings.IsIgnored(path)) continue;
                    sections[path] = new SectionInfo { path = path, name = Path.GetFileNameWithoutExtension(path), kind = SectionKind.Prefab };
                }
            }

            // 4. Mỗi mục dùng những script nào, chứa prefab-mục nào (tính cả prefab lồng bên trong).
            var prefabPaths = new HashSet<string>(sections.Values.Where(s => s.kind == SectionKind.Prefab).Select(s => s.path));
            int done = 0;
            foreach (var section in sections.Values)
            {
                if (Progress(section.name, 0.5f * done++ / sections.Count)) return null;
                var dependencies = AssetDatabase.GetDependencies(section.path, true);
                section.scripts = dependencies
                    .Where(d => d.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && d.StartsWith("Assets/") && !settings.IsIgnored(d))
                    .OrderBy(d => d, NameRules.Natural)
                    .ToList();
                section.prefabs = dependencies.Where(d => d != section.path && prefabPaths.Contains(d)).OrderBy(d => d, NameRules.Natural).ToList();
            }
            scan.sections = ProjectAnalysis.Ordered(sections.Values).ToList();

            // 5. Đọc từng script: lệnh chuyển scene, DontDestroyOnLoad, hằng chuỗi (để hiểu LoadScene(SceneNames.Menu)).
            var methods = SceneLoadParser.DefaultMethods.Concat(settings.extraLoadMethods.Where(m => !string.IsNullOrWhiteSpace(m))).ToList();
            var scriptPaths = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !settings.IsIgnored(p))
                .Distinct()
                .OrderBy(p => p, NameRules.Natural)
                .ToList();

            var facts = new Dictionary<string, ScriptFacts>();
            var constants = new Dictionary<string, string>();
            var ambiguous = new HashSet<string>();
            done = 0;
            foreach (string path in scriptPaths)
            {
                if (Progress(Path.GetFileName(path), 0.5f + 0.5f * done++ / Math.Max(1, scriptPaths.Count))) return null;
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    Log.Warning($"Không đọc được {path}: {e.Message}");
                    continue;
                }
                var result = SceneLoadParser.Parse(text, methods);
                facts[path] = result;
                foreach (var pair in result.Constants)
                {
                    if (constants.TryGetValue(pair.Key, out string existing) && existing != pair.Value) ambiguous.Add(pair.Key);
                    else constants[pair.Key] = pair.Value;
                }
            }
            foreach (string key in ambiguous) constants.Remove(key); // trùng tên khác giá trị thì không đoán

            var used = new HashSet<string>(sections.Values.SelectMany(s => s.scripts));
            foreach (var pair in facts)
            {
                var result = pair.Value;
                if (result.Loads.Count == 0 && !result.DontDestroyOnLoad && !used.Contains(pair.Key)) continue;

                var info = new ScriptInfo
                {
                    path = pair.Key,
                    name = Path.GetFileNameWithoutExtension(pair.Key),
                    assembly = AssemblyOf(pair.Key),
                    dontDestroyOnLoad = result.DontDestroyOnLoad,
                };
                foreach (var call in result.Loads)
                {
                    SceneLoadParser.Classify(call, constants);
                    info.loads.Add(call);
                }
                scan.scripts.Add(info);
            }
            return scan;
        }

        /// <summary>Thư mục có tên kiểu Maps, Levels, MiniGames và chứa từ 2 prefab trở lên.</summary>
        public static List<string> DetectPrefabFolders(LogicMapSettings settings)
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !settings.IsIgnored(p))
                .GroupBy(FolderOf)
                .Where(g => g.Count() >= 2 && NameRules.LooksLikeSectionFolder(Path.GetFileName(g.Key)))
                .Select(g => g.Key)
                .OrderBy(f => f, NameRules.Natural)
                .ToList();
        }

        static string FolderOf(string path) => (Path.GetDirectoryName(path) ?? "").Replace('\\', '/');

        static string AssemblyOf(string scriptPath)
        {
            string name = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath);
            if (string.IsNullOrEmpty(name)) return "Assembly-CSharp";
            return name.EndsWith(".dll") ? name.Substring(0, name.Length - 4) : name;
        }
    }
}
