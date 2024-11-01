using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;


public class IsolateMenu : SubMenu
{
    public PipeCasing pipeCasing;
    PressableButton[] buttons;
    public GazeController.EyeInteractionType preType;

    // Start is called before the first frame update
    void Start()
    {
        preType = Config.Instance.gazeController.interactionType;
        buttons = GetComponentsInChildren<PressableButton>();
        buttons[0].OnClicked.AddListener(() =>
        {
            Config.Instance.isIsolating = false;
            Config.Instance.gazeController.interactionType = GazeController.EyeInteractionType.Repair;
            Config.Instance.paintingBoard.GetComponent<ObjectManipulator>().AllowedInteractionTypes = InteractionFlags.Near;
            gameObject.SetActive(false);
            pipeCasing.Trace();
        });
        buttons[1].OnClicked.AddListener(() => {
            Config.Instance.isIsolating = false;
            Config.Instance.gazeController.interactionType = GazeController.EyeInteractionType.Repair;
            Config.Instance.paintingBoard.GetComponent<ObjectManipulator>().AllowedInteractionTypes = InteractionFlags.Near;
            pipeCasing.ClearPipes();
            gameObject.SetActive(false);
        }) ;
        buttons[2].OnClicked.AddListener(() => pipeCasing.AddLength());
        buttons[3].OnClicked.AddListener(() => pipeCasing.DecLength());
    }
}
