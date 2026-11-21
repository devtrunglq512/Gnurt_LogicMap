using UnityEngine;
using UnityEngine.SceneManagement;

// Manager sống xuyên scene; tên scene truyền vào lúc chạy nên LogicMap không đoán được.
public class DemoGameManager : MonoBehaviour
{
    void Awake() => DontDestroyOnLoad(gameObject);

    public void LoadLevel(string sceneName) => SceneManager.LoadSceneAsync(sceneName);
}
