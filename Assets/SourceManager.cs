//This script holds a list of resoures to access them efficiently in code rather than inefficient, built in unity functions

using System.Collections.Generic;
using UnityEngine;


public class SourceManager : MonoBehaviour
{
    public static SourceManager I;
    public readonly List<resource> sources = new List<resource>();

    private void Awake()
    {
        I = this;
        Time.timeScale = 12f;
    }

    public void Register(resource r)
    {
        if (r == null) 
            return;
        if (!sources.Contains(r)) 
            sources.Add(r);
    }

    public void Unregister(resource r)
    {
        if (r == null) 
            return;
        if (sources.Contains(r)) 
            sources.Remove(r);
    }
}
