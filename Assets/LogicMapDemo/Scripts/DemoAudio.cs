using UnityEngine;

// Nhạc nền giữ nguyên khi đổi scene.
public class DemoAudio : MonoBehaviour
{
    void Awake() => DontDestroyOnLoad(gameObject);
}
