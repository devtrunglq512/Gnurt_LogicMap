using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Cài đặt dùng chung cho cả team, lưu ở <see cref="AssetPath"/> và commit cùng game.
    /// Chỉ tạo file khi có người đổi cài đặt; chưa có file thì dùng mặc định.
    /// </summary>
    internal sealed class LogicMapSettings : ScriptableObject
    {
        public const string Folder = "Assets/GnurtLogicMap";
        public const string AssetPath = Folder + "/GnurtLogicMapSettings.asset";

        [Tooltip("Tự tìm thư mục prefab có tên kiểu Maps, Levels, MiniGames.")]
        public bool autoDetectPrefabFolders = true;

        [Tooltip("Thư mục mà mỗi prefab bên trong là một map/minigame.")]
        public List<string> prefabFolders = new List<string>();

        [Tooltip("Bỏ qua scene, prefab và script nằm trong các thư mục này.")]
        public List<string> ignoredFolders = new List<string>();

        [Tooltip("Tên hàm chuyển scene tự viết, ngoài LoadScene/LoadSceneAsync có sẵn.")]
        public List<string> extraLoadMethods = new List<string>();

        static LogicMapSettings _instance;

        public static LogicMapSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = AssetDatabase.LoadAssetAtPath<LogicMapSettings>(AssetPath);
                if (_instance == null)
                {
                    _instance = CreateInstance<LogicMapSettings>();
                    _instance.hideFlags = HideFlags.DontSave;
                }
                return _instance;
            }
        }

        /// <summary>Đã có file cài đặt trong project chưa.</summary>
        public static bool Exists => File.Exists(AssetPath);

        /// <summary>Lưu thay đổi. Lần đầu thì tạo file trong Assets/GnurtLogicMap/.</summary>
        public void Save()
        {
            if (!AssetDatabase.Contains(this))
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "GnurtLogicMap");
                hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(this, AssetPath);
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Đường dẫn thư mục dạng "Assets/A/B", không có dấu / ở cuối.</summary>
        public static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;
            string path = folder.Trim().Replace('\\', '/').TrimEnd('/');
            string data = Application.dataPath.Replace('\\', '/');
            if (path.StartsWith(data)) path = "Assets" + path.Substring(data.Length);
            return path.StartsWith("Assets") ? path : null;
        }

        public bool IsIgnored(string assetPath)
        {
            foreach (string folder in ignoredFolders)
            {
                string normalized = NormalizeFolder(folder);
                if (normalized != null && (assetPath == normalized || assetPath.StartsWith(normalized + "/"))) return true;
            }
            return false;
        }
    }
}
