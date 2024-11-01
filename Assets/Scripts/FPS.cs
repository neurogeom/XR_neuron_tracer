using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPS : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    private float m_time = 0.0f;
    private bool isActive = false;
    private int frames = 0;

    [InspectorButton]
    void ClearTime()
    {
        Debug.Log("cleared");
        m_time = 0;
        isActive = true;
        frames = 0;
    }
    
    void Update()
    {
        if (isActive) 
        {
            m_time += (Time.unscaledDeltaTime);

            frames++;
            if (m_time > 5)
            {
                Debug.Log($"average fps:{frames/m_time})");
                isActive = false;
            }
        }
    }
}