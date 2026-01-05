using UnityEngine;
using UnityEngine.UI;

public class BarsUI : MonoBehaviour
{
    public Slider healthSlider;
    public Slider energySlider;

    public Traits traits;

    void Start()
    {
        healthSlider.maxValue = 1;
        energySlider.maxValue = 1;
    }

    private void Update()
    {
        if (traits == null) return;

        // update values (safe clamps)
        healthSlider.value = traits.currentHealth / traits.maxHealth;

        energySlider.value = traits.currentEnergy / traits.maxEnergy;
    }
}
