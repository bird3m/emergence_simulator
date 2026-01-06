using UnityEngine;
using UnityEngine.SceneManagement;

public class startSim : MonoBehaviour
{
    public void SimStart()
    {
        SceneManager.LoadScene("SampleScene");
    }
}
