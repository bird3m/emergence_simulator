using System.Collections.Generic;
using UnityEngine;

public class SourceManager : MonoBehaviour
{
    public static SourceManager I;

    // Active sources (resource components)
    public readonly List<resource> sources = new List<resource>();

    private void Awake()
    {
        I = this;
        Time.timeScale = 12f;
    }

    public void Register(resource r)
    {
        if (r == null) return;
        if (!sources.Contains(r)) sources.Add(r);
    }

    public void Unregister(resource r)
    {
        if (r == null) return;
        if (sources.Contains(r)) sources.Remove(r);
    }
}
