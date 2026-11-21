using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tạo game demo để thử LogicMap trong project dev (không nằm trong package):
/// Loading → Menu → Map 1..3 / khu minigame (3 prefab) → Reward, cùng Map 4 chưa vào build.
/// Chạy lại: Unity -batchmode -quit -projectPath . -executeMethod LogicMapDemoBuilder.Build
/// </summary>
public static class LogicMapDemoBuilder
{
    const string Root = "Assets/LogicMapDemo";

    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Scenes");
        Directory.CreateDirectory(Root + "/MiniGames");
        AssetDatabase.Refresh();

        var minigames = new List<GameObject>();
        foreach (string name in new[] { "Cut", "Jump", "Swim" })
        {
            var go = new GameObject(name);
            go.AddComponent<DemoMinigame>();
            minigames.Add(PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/MiniGames/{name}.prefab"));
            Object.DestroyImmediate(go);
        }

        var build = new List<EditorBuildSettingsScene>();
        CreateScene(build, "DemoLoading", true, null, typeof(DemoBoot), typeof(DemoAudio), typeof(DemoGameManager));
        CreateScene(build, "DemoMenu", true, null, typeof(DemoMenu));
        for (int i = 1; i <= 3; i++) CreateScene(build, "DemoMap " + i, true, null, typeof(DemoGameUI));
        CreateScene(build, "DemoMinigames", true, go => go.GetComponent<DemoMinigameHost>().minigames = minigames, typeof(DemoMinigameHost));
        CreateScene(build, "DemoReward", true, null, typeof(DemoGameUI));
        CreateScene(build, "DemoMap 4", false, null, typeof(DemoGameUI));

        EditorBuildSettings.scenes = build.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("LogicMap demo: đã tạo " + build.Count + " scene");
    }

    // Scene trống có một GameObject gắn các component; setup chỉnh thêm trước khi lưu.
    static void CreateScene(List<EditorBuildSettingsScene> build, string name, bool inBuild, System.Action<GameObject> setup, params System.Type[] components)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject(name);
        foreach (var type in components) go.AddComponent(type);
        setup?.Invoke(go);

        string path = $"{Root}/Scenes/{name}.unity";
        EditorSceneManager.SaveScene(scene, path);
        build.Add(new EditorBuildSettingsScene(path, inBuild));
    }
}
