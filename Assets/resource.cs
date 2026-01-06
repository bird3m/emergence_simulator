using UnityEngine;

public class resource : MonoBehaviour
{
    public float nutrition = 40f;

    private void OnEnable()
    {
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
