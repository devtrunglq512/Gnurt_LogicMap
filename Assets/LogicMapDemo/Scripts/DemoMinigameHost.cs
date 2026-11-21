using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene chứa các minigame dạng prefab, chơi lần lượt rồi tính thưởng.
public class DemoMinigameHost : MonoBehaviour
{
    public List<GameObject> minigames = new List<GameObject>();

    public void OnFinish(bool won)
    {
        if (won)
            SceneManager.LoadScene(DemoScenes.Reward);
        else
            SceneManager.LoadScene(DemoScenes.Menu);
    }
}
