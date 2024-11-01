using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransferSomaInfo : MonoBehaviour
{
    public MeshRenderer renderer;

    public float radius;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        renderer.material.SetFloat("_SomaRadius",radius * transform.lossyScale.x);
        renderer.material.SetVector("_SomaPos",transform.position);
    }
}
