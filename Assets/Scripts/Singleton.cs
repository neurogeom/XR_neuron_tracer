using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Microsoft.MixedReality.GraphicsTools.MeshInstancer;

public class Singleton<T> : MonoBehaviour where T : Component
{
    public static T Instance { get; private set; }

    public virtual void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this as T;
        DontDestroyOnLoad(this.gameObject);
    }
}
