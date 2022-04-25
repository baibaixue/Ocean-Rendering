using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class UIController : MonoBehaviour
{
    Button menuButton;
    GameObject menuPanel;

    FPSDisplay MainCameraFPS;

    // UI
    [SerializeField]
    Toggle ShowFrame;
    [SerializeField]
    Dropdown ChooseSkyBox;
    [SerializeField]
    Dropdown WaveShape;
    [SerializeField]
    GameObject FFTPow;
    [SerializeField]
    GameObject Lambda;
    [SerializeField]
    GameObject WaveHeight;
    [SerializeField]
    GameObject LodScale;
    [SerializeField]
    GameObject SSSStrength;
    [SerializeField]
    GameObject Fresnel;
    [SerializeField]
    GameObject FoamScale;


    // Start is called before the first frame update
    [System.Obsolete]
    void Start()
    {
        MainCameraFPS = Camera.main.gameObject.GetComponent<FPSDisplay>();
        menuButton = gameObject.transform.FindChild("MenuBtn").GetComponent<Button>();
        menuPanel = gameObject.transform.FindChild("MenuPanel").gameObject;

        ShowFrame = ShowFrame == null ? gameObject.transform.FindChild("MenuPanel/Scroll View/Viewport/Content/ShowFrame").GetComponent<Toggle>() : ShowFrame;
        ChooseSkyBox = ChooseSkyBox == null ? gameObject.transform.FindChild("MenuPanel/Scroll View/Viewport/Content/ChooseSkyBox").GetComponent<Dropdown>() : ChooseSkyBox;

        menuButton.gameObject.SetActive(true);
        menuPanel.gameObject.SetActive(false);

        menuButton.onClick.AddListener(OnClickMenuBtn);

        InitUIComponent();
    }

    void OnClickMenuBtn()
    {
        bool isActive = menuPanel.activeSelf;
        menuPanel.gameObject.SetActive(!isActive);
    }

    void InitUIComponent()
    {
        ShowFrame.isOn = MainCameraFPS.enabled;
        ShowFrame.onValueChanged.AddListener((bool value) => OnShowFrameToggleClick(value));

        //InitDropDown(ChooseSkyBox,"SkyBox");
        //InitDropDown(WaveShape,"WaveData");
    }

    void InitDropDown(Dropdown dropDown,string name)
    {
        dropDown.options.Clear();

        for(int i=1;i<=3;i++)
        {
            Dropdown.OptionData op = new Dropdown.OptionData();
            op.text = name + i.ToString();
            dropDown.options.Add(op);
        }
        dropDown.value = 0;
        //dropDown.Show();
        dropDown.onValueChanged.AddListener(OnDropDownChange);
    }

    void OnShowFrameToggleClick(bool value)
    {
        MainCameraFPS.enabled = value;
    }

    void OnDropDownChange(int value)
    {
        Debug.LogError("Change" + value.ToString());
    }
    // Update is called once per frame
    void Update()
    {
        
    }


}
