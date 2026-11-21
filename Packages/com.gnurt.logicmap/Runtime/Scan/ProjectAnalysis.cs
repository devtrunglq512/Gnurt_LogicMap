using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gnurt.LogicMap
{
    /// <summary>Project chia nội dung game theo cách nào.</summary>
    internal enum ProjectStructure
    {
        Empty,       // chưa có scene nào trong Build Settings
        SingleScene, // gần như mọi thứ nằm trong một scene
        Scenes,      // mỗi map/level là một scene
        Prefabs,     // mỗi map/minigame là một prefab, nạp vào một scene chung
        Mixed,       // cả hai
    }

    /// <summary>Một lệnh chuyển scene đã biết đi từ đâu tới đâu.</summary>
    internal sealed class ResolvedLoad
    {
        public ScriptInfo Script;
        public SceneLoadCall Call;
        public SectionInfo Source; // null = script chung (DontDestroyOnLoad, hoặc không gắn vào mục nào)
        public readonly List<SectionInfo> Targets = new List<SectionInfo>();
    }

    /// <summary>Đọc kết quả quét: vai trò từng mục, cách chia project, ai chuyển sang ai.</summary>
    internal sealed class ProjectAnalysis
    {
        public readonly ProjectScan Scan;
        public readonly ProjectStructure Structure;
        public readonly SectionInfo Entry; // scene #0 trong Build Settings
        public readonly List<ResolvedLoad> Loads = new List<ResolvedLoad>();
        public readonly Dictionary<string, ScriptInfo> Scripts = new Dictionary<string, ScriptInfo>();

        readonly Dictionary<string, List<SectionInfo>> _users = new Dictionary<string, List<SectionInfo>>();
        readonly Dictionary<SectionInfo, SectionRole> _roles = new Dictionary<SectionInfo, SectionRole>();
        static readonly IReadOnlyList<SectionInfo> None = new SectionInfo[0];

        public ProjectAnalysis(ProjectScan scan)
        {
            Scan = scan ?? new ProjectScan();

            foreach (var section in Scan.sections)
            {
                _roles[section] = section.kind == SectionKind.Prefab ? SectionRole.Gameplay : NameRules.GuessRole(section.name);
                foreach (string script in section.scripts.Distinct())
                {
                    if (!_users.TryGetValue(script, out var list)) _users[script] = list = new List<SectionInfo>();
                    list.Add(section);
                }
            }
            foreach (var script in Scan.scripts) Scripts[script.path] = script;

            Entry = Scan.sections.FirstOrDefault(s => s.kind == SectionKind.Scene && s.inBuild && s.buildIndex == 0);

            foreach (var script in Scan.scripts)
            {
                if (script.loads.Count == 0) continue;
                // Script sống xuyên scene hoặc không gắn vào đâu thì chạy được ở mọi nơi: coi là nguồn chung.
                IEnumerable<SectionInfo> sources = IsGlobal(script) ? new SectionInfo[] { null } : (IEnumerable<SectionInfo>)UsersOf(script.path);
                foreach (var source in sources)
                    foreach (var call in script.loads)
                    {
                        var load = new ResolvedLoad { Script = script, Call = call, Source = source };
                        load.Targets.AddRange(Resolve(call, source));
                        Loads.Add(load);
                    }
            }

            Structure = Classify();
        }

        public SectionRole RoleOf(SectionInfo section) =>
            section != null && _roles.TryGetValue(section, out var role) ? role : SectionRole.Gameplay;

        /// <summary>Các mục có dùng script này.</summary>
        public IReadOnlyList<SectionInfo> UsersOf(string scriptPath) =>
            scriptPath != null && _users.TryGetValue(scriptPath, out var list) ? list : None;

        /// <summary>Script chạy được ở mọi nơi: có DontDestroyOnLoad, hoặc không gắn vào scene/prefab nào.</summary>
        public bool IsGlobal(ScriptInfo script) => script.dontDestroyOnLoad || UsersOf(script.path).Count == 0;

        public IEnumerable<SectionInfo> Sections(SectionRole role) =>
            Ordered(Scan.sections.Where(s => RoleOf(s) == role));

        /// <summary>Thứ tự hiển thị: scene trong build theo số thứ tự, rồi prefab, rồi scene ngoài build.</summary>
        public static IEnumerable<SectionInfo> Ordered(IEnumerable<SectionInfo> sections) =>
            sections
                .OrderBy(s => s.inBuild && s.kind == SectionKind.Scene ? 0 : s.kind == SectionKind.Prefab ? 1 : 2)
                .ThenBy(s => s.inBuild ? s.buildIndex : int.MaxValue)
                .ThenBy(s => s.name, NameRules.Natural);

        public SectionInfo Find(string path) => Scan.sections.FirstOrDefault(s => s.path == path);

        public IEnumerable<ResolvedLoad> ExitsOf(SectionInfo section) => Loads.Where(l => l.Source == section);

        /// <summary>Các lệnh dẫn tới mục này từ chỗ khác (không tính tải lại chính nó).</summary>
        public IEnumerable<ResolvedLoad> EntriesOf(SectionInfo section) =>
            Loads.Where(l => l.Source != section && l.Targets.Contains(section));

        /// <summary>Các scene chứa (hoặc tham chiếu) prefab-mục này.</summary>
        public IEnumerable<SectionInfo> ContainersOf(SectionInfo section) =>
            Scan.sections.Where(s => s.prefabs.Contains(section.path));

        public List<SectionInfo> Resolve(SceneLoadCall call, SectionInfo source)
        {
            var scenes = Scan.sections.Where(s => s.kind == SectionKind.Scene).ToList();
            List<SectionInfo> found;
            switch (call.targetKind)
            {
                case TargetKind.Name:
                    found = scenes.Where(s => SameScene(s, call.target)).ToList();
                    break;
                case TargetKind.BuildIndex:
                    found = scenes.Where(s => s.inBuild && s.buildIndex == call.buildIndex).ToList();
                    break;
                case TargetKind.Pattern:
                    var regex = new Regex("^" + string.Join(".*", call.target.Split('*').Select(Regex.Escape)) + "$", RegexOptions.IgnoreCase);
                    found = scenes.Where(s => regex.IsMatch(s.name)).ToList();
                    break;
                case TargetKind.Next:
                    found = source != null && source.kind == SectionKind.Scene && source.inBuild
                        ? scenes.Where(s => s.inBuild && s.buildIndex == source.buildIndex + 1).ToList()
                        : new List<SectionInfo>();
                    break;
                case TargetKind.Reload:
                    found = source != null && source.kind == SectionKind.Scene ? new List<SectionInfo> { source } : new List<SectionInfo>();
                    break;
                default:
                    found = new List<SectionInfo>();
                    break;
            }

            // Unity chỉ nạp được scene có trong build: có bản trong build thì bỏ các bản trùng tên ở ngoài.
            if (found.Any(s => s.inBuild)) found = found.Where(s => s.inBuild).ToList();
            return Ordered(found).ToList();
        }

        // LoadScene nhận tên ("Map 1"), hoặc đường dẫn ("Scenes/Map 1", "Assets/Scenes/Map 1.unity").
        static bool SameScene(SectionInfo scene, string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            string t = target.Replace('\\', '/');
            if (t.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 6);
            if (string.Equals(scene.name, t, StringComparison.OrdinalIgnoreCase)) return true;
            if (!t.Contains("/")) return false;
            string path = scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? scene.path.Substring(0, scene.path.Length - 6) : scene.path;
            return path.EndsWith("/" + t, StringComparison.OrdinalIgnoreCase) || string.Equals(path, t, StringComparison.OrdinalIgnoreCase);
        }

        ProjectStructure Classify()
        {
            int gameplayScenes = Scan.sections.Count(s => s.kind == SectionKind.Scene && s.inBuild && RoleOf(s) == SectionRole.Gameplay);
            int prefabs = Scan.sections.Count(s => s.kind == SectionKind.Prefab);
            if (!Scan.sections.Any(s => s.inBuild || s.kind == SectionKind.Prefab)) return ProjectStructure.Empty;
            if (prefabs >= 2 && gameplayScenes <= 1) return ProjectStructure.Prefabs;
            if (gameplayScenes >= 2 && prefabs >= 2) return ProjectStructure.Mixed;
            if (gameplayScenes >= 2) return ProjectStructure.Scenes;
            return ProjectStructure.SingleScene;
        }
    }
}
