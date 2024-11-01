using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class AutoMenu : SubMenu
{
    private int[] SLIDER_MAXIMUM = { 127, 40 , 255};
    Config config;
    public Material withThreshold;
    public Material origin;
    Slider[] sliders;
    Task taskPruning;
    CancellationTokenSource source;
    CancellationToken token;
    PressableButton[] buttons;
    // Start is called before the first frame update
    void Start()
    {
        config = GameObject.FindGameObjectWithTag("Config").GetComponent<Config>();
        sliders = GetComponentsInChildren<Slider>();
        buttons = GetComponentsInChildren<PressableButton>();
        
        
        sliders[0].OnValueUpdated.AddListener((SliderEventData data) => UpdateBkgValue(data));
        sliders[1].OnValueUpdated.AddListener((SliderEventData data) => UpdateRadiusValue(data));
        buttons[0].OnClicked.AddListener(() =>AdjustSlider(0,true, 3));
        buttons[1].OnClicked.AddListener(() =>AdjustSlider(0,false, 3));
        buttons[2].OnClicked.AddListener(() =>AdjustSlider(1,true));
        buttons[3].OnClicked.AddListener(() =>AdjustSlider(1,false));
        buttons[4].OnClicked.AddListener(OnStartClicked);
        buttons[5].OnClicked.AddListener(OnCancelClicked);
        buttons[6].OnClicked.AddListener(OnModifyClicked);

        config.VRShaderType = Config.ShaderType.FlexibleThreshold;

        source = new CancellationTokenSource();
        token = source.Token;

        sliders[0].Value = config.BkgThresh / (float)SLIDER_MAXIMUM[0];
        sliders[1].Value = config.somaRadius / (float)SLIDER_MAXIMUM[1];
    }

    private void OnModifyClicked()
    {
        var iconSelector = buttons[6].GetComponentInChildren<FontIconSelector>();
        if (buttons[6].IsToggled)
        {
            config.gazeController.interactionType = GazeController.EyeInteractionType.EditThresh;
            config.paintingBoard.GetComponent<ObjectManipulator>().enabled = true;
            iconSelector.CurrentIconName = "Icon 9";
        }
        else
        {
            config.gazeController.interactionType = GazeController.EyeInteractionType.None;
            config.paintingBoard.GetComponent<ObjectManipulator>().enabled = false;
            iconSelector.CurrentIconName = "Icon 10";
        }
    }

    private void UpdateOffsetValue(SliderEventData data)
    {
        if (data.OldValue == data.NewValue) { return; }
        int newThresh = Mathf.RoundToInt(data.NewValue * SLIDER_MAXIMUM[2]);
        int fixedThresh = Mathf.RoundToInt(sliders[0].Value * SLIDER_MAXIMUM[0]);
        config.thresholdOffset = newThresh - fixedThresh;
        Debug.Log(newThresh); 
        TextMeshProUGUI[] textMeshProUGUIs = GetComponentsInChildren<TextMeshProUGUI>();
        var Text = textMeshProUGUIs[6];
        Text.text = $"Customized\n Thresh:\n {newThresh}";
    }

    void UpdateBkgValue(SliderEventData data)
    {
        if(data.OldValue==data.NewValue) { return; }
        int value = Mathf.RoundToInt(data.NewValue * SLIDER_MAXIMUM[0]);
        value = Mathf.Clamp(value, 20, SLIDER_MAXIMUM[0]);
        TextMeshProUGUI[] textMeshProUGUIs = GetComponentsInChildren<TextMeshProUGUI>();
        var bkgText = textMeshProUGUIs[0];
        bkgText.text = $"Background\n Threshold:\n {value}";
        config.BkgThresh = value;

        var threshold = config.tracer.InitThreshold();
        var connection = config.tracer.ConnectedPart(false);

        config.VRShaderType = Config.ShaderType.FlexibleThreshold;

        var renderSetting = Config.Instance.volumeRendering;
        renderSetting.connection.overrideState = true;
        renderSetting.connection.value = connection;
        renderSetting.threshold.overrideState = true;
        renderSetting.threshold.value = threshold;

        config.tracer.TraceTrunk();
    }

    async void UpdateRadiusValue(SliderEventData data)
    {
        if (Mathf.Abs(data.OldValue-data.NewValue)* SLIDER_MAXIMUM[1] < 0.1) { return; }
        source.Cancel();
        int value = Mathf.RoundToInt(data.NewValue * SLIDER_MAXIMUM[1]);
        value = Mathf.Clamp(value, 8, SLIDER_MAXIMUM[1]);
        TextMeshProUGUI[] textMeshProUGUIs = GetComponentsInChildren<TextMeshProUGUI>();
        var radiusText = textMeshProUGUIs[3];
        radiusText.text = $"Soma Radius:\n {value}";
        config.somaRadius = value ;
        // config.somaRadius = value;

        source = new CancellationTokenSource();
        token = source.Token;
        float time = Time.realtimeSinceStartup;
        await Task.Run(() =>
        {
            if (token.IsCancellationRequested) { return; }
            config.tracer.Pruning(token);
        }, token);
        Debug.Log("Update Radius Time:"+(Time.realtimeSinceStartup-time));
        config.tracer.CreateTree();
    }

    void OnStartClicked()
    {
        mainMenu.OnAutoFinished();
        Hide();
    }

    void OnCancelClicked()
    {
        Config.Instance.tracer.ClearResult();
        Hide();
    }

    void AdjustSlider(int index, bool up, int factor = 1)
    {
        sliders[index].Value += factor * (up ? 1.0f : -1.0f) / SLIDER_MAXIMUM[index];
    }

    // Update is called once per frame

    public override void Hide()
    {
        Config.Instance.VRShaderType = Config.ShaderType.Base;
        Config.Instance.gazeController.interactionType = GazeController.EyeInteractionType.None;
        base.Hide();
    }

    public override void Show()
    {
        Config.Instance.gazeController.interactionType = GazeController.EyeInteractionType.None;
        Config.Instance.VRShaderType = Config.ShaderType.FlexibleThreshold;
        Config.Instance.invoker.Clear();
        base.Show();
    }
}
