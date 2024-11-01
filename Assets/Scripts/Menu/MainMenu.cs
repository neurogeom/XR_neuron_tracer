using CommandStructure;
using MixedReality.Toolkit;
using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
public class MainMenu : MonoBehaviour
{
    private enum ButtonFunc
    {
        Load,
        Save,
        Auto,
        Eye,
        Move,
        Blocker,
        Draw,
        Erase,
        Undo,
        Redo,
        Filter
    }

    Config config;
    public PressableButton[] buttons;
    ToggleCollection toggleCollection;

    public SubMenu subMenu;
    public AutoMenu autoMenu;
    public GazeMenu eyeMenu;
    // Start is called before the first frame update
    void Start()
    {
        config = GameObject.FindGameObjectWithTag("Config").GetComponent<Config>();
        buttons = gameObject.GetComponentsInChildren<PressableButton>();
        buttons[(int)ButtonFunc.Save].OnClicked.AddListener(() => SaveEvent());
        buttons[(int)ButtonFunc.Auto].OnClicked.AddListener(() => AutoEvent());
        buttons[(int)ButtonFunc.Eye].OnClicked.AddListener(() => EyeEvent());
        buttons[(int)ButtonFunc.Move].OnClicked.AddListener(() => MoveEvent());
        Debug.Log(buttons[(int)ButtonFunc.Blocker]);
        buttons[(int)ButtonFunc.Blocker].OnClicked.AddListener(BlockerEvent);
        buttons[(int)ButtonFunc.Draw].OnClicked.AddListener(() => DrawEvent());
        buttons[(int)ButtonFunc.Erase].OnClicked.AddListener(() => EraseEvent());
        buttons[(int)ButtonFunc.Undo].OnClicked.AddListener(() => UndoEvent());
        buttons[(int)ButtonFunc.Redo].OnClicked.AddListener(() => RedoEvent());
        buttons[(int)ButtonFunc.Filter].OnClicked.AddListener(() => FilterEvent());
    

        var toggleCollection = gameObject.AddComponent<ToggleCollection>();
        toggleCollection.AllowSwitchOff = true;
        List<StatefulInteractable> toggles = new();
        toggles.Add(buttons[(int)ButtonFunc.Eye]);
        toggles.Add(buttons[(int)ButtonFunc.Blocker]);
        toggles.Add(buttons[(int)ButtonFunc.Draw]);
        toggles.Add(buttons[(int)ButtonFunc.Erase]);
        // toggles.Add(buttons[(int)ButtonFunc.Filter]);
        toggleCollection.Toggles = toggles;


        buttons[(int)ButtonFunc.Save].enabled = false;
        buttons[(int)ButtonFunc.Eye].enabled = false;
        buttons[(int)ButtonFunc.Blocker].enabled = false;
        buttons[(int)ButtonFunc.Draw].enabled = false;
        buttons[(int)ButtonFunc.Erase].enabled = false;
        buttons[(int)ButtonFunc.Undo].enabled = false;
        buttons[(int)ButtonFunc.Redo].enabled = false;

        //gameObject.transform.GetChild(0).gameObject.SetActive(false);
    }

    void SaveEvent()
    {
        Config.Instance.tracer.Save();
    }

    void AutoEvent()
    {
        if(subMenu !=null && subMenu is not AutoMenu)
        {
            HideSubMenu();
        }

        if(subMenu == null)
        {
            if (autoMenu == null)
            {
                var GO = Instantiate(Resources.Load("prefabs/AutoMenu")) as GameObject;
                autoMenu = GO.GetComponent<AutoMenu>();
                autoMenu.mainMenu = this;
                subMenu = autoMenu;
            }
            autoMenu.Show();
        }
    }

    public void OnAutoFinished()
    {
        config.invoker.Execute(new AutoCommand(config.tracer, config.BkgThresh, config.somaRadius, config._rootPos));
        //config.tracer.Trace(3);
        //config.tracer.Save(0);
        buttons[(int)ButtonFunc.Save].enabled = true;
        buttons[(int)ButtonFunc.Eye].enabled = true;
        buttons[(int)ButtonFunc.Blocker].enabled = true;
        buttons[(int)ButtonFunc.Draw].enabled = true;
        buttons[(int)ButtonFunc.Erase].enabled = true;
        buttons[(int)ButtonFunc.Undo].enabled = true;
        buttons[(int)ButtonFunc.Redo].enabled = true;
    }

    void EyeEvent()
    {
        if (buttons[(int)ButtonFunc.Eye].IsToggled)
        {
            LockMove(false);
            if(subMenu != null && subMenu is not GazeMenu)
            {
                HideSubMenu();
            }

            if (eyeMenu == null)
            {
                var obj = Instantiate(Resources.Load("prefabs/EyeMenu")) as GameObject;
                eyeMenu = obj.GetComponent<GazeMenu>();
                eyeMenu.mainMenu = this;
                subMenu = eyeMenu;
            }
            else
            {
                eyeMenu.Show();
            }
            config.GetComponent<GestureController>().operation = GestureController.OperationType.None;
            config.gazeController.interactionType = GazeController.EyeInteractionType.Repair;
        }
        else
        {  
            if(subMenu is GazeMenu)
            {
                HideSubMenu();
                config.gazeController.interactionType = GazeController.EyeInteractionType.None;
            }
        }
    }
    void DrawEvent()
    {
        if (buttons[(int)ButtonFunc.Draw].IsToggled)
        {
            LockMove(true);
            config.gazeController.interactionType = GazeController.EyeInteractionType.None;
            config.gestureController.operation = GestureController.OperationType.Draw;
            config.paintingBoard.GetComponent<ObjectManipulator>().enabled = false;
            HideSubMenu();
        }
        else
        {
            config.gestureController.operation = GestureController.OperationType.None;
        }
    }

    void EraseEvent()
    {
        if (buttons[(int)ButtonFunc.Erase].IsToggled)
        {
            LockMove(true);
            config.gazeController.interactionType = GazeController.EyeInteractionType.None;
            config.gestureController.operation = GestureController.OperationType.Erase;
            HideSubMenu();
        }
        else
        {
            config.gestureController.operation = GestureController.OperationType.None;
        }
    }

    void BlockerEvent()
    {
        if (buttons[(int)ButtonFunc.Blocker].IsToggled)
        {
            Config.Instance.isIsolating = false;
        }
        else
        {
            Config.Instance.isIsolating = true;
        }
    }

    void MoveEvent()
    {
        Debug.Log("move");
        LockMove(config.paintingBoard.GetComponent<ObjectManipulator>().enabled);
        buttons[(int)ButtonFunc.Draw].ForceSetToggled(false);
        buttons[(int)ButtonFunc.Erase].ForceSetToggled(false);
        HideSubMenu();
    }
    
    void UndoEvent()
    {
        config.invoker.Undo();
    }

    void RedoEvent()
    {
        config.invoker.Redo();
    }

    void FilterEvent()
    {
    }

    void HideSubMenu()
    {
        if (subMenu != null)
        {
            subMenu.Hide();
            subMenu = null;
        }
    }

    void LockMove(bool islock)
    {
        PressableButton button = buttons[(int)ButtonFunc.Move];
        var iconSelector = button.GetComponentInChildren<FontIconSelector>();
        TextMeshProUGUI textMeshPro = button.GetComponentsInChildren<TextMeshProUGUI>()[1];
        if (!islock)
        {
            config.paintingBoard.GetComponent<ObjectManipulator>().enabled=true;
            textMeshPro.text = "lock";
            iconSelector.CurrentIconName = "Icon 17";
        }
        else
        {
            config.paintingBoard.GetComponent<ObjectManipulator>().enabled = false;
            textMeshPro.text = "move";
            iconSelector.CurrentIconName = "Icon 40";
            buttons[(int)ButtonFunc.Move].ForceSetToggled(false);

        }
        config.gestureController.operation = GestureController.OperationType.None;

    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
