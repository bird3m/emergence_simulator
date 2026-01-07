using UnityEngine;

public class resource : MonoBehaviour
{
    public float nutrition = 40f;
    public bool isOrganism;
    public int organismNutrition;

    private void OnEnable()
    {
        if(isOrganism)
        {
            // Get organism nutrition from stats_for_simulation
            if (stats_for_simulation.Instance != null)
            {
                nutrition = stats_for_simulation.Instance.organismNutrition;
            }
            else
            {
                nutrition = organismNutrition; // Fallback to field value
            }
        }

        if (SourceManager.I != null)
            SourceManager.I.Register(this);
    }

    private void OnDisable()
    {
        if (SourceManager.I != null)
            SourceManager.I.Unregister(this);
    }

    private void OnDestroy()
    {
        if (SourceManager.I != null)
            SourceManager.I.Unregister(this);
    }
}
