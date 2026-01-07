using UnityEngine;
using UnityEngine.UI;

public class BarsUI : MonoBehaviour
{
    public Slider healthSlider;

    public Traits traits;

    void Start()
    {
        if (traits != null)
        {
            healthSlider.maxValue = traits.maxEnergy;
        }
    }

    private void Update()
    {
        if (traits == null) 
            return;
        healthSlider.value = traits.currentEnergy;

    }
}
