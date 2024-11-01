using CommandStructure;
using MixedReality.Toolkit.UX;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class GazeMenu : SubMenu
{
    Config config;
    Slider[] sliders;
    [SerializeField] int[] SLIDER_MAXIMUM = { 128 };
    PressableButton[] Buttons;
    // Start is called before the first frame update
    void Start()
    {
        config = GameObject.FindGameObjectWithTag("Config").GetComponent<Config>();
        sliders = GetComponentsInChildren<Slider>();
        Buttons = GetComponentsInChildren<PressableButton>();
        sliders[0].OnValueUpdated.AddListener((SliderEventData data) => UpdateViewValue(data));
        Buttons[0].OnClicked.AddListener(() => AdjustSlider(0,true));
        Buttons[1].OnClicked.AddListener(() => AdjustSlider(0, false));
        Buttons[2].OnClicked.AddListener(() => Leave());
        Buttons[3].OnClicked.AddListener(() => Delete());
        Buttons[4].OnClicked.AddListener(() => Undo());
        Buttons[5].OnClicked.AddListener(() => Redo());
        //Buttons[2].IsToggled = false;
        config.VRShaderType = Config.ShaderType.FixedThreshold;

        sliders[0].Value = config.ViewThresh / (float)SLIDER_MAXIMUM[0];

        config.VRShaderType = Config.ShaderType.FixedThreshold;
        config.volumeRendering.viewThreshold.overrideState = true;
        config.volumeRendering.viewThreshold.value = config.ViewThresh / 255.0f;
        config.volumeRendering.mask.overrideState = true;
        config.volumeRendering.mask.value = config.tracer.fim.mask;
        config.volumeRendering.selection.overrideState = true;
        config.volumeRendering.selection.value = config.tracer.fim.selection;
    }

    private void Redo()
    {
        config.invoker.Redo();
    }

    private void Undo()
    {
        config.invoker.Undo();
    }

    private void Delete()
    {
        Config.Instance.gazeController.interactionType = Buttons[3].IsToggled? GazeController.EyeInteractionType.DeleteNoise:GazeController.EyeInteractionType.Repair;
        if (Buttons[3].IsToggled)
        {
            config.volumeRendering.mask.overrideState = true;
            config.volumeRendering.mask.value = config.tracer.fim.mask;
            config.volumeRendering.selection.overrideState = true;
            config.volumeRendering.selection.value = config.tracer.fim.selection;
        }
        else
        {
            config.volumeRendering.mask.overrideState = true;
            config.volumeRendering.mask.value = config.tracer.fim.mask;
            config.volumeRendering.selection.overrideState = true;
            config.volumeRendering.selection.value = config.tracer.fim.ClearSelection();
        }
        //config.invoker.Execute(new DeleteCommand(config.invoker, config.tracer, config.curIndex));
    }



    void UpdateViewValue(SliderEventData data)
    {
        int value = Mathf.RoundToInt(data.NewValue * SLIDER_MAXIMUM[0]);
        value = Mathf.Clamp(value, 5, 128);

        TextMeshProUGUI[] textMeshProUGUIs = GetComponentsInChildren<TextMeshProUGUI>();
        var viewText = textMeshProUGUIs[0];
        viewText.text = $"View Threshold:\n {value}"; 

        config.ViewThresh = value;

        config.VRShaderType = Config.ShaderType.FixedThreshold;
        config.volumeRendering.viewThreshold.overrideState = true;
        config.volumeRendering.viewThreshold.value = value/255.0f;
        config.volumeRendering.mask.overrideState = true;
        config.volumeRendering.mask.value = config.tracer.fim.mask;
        config.volumeRendering.selection.overrideState = true;
        config.volumeRendering.selection.value = config.tracer.fim.selection;
        
        if(Config.Instance.gazeController.interactionType == GazeController.EyeInteractionType.DeleteNoise)
        {
            Config.Instance.tracer.HighlightNoise();
        }
    }

    void Leave()
    {

        if (Config.Instance.gazeController.interactionType == GazeController.EyeInteractionType.Repair)
        {
            Hide();
        }
        else
        {
            Buttons[3].ForceSetToggled(false);
            config.volumeRendering.mask.overrideState = true;
            config.volumeRendering.mask.value = config.tracer.fim.mask;
            config.volumeRendering.selection.overrideState = true;
            config.volumeRendering.selection.value = config.tracer.fim.ClearSelection();
            config.invoker.Execute(new DeleteCommand(config.tracer, config.selectedIndex));
            config.gazeController.interactionType = GazeController.EyeInteractionType.Repair;
        }
    }

    void AdjustSlider(int index, bool up)
    {
        if (up)
            sliders[index].Value += 1.0f / SLIDER_MAXIMUM[index];
        else
            sliders[index].Value -= 1.0f / SLIDER_MAXIMUM[index];
    }

    public override void Hide()
    {
        config.VRShaderType = Config.ShaderType.Base;
        config.gazeController.interactionType = GazeController.EyeInteractionType.None;
        mainMenu.buttons[3].ForceSetToggled(false);
        base.Hide();
    }

    public override void Show()
    {
        config.VRShaderType = Config.ShaderType.FixedThreshold;
        base.Show();
    }
}
