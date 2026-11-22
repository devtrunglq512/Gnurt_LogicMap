using System;
using System.Collections.Generic;

namespace Gnurt.LogicMap
{
    // Kết quả quét project. Các lớp để public và [Serializable] để lưu bằng JsonUtility vào Library/.

    public enum SectionKind
    {
        Scene,
        Prefab,
    }

    /// <summary>Lệnh chuyển scene trỏ tới đâu.</summary>
    public enum TargetKind
    {
        Name,       // tên scene cụ thể
        BuildIndex, // số thứ tự trong Build Settings
        Pattern,    // ghép chuỗi, vd "Map " + level → Map *
        Next,       // scene kế tiếp trong Build Settings
        Reload,     // tải lại scene đang mở
        Unknown,    // tính lúc chạy, không đoán được
    }

    [Serializable]
    public sealed class ProjectScan
    {
        public const int CurrentVersion = 2; // tăng khi đổi cấu trúc, để bản lưu cũ bị bỏ và quét lại

        public int version = CurrentVersion;
        public string projectName;
        public string scannedAt;
        public List<SectionInfo> sections = new List<SectionInfo>(); // mọi scene + prefab là map/minigame
        public List<AssetInfo> prefabs = new List<AssetInfo>();       // mọi prefab trong Assets, để xem theo thư mục
        public List<ScriptInfo> scripts = new List<ScriptInfo>();
        public List<string> prefabFolders = new List<string>(); // thư mục prefab-mục đã dùng khi quét
    }

    /// <summary>Một mục của game: một scene, hoặc một prefab đóng vai một map/minigame.</summary>
    [Serializable]
    public sealed class SectionInfo
    {
        public string path;
        public string name;
        public SectionKind kind;
        public bool inBuild;
        public int buildIndex = -1; // thứ tự trong các scene đang bật ở Build Settings
        public List<string> scripts = new List<string>(); // script được dùng (kể cả qua prefab lồng bên trong)
        public List<string> prefabs = new List<string>(); // prefab-mục mà mục này chứa hoặc tham chiếu
        public List<string> uses = new List<string>();    // prefab được tham chiếu trực tiếp
    }

    /// <summary>Một prefab bất kỳ: dùng script nào, chứa prefab nào.</summary>
    [Serializable]
    public sealed class AssetInfo
    {
        public string path;
        public string name;
        public List<string> scripts = new List<string>();
        public List<string> uses = new List<string>(); // prefab được tham chiếu trực tiếp
    }

    [Serializable]
    public sealed class ScriptInfo
    {
        public string path;
        public string name;
        public string assembly;
        public bool dontDestroyOnLoad;
        public List<SceneLoadCall> loads = new List<SceneLoadCall>();
    }

    [Serializable]
    public sealed class SceneLoadCall
    {
        public string method;    // LoadScene, LoadSceneAsync...
        public int line;
        public string member;    // hàm chứa lệnh
        public string code;      // dòng code
        public string argument;  // tham số đầu tiên, nguyên văn
        public string condition; // điều kiện dẫn tới lệnh, nếu đoán được
        public TargetKind targetKind;
        public string target;    // tên scene, mẫu (Map *), hoặc biểu thức chưa hiểu
        public int buildIndex = -1;
    }
}
