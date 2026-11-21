using UnityEngine;
using UnityEngine.SceneManagement;

// Menu chính: chọn map, hoặc vào khu minigame.
public class DemoMenu : MonoBehaviour
{
    public void OnPlay(int level)
    {
        if (level > 3)
            SceneManager.LoadScene("DemoMinigames");
        else
            SceneManager.LoadScene("DemoMap " + level);
    }
}
