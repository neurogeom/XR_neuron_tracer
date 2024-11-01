using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubMenu : MonoBehaviour
{
    public MainMenu mainMenu;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public virtual void Hide()
    {
        this.gameObject.SetActive(false);
        mainMenu.subMenu = null;
    }

    public virtual void Show()
    {
        this.gameObject.SetActive(true);
        mainMenu.subMenu = this;
    }
}
