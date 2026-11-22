using System;
using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Cài đặt cách quét: thư mục prefab-mục, thư mục bỏ qua, hàm chuyển scene tự viết, Claude Code CLI.</summary>
    internal sealed class SettingsPage : LogicMapPage
    {
        public const string PageId = "settings";

        public override string Id => PageId;
        public override string Title => Loc.T("Settings", "Cài đặt");
        public override string Subtitle => Loc.T(
            "Shared with the team in " + LogicMapSettings.AssetPath + ". Changes rescan the project.",
            "Dùng chung cho cả team, lưu ở " + LogicMapSettings.AssetPath + ". Đổi xong sẽ tự quét lại.");
        public override string Group => Loc.T("Other", "Khác");
        public override GnurtIconKind Icon => GnurtIconKind.Settings;

        static LogicMapSettings Settings => LogicMapSettings.Instance;

        public override void Build(VisualElement root)
        {
            root.Add(GnurtUI.Columns(PrefabFoldersCard(), OtherCard()));
            root.Add(GnurtUI.Columns(ClaudeCard(), DataCard()));
        }

        VisualElement PrefabFoldersCard()
        {
            var card = GnurtUI.Card(
                Loc.T("Maps / minigames as prefabs", "Map / minigame dạng prefab"),
                Loc.T("Each prefab directly inside these folders is one section of the game.",
                    "Mỗi prefab nằm ngay trong các thư mục này là một mục của game."));

            var auto = new Toggle
            {
                text = Loc.T("Find folders named Maps, Levels, MiniGames… automatically", "Tự tìm thư mục tên kiểu Maps, Levels, MiniGames…"),
                value = Settings.autoDetectPrefabFolders,
            };
            auto.AddToClassList("gnurt-field");
            auto.RegisterValueChangedCallback(e => Change(s => s.autoDetectPrefabFolders = e.newValue));
            card.Add(auto);

            if (Settings.autoDetectPrefabFolders)
            {
                var detected = ProjectScanner.DetectPrefabFolders(Settings).Where(f => !Settings.prefabFolders.Contains(f)).ToList();
                foreach (string folder in detected)
                    card.Add(GnurtUI.ListRow(null, folder, Loc.T("found automatically", "tự tìm thấy"), GnurtUI.Badge("auto")));
            }
            AddFolderList(card, Settings.prefabFolders, Loc.T("Add folder…", "Thêm thư mục…"));
            return card;
        }

        VisualElement OtherCard()
        {
            var card = GnurtUI.Card(Loc.T("Scanning", "Cách quét"));

            card.Add(GnurtUI.Text(Loc.T("Ignored folders", "Thư mục bỏ qua"), "lm-detail__subhead"));
            card.Add(GnurtUI.Hint(Loc.T("Scenes, prefabs and scripts inside are skipped (plugins, samples…).",
                "Bỏ qua scene, prefab, script nằm trong đó (plugin, sample…).")));
            AddFolderList(card, Settings.ignoredFolders, Loc.T("Add folder…", "Thêm thư mục…"));

            card.Add(GnurtUI.Text(Loc.T("Own scene-loading functions", "Hàm chuyển scene tự viết"), "lm-detail__subhead"));
            card.Add(GnurtUI.Hint(Loc.T(
                "LoadScene and LoadSceneAsync are always read. Add your wrappers, comma separated, e.g. GoToScene, LoadMap.",
                "LoadScene, LoadSceneAsync luôn được đọc. Thêm hàm bọc của game, cách nhau dấu phẩy, vd GoToScene, LoadMap.")));
            var methods = new TextField { value = string.Join(", ", Settings.extraLoadMethods) };
            methods.AddToClassList("gnurt-field");
            card.Add(methods);
            card.Add(GnurtUI.Row(GnurtUI.Button(Loc.T("Apply", "Áp dụng"), () => Change(s =>
            {
                s.extraLoadMethods = methods.value.Split(',', ';', ' ')
                    .Select(m => m.Trim()).Where(m => m.Length > 0).Distinct().ToList();
            }))));
            return card;
        }

        VisualElement ClaudeCard()
        {
            var card = GnurtUI.Card("Claude Code CLI", Loc.T(
                "LogicMap looks for the claude command to turn on the chat area. Saved per user, not committed.",
                "LogicMap tìm lệnh claude để bật vùng chat. Lưu riêng cho từng người, không commit."));
            card.Add(GnurtUI.Inline(new ClaudeBadge()));
            if (ClaudeCli.ExecutablePath != null) card.Add(GnurtUI.KeyValue(Loc.T("Found at", "Tìm thấy ở"), ClaudeCli.ExecutablePath));

            var path = new TextField(Loc.T("Custom path", "Đường dẫn tự chọn")) { value = ClaudeCli.CustomPath ?? "" };
            path.AddToClassList("gnurt-field");
            card.Add(path);
            card.Add(GnurtUI.Row(
                GnurtUI.Button(Loc.T("Browse…", "Chọn file…"), () =>
                {
                    string file = EditorUtility.OpenFilePanel(Loc.T("Find claude", "Tìm lệnh claude"), "", "");
                    if (string.IsNullOrEmpty(file)) return;
                    path.value = file;
                }),
                GnurtUI.Button(Loc.T("Save and check", "Lưu và kiểm tra"), () =>
                {
                    ClaudeCli.CustomPath = path.value;
                    ClaudeCli.Check();
                    Window.RefreshPage();
                }, primary: true)));
            card.Add(new ClaudeInstallControls());
            return card;
        }

        VisualElement DataCard()
        {
            var card = GnurtUI.Card(Loc.T("Data", "Dữ liệu"));
            card.Add(GnurtUI.KeyValue(Loc.T("Settings", "Cài đặt"),
                LogicMapSettings.Exists ? LogicMapSettings.AssetPath : Loc.T("default (no file yet)", "mặc định (chưa có file)")));
            card.Add(GnurtUI.KeyValue(Loc.T("Last scan", "Lần quét cuối"), ProjectScanner.Current?.scannedAt ?? "—"));
            card.Add(GnurtUI.Hint(Loc.T(
                "The scan result is kept in Library/ (not committed). Rescan after changing scenes, prefabs or scripts.",
                "Kết quả quét nằm trong Library/ (không commit). Đổi scene, prefab hay script xong thì quét lại.")));
            card.Add(GnurtUI.Row(GnurtUI.Button(Loc.T("Rescan", "Quét lại"), ProjectScanner.Rescan, primary: true)));
            return card;
        }

        // Danh sách thư mục có nút xoá từng dòng và nút thêm.
        void AddFolderList(VisualElement card, List<string> folders, string addText)
        {
            foreach (string folder in folders.ToList())
            {
                string current = folder;
                var remove = GnurtUI.Button(Loc.T("Remove", "Bỏ"), () => Change(_ => folders.Remove(current)));
                card.Add(GnurtUI.ListRow(null, folder, null, remove));
            }
            card.Add(GnurtUI.Row(GnurtUI.Button(addText, () =>
            {
                string picked = EditorUtility.OpenFolderPanel(addText, Application.dataPath, "");
                string folder = LogicMapSettings.NormalizeFolder(picked);
                if (picked.Length == 0) return;
                if (folder == null)
                {
                    EditorUtility.DisplayDialog("Gnurt LogicMap", Loc.T("Pick a folder inside Assets/.", "Hãy chọn thư mục nằm trong Assets/."), "OK");
                    return;
                }
                if (!folders.Contains(folder)) Change(_ => folders.Add(folder));
            })));
        }

        void Change(Action<LogicMapSettings> edit)
        {
            var settings = Settings;
            Undo.RecordObject(settings, "LogicMap Settings");
            edit(settings);
            settings.Save();
            ProjectScanner.Rescan(); // trang sẽ tự vẽ lại khi có kết quả mới
            Window.RefreshPage();
        }
    }
}
