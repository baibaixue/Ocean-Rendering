using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

class LightData
{
    public Quaternion Rotation;
    public Color32 lightColor;

    public LightData()
    {
        Rotation = new Quaternion();
        lightColor = new Color32();
    }

    public LightData(float x,float y,float z,float r,float g,float b)
    {
        Rotation = Quaternion.Euler(x, y, z);
        lightColor = new Color(r, g, b);
    }
}
public class UIController : MonoBehaviour
{
    Button menuButton;
    GameObject menuPanel;
    Button closeButton;

    FPSDisplay MainCameraFPS;
    [SerializeField]
    Material OceanMaterial;
    [SerializeField]
    Light light;
    [SerializeField]
    OceanMesh oceanMesh;
    // UI
    [SerializeField]
    Toggle ShowFrame;
    [SerializeField]
    Button ChangeSkyBox;
    [SerializeField]
    Button ChangeWaveShape;
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

    // UIData
    int SkyBoxIndex = 0;
    int WaveDataIndex = 0;
    List<List<WindData>> WaveData;
    List<LightData> SkyBoxLightData;
    int FFTPowData;
    float LambdaData;
    float WaveHeightData;
    float LodScaleData;
    float SSSStrengthData;
    float FresnelData;
    float FoamScaleData;

    //Camera RenderCamera;

    // Start is called before the first frame update
    [System.Obsolete]
    void Start()
    {
        MainCameraFPS = Camera.main.gameObject.GetComponent<FPSDisplay>();
        menuButton = gameObject.transform.Find("MenuBtn").GetComponent<Button>();
        menuPanel = gameObject.transform.Find("MenuPanel").gameObject;
        closeButton = gameObject.transform.Find("CloseBtn").GetComponent<Button>();

        ShowFrame = ShowFrame == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/ShowFrame").GetComponent<Toggle>() : ShowFrame;
        ChangeSkyBox = ChangeSkyBox == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/ChangeSkyBox").GetComponent<Button>() : ChangeSkyBox;
        ChangeWaveShape = ChangeWaveShape == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/ChangeWaveShape").GetComponent<Button>() : ChangeWaveShape;
        FFTPow = FFTPow == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/FFTPow").gameObject : FFTPow;
        Lambda = Lambda == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/Lambda").gameObject : Lambda;
        WaveHeight = WaveHeight == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/WaveHeight").gameObject : WaveHeight;
        LodScale = LodScale == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/LodScale").gameObject : LodScale;
        SSSStrength = SSSStrength == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/SSSStrength").gameObject : SSSStrength;
        Fresnel = Fresnel == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/Fresnel").gameObject : Fresnel;
        FoamScale = FoamScale == null ? gameObject.transform.Find("MenuPanel/Scroll View/Viewport/Content/FoamScale").gameObject : FoamScale;

        
        oceanMesh = oceanMesh == null ? new OceanMesh() : oceanMesh;

        menuButton.gameObject.SetActive(true);
        menuPanel.gameObject.SetActive(false);

        menuButton.onClick.AddListener(OnClickMenuBtn);

        closeButton.onClick.AddListener(OnCloseBtnClick);
        InitUIData();

        InitUIComponent();
    }

    void OnClickMenuBtn()
    {
        bool isActive = menuPanel.activeSelf;
        menuPanel.gameObject.SetActive(!isActive);
    }

    void InitUIData()
    {
        SkyBoxIndex = 0;
        WaveDataIndex = 0;

        InitWaveData();
        InitSkyBoxData();

        FFTPowData = oceanMesh.FFTPow;
        LambdaData = oceanMesh.lambda;
        WaveHeightData = oceanMesh.waveA;
        LodScaleData = OceanMaterial.GetFloat("_LOD_scale");
        SSSStrengthData = OceanMaterial.GetFloat("_SSSStrength");
        FresnelData = OceanMaterial.GetFloat("_FresnelScale");
        FoamScaleData = OceanMaterial.GetFloat("_FoamScale");
    }

    void InitSkyBoxData()
    {
        SkyBoxLightData = new List<LightData>();
        SkyBoxIndex = 0;
        SkyBoxLightData.Add(new LightData(15, 305, 0, 1, 0.825f, 0.683f));
        SkyBoxLightData.Add(new LightData(90, 180, 0, 1, 0.825f, 0.683f));
        SkyBoxLightData.Add(new LightData(25, 100, 0, 0.877f, 0.571f, 0.326f));
        SkyBoxLightData.Add(new LightData(25, 196, 0, 1, 0.825f, 0.683f));
        SkyBoxLightData.Add(new LightData(30, 70, 0, 1, 0.825f, 0.683f));
        SkyBoxLightData.Add(new LightData(12, 155, 0, 1, 0.658f, 0.258f));
        SkyBoxLightData.Add(new LightData(15, 193, 0, 1, 0.768f, 0.522f));
        SkyBoxLightData.Add(new LightData(85, 100, 0, 1, 0.825f, 0.683f));
        SkyBoxLightData.Add(new LightData(10, 166, 0, 1, 0.825f, 0.683f));
        Material skyBox = Resources.Load<Material>("SkyBox/sky-" + SkyBoxIndex.ToString());
        Cubemap cubemap = Resources.Load<Cubemap>("SkyBox/sky-" + SkyBoxIndex.ToString());
        if (skyBox == null || cubemap == null)
            return;
        RenderSettings.skybox = skyBox;
        OceanMaterial.SetTexture("_Cubemap", cubemap);
        light.transform.localRotation = SkyBoxLightData[SkyBoxIndex].Rotation;
        light.color = SkyBoxLightData[SkyBoxIndex].lightColor;
    }
    void InitWaveData()
    {
        WaveData = new List<List<WindData>>();
        WindData wind1 = new WindData(1, 1, 60);
        WindData wind2 = new WindData(-1, 1, 60);
        WindData wind3 = new WindData(-1, -1, 60);
        WindData wind4 = new WindData(1, -1, 60);
        List<WindData>  Wave1 = new List<WindData>();
        Wave1.Add(wind1); Wave1.Add(wind3);

        List<WindData> Wave2 = new List<WindData>();
        Wave2.Add(wind1); Wave2.Add(wind2);

        List<WindData> Wave3 = new List<WindData>();
        Wave3.Add(wind1); Wave3.Add(wind2); Wave3.Add(wind3);

        WaveData.Add(Wave1); WaveData.Add(Wave2); WaveData.Add(Wave3);
    }

    [System.Obsolete]
    void InitUIComponent()
    {
        ChangeSkyBox.onClick.AddListener(OnClickChangeSkyBox);
        ChangeWaveShape.onClick.AddListener(OnClickChangeWaveShape);

        ShowFrame.isOn = MainCameraFPS.enabled;
        ShowFrame.onValueChanged.AddListener((bool value) => OnShowFrameToggleClick(value));

        InitSlider(FFTPow, FFTPowData, 4, 8);
        InitSlider(Lambda, LambdaData, 0.0f, 2.0f);
        InitSlider(WaveHeight, WaveHeightData, 0.0f, 1.0f);
        InitSlider(LodScale, LodScaleData, 0.0f, 10.0f);
        InitSlider(SSSStrength, SSSStrengthData, 0.0f, 1.0f);
        InitSlider(Fresnel, FresnelData, 0.0f, 1.0f);
        InitSlider(FoamScale, FoamScaleData, 0.0f, 10.0f);
    }

    [System.Obsolete]
    void InitSlider(GameObject go, float _value, float min, float max)
    {
        Slider SliderGo = go.transform.Find("Slider").GetComponent<Slider>();
        Text DataText = go.transform.Find("Data").GetComponent<Text>();

        DataText.text = _value.ToString();

        SliderGo.value = _value;
        SliderGo.maxValue = max;
        SliderGo.minValue = min;
        SliderGo.wholeNumbers = false;

        switch (go.name)
        {
            case "FFTPow":
                SliderGo.value = _value;
                SliderGo.wholeNumbers = true;
                SliderGo.onValueChanged.AddListener((float value) => OnFFTPowChange((int)value, DataText));
                break;
            case "Lambda":
                SliderGo.value = _value - 1.0f;
                SliderGo.onValueChanged.AddListener((float value) => OnLambdaChange(value, DataText));
                break;
            case "WaveHeight":
                SliderGo.onValueChanged.AddListener((float value) => OnWaveHeightChange(value, DataText));
                break;
            case "LodScale":
                SliderGo.onValueChanged.AddListener((float value) => OnShaderDataChange(value, DataText, "_LOD_scale"));
                break;
            case "SSSStrength":
                SliderGo.onValueChanged.AddListener((float value) => OnShaderDataChange(value, DataText, "_SSSStrength"));
                break;
            case "Fresnel":
                SliderGo.onValueChanged.AddListener((float value) => OnShaderDataChange(value, DataText, "_FresnelScale"));
                break;
            case "FoamScale":
                SliderGo.onValueChanged.AddListener((float value) => OnShaderDataChange(value, DataText, "_FoamScale"));
                break;
        }
    }
    void OnFFTPowChange(int value,Text text)
    {
        oceanMesh.FFTPow = value;
        text.text = value.ToString();
        oceanMesh.InitOceanData();
    }

    void OnLambdaChange(float value, Text text)
    {
        oceanMesh.lambda = value - 1.0f;
        text.text = (value - 1.0f).ToString();
        oceanMesh.InitOceanData();
    }

    void OnWaveHeightChange(float value, Text text)
    {
        oceanMesh.waveA= value;
        text.text = value.ToString();
        oceanMesh.InitOceanData();
    }
    void OnShaderDataChange(float value ,Text text, string name)
    {
        OceanMaterial.SetFloat(name, value);
        text.text = value.ToString();
    }
    void OnClickChangeSkyBox()
    {
        SkyBoxIndex = (SkyBoxIndex + 1) % SkyBoxLightData.Count;

        Material skyBox = Resources.Load<Material>("SkyBox/sky-" + SkyBoxIndex.ToString());
        Cubemap cubemap = Resources.Load<Cubemap>("SkyBox/sky-" + SkyBoxIndex.ToString());
        if (skyBox == null || cubemap == null)
            return;
        RenderSettings.skybox = skyBox;
        OceanMaterial.SetTexture("_Cubemap", cubemap);
        light.transform.localRotation = SkyBoxLightData[SkyBoxIndex].Rotation;
        light.color = SkyBoxLightData[SkyBoxIndex].lightColor;
    }

    void OnClickChangeWaveShape()
    {
        WaveDataIndex = (WaveDataIndex + 1) % WaveData.Count;
        oceanMesh.windData = WaveData[WaveDataIndex];
        oceanMesh.InitOceanData();
    }
    void OnShowFrameToggleClick(bool value)
    {
        MainCameraFPS.enabled = value;
    }
    void OnCloseBtnClick()
    {
#if UNITY_EDITOR    //在编辑器模式下
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    // Update is called once per frame
    void Update()
    {
        
    }


}
