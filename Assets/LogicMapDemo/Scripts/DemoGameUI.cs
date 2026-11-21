using UnityEngine;
using UnityEngine.SceneManagement;

// Giao diện trong map: về menu hoặc sang map kế tiếp.
public class DemoGameUI : MonoBehaviour
{
    public void OnHome() => SceneManager.LoadScene(DemoScenes.Menu);

    public void OnNext() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
}
