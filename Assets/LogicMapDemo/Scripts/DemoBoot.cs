using UnityEngine;
using UnityEngine.SceneManagement;

// Scene đầu tiên: nạp xong thì vào menu.
public class DemoBoot : MonoBehaviour
{
    void Start() => SceneManager.LoadScene("DemoMenu");
}
