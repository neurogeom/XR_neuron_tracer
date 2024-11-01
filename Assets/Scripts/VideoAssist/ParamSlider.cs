using System.Collections;
using System.Collections.Generic;
using MixedReality.Toolkit.UX;
using UnityEngine;

public class ParamSlider : MonoBehaviour
{
    private Slider slider;
    private Config config;
    
    // Start is called before the first frame update
    void Start()
    {
        slider = GetComponentsInChildren<Slider>()[0];
        slider.OnValueUpdated.AddListener((SliderEventData data) => UpdateViewValue(data));
        config = Config.Instance;
    }

    private void UpdateViewValue(SliderEventData arg0)
    {
        config.volumeRendering.viewParam.overrideState = true;
        config.volumeRendering.viewParam.value = arg0.NewValue;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
