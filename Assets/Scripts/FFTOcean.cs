using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using System;
[Serializable]
public class WindData
{
    public Vector2 windDir;
    public float windSpeed;
}
public class FFTOcean : MonoBehaviour
{
    // 网格生成
    private Mesh mesh;
    private MeshFilter filter;
    private MeshRenderer render;
    public int meshSize = 100;              // 网格大小，每行/列的顶点数
    public int OceanLength = 512;           // 海平面大小，对应Lx与Lz(方形海域Lx=Lz)
    public int FFTPow = 9;                  // 频域坐标采样点数量（2的指数幂）
    private int fftSize;                    // pow(2,FFTPow)，对应N,M (N=M)
    
    private int[] vertIndex;        // 网格三角形索引
    private Vector3[] position;     // 网格顶点位置
    private Vector2[] uv;           // 顶点uv坐标
    private Vector3[] normal;       // 顶点法向量

    // 材质
    public Material oceanMaterial;                  // 海洋材质
    public Material DisplaceMat;                    // 显示偏移纹理材质
    public Material NormalMat;                      // 显示法线纹理材质
    public Material DebugMat;                       // debug材质，查看中间数据

    private Texture2D GaussianNoiseRT;              // 高斯噪声纹理
    private Texture2D ButterflyRT;                  // 蝶形运算纹理
    private RenderTexture WaveData;                 // 记录中间用于计算的波数据
    private RenderTexture DisplaceRT;               // 偏移纹理
    private RenderTexture NormalRT;                 // 法线纹理
    // 频谱数据
    private RenderTexture H0;                       // 初始频谱 h0
    private RenderTexture H0Conj;                   // 初始频谱的共轭复数 h0Conj
    private RenderTexture HeightSpectrumRT;         // 高度频谱
    private RenderTexture DisplacementSpectrumRT;   // 偏移频谱
    private RenderTexture GradientSpectrumRT;       // 梯度频谱
    private RenderTexture TempRT;                   // 临时存储中间过程
    // 海洋参数
    public ComputeShader OceanCs;                   // 计算海洋的compute shader
    public ComputeShader InitSpectrumCs;            // 计算初始频谱的compute shader
    public ComputeShader ComputeFFTCs;              // 进行FFT运算的compute shader
    public ComputeShader ComputeWithTimeCs;         // 计算随时间变换的频谱和纹理的 compute shader


    public List<WindData> windData;    
    //public Vector2 windDir;                         // 风向
    //public float windSpeed;                         // 风速
    public float waveA;                             // 菲利普参数，影响波浪高度
    public float depth;                             // 水深
    public float timeScale;                         // 时间系数

    private float time = 0;                         // 时间

    // KernelID
    private int kernelInitH0;                           // 重置H0
    private int kernelInitPhillipsSpectrum;             // 生成初始频谱
    private int kernelCreateSpectrumWithTime;           // 生成每帧的波形频谱
    private int kernelCreateRenderTextureWithTime;      // 生成每帧的纹理
    private int kernelComputeFFTH;                      // 横向FFT运算
    private int kernelComputeFFTV;                      // 纵向FFT运算
    private void Awake()
    {
        mesh = new Mesh();
        filter = gameObject.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = gameObject.AddComponent<MeshFilter>();
        }
        render = gameObject.GetComponent<MeshRenderer>();
        if (render == null)
        {
            render = gameObject.AddComponent<MeshRenderer>();
        }
        filter.mesh = mesh;
        render.material = oceanMaterial;
    }

    void Start()
    {
        fftSize = (int)Mathf.Pow(2, FFTPow);
        CreateMesh();
        InitialComputeShaderValue();
    }

    void Update()
    {
        time += Time.deltaTime * timeScale;
        CreateComputeShaderValue();
    }
    /// <summary>
    /// 初始化computer Shader相关数据值
    /// </summary>
    private void InitialComputeShaderValue()
    {
        if (H0 != null && H0.IsCreated())
        {
            WaveData.Release();
            H0.Release();
            H0Conj.Release();
            HeightSpectrumRT.Release();
            DisplacementSpectrumRT.Release();
            GradientSpectrumRT.Release();
            TempRT.Release();
        }
        WaveData = CreateRenderTexture(fftSize);
        H0 = CreateRenderTexture(fftSize);
        H0Conj = CreateRenderTexture(fftSize);
        HeightSpectrumRT = CreateRenderTexture(fftSize);
        DisplacementSpectrumRT = CreateRenderTexture(fftSize);
        GradientSpectrumRT = CreateRenderTexture(fftSize);
        TempRT = CreateRenderTexture(fftSize);
        NormalRT = CreateRenderTexture(fftSize);
        DisplaceRT = CreateRenderTexture(fftSize);
        // kernelID
        kernelInitH0 = InitSpectrumCs.FindKernel("InitH0");
        kernelInitPhillipsSpectrum = InitSpectrumCs.FindKernel("InitPhillipsSpectrum");
        kernelComputeFFTH = ComputeFFTCs.FindKernel("ComputeFFTH");
        kernelComputeFFTV = ComputeFFTCs.FindKernel("ComputeFFTV");
        kernelCreateSpectrumWithTime = ComputeWithTimeCs.FindKernel("CreateSpectrumWithTime");
        kernelCreateRenderTextureWithTime = ComputeWithTimeCs.FindKernel("CreateRenderTextureWithTime");

        // 得到高斯噪声纹理和蝶形运算纹理
        GetNoiseAndButterFlyTexture();

    }
    /// <summary>
    /// 计算每帧的频谱和纹理
    /// </summary>
    private void CreateComputeShaderValue()
    {
        // 得到频谱
        GetInitSpectrum();
        GetSpectrum();
        // FFT
        FastFourierTransform();
        // 生成并设置纹理
        SetMaterialTexture();
        SetDebugTexture();
    }
    /// <summary>
    /// 创建海面网格
    /// </summary>
    private void CreateMesh()
    {
        vertIndex = new int[(meshSize - 1) * (meshSize - 1) * 6];
        position = new Vector3[meshSize * meshSize];
        uv = new Vector2[meshSize * meshSize];
        normal = new Vector3[meshSize * meshSize];
        if (meshSize == 1)
        {
            position[0] = new Vector3(0, 0, 0);
            uv[0] = new Vector2(0, 0);
            normal[0] = new Vector3(0, 1, 0);
        }
        else
        {
            int count = 0;
            for (int i = 0; i < meshSize; i++)
            {
                for (int j = 0; j < meshSize; j++)
                {
                    int index = i * meshSize + j;
                    position[index] = new Vector3((j - meshSize / 2.0f) * OceanLength / meshSize, 0, (i - meshSize / 2.0f) * OceanLength / meshSize);
                    uv[index] = new Vector2(j / (meshSize - 1.0f), i / (meshSize - 1.0f));
                    normal[index] = new Vector3(0, 1, 0);
                    if (i != meshSize - 1 && j != meshSize - 1)
                    {
                        vertIndex[count++] = index;
                        vertIndex[count++] = index + meshSize;
                        vertIndex[count++] = index + meshSize + 1;

                        vertIndex[count++] = index;
                        vertIndex[count++] = index + meshSize + 1;
                        vertIndex[count++] = index + 1;
                    }
                }
            }
        }
        mesh.vertices = position;
        mesh.SetIndices(vertIndex, MeshTopology.Triangles, 0);
        mesh.uv = uv;
        mesh.normals = normal;
    }
    /// <summary>
    /// 得到高斯噪声纹理和蝶形运算纹理
    /// </summary>
    private void GetNoiseAndButterFlyTexture()
    {
        string Gaussianfilename = "GaussianNoiseTexture" + fftSize.ToString() + "x" + fftSize.ToString();
        string Butterflyfilename = "ButterflyTexture" + fftSize.ToString() + "x" + fftSize.ToString();

        Texture2D GaussianNoise = Resources.Load<Texture2D>("Textures/" + Gaussianfilename);
        Texture2D Butterfly = Resources.Load<Texture2D>("Textures/" + Butterflyfilename);

        GaussianNoiseRT = GaussianNoise ? GaussianNoise : CreateGaussianNoiseRT();
        ButterflyRT = Butterfly ? Butterfly : CreateButterflyRT();
        InitSpectrumCs.SetTexture(kernelInitPhillipsSpectrum, "Noise", GaussianNoiseRT);
        ComputeFFTCs.SetTexture(kernelComputeFFTV, "ButterflyRT", ButterflyRT);
        ComputeFFTCs.SetTexture(kernelComputeFFTH, "ButterflyRT", ButterflyRT);
    }
    /// <summary>
    /// 创建高斯噪声纹理
    /// </summary>
    private Texture2D CreateGaussianNoiseRT()
    {
        Texture2D GaussianNoise = new Texture2D(fftSize, fftSize, TextureFormat.RGBAFloat, false, true);
        for (int i = 0;i < fftSize; i++)
        {
            for(int j = 0; j < fftSize; j++)
            {
                GaussianNoise.SetPixel(i, j, new Vector4(GaussianRandom(), GaussianRandom(), GaussianRandom(), GaussianRandom()));
            }
        }
        GaussianNoise.Apply();
        SaveIntoFile("GaussianNoise",GaussianNoise);
        return GaussianNoise;
    }
    /// <summary>
    /// 创建蝶形运算纹理
    /// </summary>
    private Texture2D CreateButterflyRT()
    {
        Texture2D Butterfly = new Texture2D(fftSize , FFTPow, TextureFormat.RGBAFloat, false, true);
        for (int i = 0; i < FFTPow; i++)
        {
            int nBlocks = (int)Mathf.Pow(2, FFTPow - i - 1);  // 块
            int nHInputs = (int)Mathf.Pow(2, i);    // 两个复数的索引间隔

            for (int j = 0; j < nBlocks; j++)
            {
                for (int k = 0; k < nHInputs; k++)
                {
                    int i1, i2, j1, j2;
                    // 蝶形网络第一步，先按照bit位进行调换，重新排序
                    if (i == 0)
                    {
                        i1 = j * nHInputs * 2 + k;
                        i2 = j * nHInputs * 2 + nHInputs + k;
                        j1 = BitReverse(i1);
                        j2 = BitReverse(i2);
                    }
                    else
                    {
                        i1 = j * nHInputs * 2 + k;
                        i2 = j * nHInputs * 2 + nHInputs + k;
                        j1 = i1;
                        j2 = i2;
                    }
                    // 权值W
                    float theta = 2.0f * Mathf.PI * (float)(k * nBlocks) / (float)fftSize;
                    float wr = Mathf.Cos(theta);
                    float wi = Mathf.Sin(theta);
                    float index1 = j1 * 1.0f;
                    float index2 = j2 * 1.0f;
                    if (i == FFTPow - 1)
                    {
                        //wr *= -1.0f;
                        //wi *= -1.0f;
                    }

                    Butterfly.SetPixel(i1, i, new Vector4(index1, index2, wr, wi));
                    Butterfly.SetPixel(i2, i, new Vector4(index1, index2, -wr, -wi));
                }
            }
        }
        Butterfly.Apply();
        SaveIntoFile("Butterfly", Butterfly);
        return Butterfly;
    }
    /// <summary>
    /// 生成高斯随机数
    /// </summary>
    private float GaussianRandom()
    {
        return Mathf.Cos(2 * Mathf.PI * UnityEngine.Random.value) * Mathf.Sqrt(-2 * Mathf.Log(UnityEngine.Random.value));
    }
    /// <summary>
    /// 生成初始频谱
    /// </summary>
    private void GetInitSpectrum()
    {
        // 创建初始频谱
        InitSpectrumCs.SetInt("fftSize", fftSize);
        InitSpectrumCs.SetInt("OceanLength", OceanLength);
        InitSpectrumCs.SetFloat("Depth", depth);
        InitSpectrumCs.SetFloat("Wave_A", waveA);

        InitSpectrumCs.SetTexture(kernelInitH0, "WaveData", WaveData);
        InitSpectrumCs.SetTexture(kernelInitH0, "H0", H0);
        InitSpectrumCs.SetTexture(kernelInitH0, "H0Conj", H0Conj);
        InitSpectrumCs.Dispatch(kernelInitH0, fftSize / 8, fftSize / 8, 1);
        for (int i = 0; i < windData.Count; i++)
        {
            windData[i].windDir.Normalize();
            InitSpectrumCs.SetVector("WindDir", windData[i].windDir);
            InitSpectrumCs.SetFloat("WindSpeed", windData[i].windSpeed);

            InitSpectrumCs.SetTexture(kernelInitPhillipsSpectrum, "WaveData", WaveData);
            InitSpectrumCs.SetTexture(kernelInitPhillipsSpectrum, "H0", H0);
            InitSpectrumCs.SetTexture(kernelInitPhillipsSpectrum, "H0Conj", H0Conj);
            InitSpectrumCs.Dispatch(kernelInitPhillipsSpectrum, fftSize / 8, fftSize / 8, 1);
        }

        // 设置初始频谱
        ComputeWithTimeCs.SetTexture(kernelCreateSpectrumWithTime, "H0", H0);
        ComputeWithTimeCs.SetTexture(kernelCreateSpectrumWithTime, "H0Conj", H0Conj);
        ComputeWithTimeCs.SetTexture(kernelCreateSpectrumWithTime, "WaveData", WaveData);
    }
    /// <summary>
    ///  生成每帧的高度,梯度，偏移频谱
    /// </summary>
    private void GetSpectrum()
    {
        ComputeWithTimeCs.SetFloat("time",time);
        ComputeWithTimeCs.SetTexture(kernelCreateSpectrumWithTime, "HeightSpectrumRT", HeightSpectrumRT);
        ComputeWithTimeCs.SetTexture(kernelCreateSpectrumWithTime, "DisplacementSpectrumRT", DisplacementSpectrumRT);
        ComputeWithTimeCs.SetTexture(kernelCreateSpectrumWithTime, "GradientSpectrumRT", GradientSpectrumRT);
        ComputeWithTimeCs.Dispatch(kernelCreateSpectrumWithTime, fftSize / 8, fftSize / 8, 1);
    }

    /// <summary>
    /// FFT变换
    /// </summary>
    private void FastFourierTransform()
    {
        ComputeFFTCs.SetInt("fftSize", fftSize);
        int isEnd = 1;
        // 横向运算
        for (int i = 0; i< FFTPow; i++)
        {
            ComputeFFTCs.SetInt("stage", i);
            isEnd = i == FFTPow - 1 ? 1 : 0;
            ComputeFFTCs.SetInt("isEnd", isEnd);
            ComputeFFT(kernelComputeFFTH, ref HeightSpectrumRT);
            ComputeFFT(kernelComputeFFTH, ref DisplacementSpectrumRT);
            ComputeFFT(kernelComputeFFTH, ref GradientSpectrumRT);
        }
        // 纵向运算
        for (int i = 0; i < FFTPow; i++)
        {
            ComputeFFTCs.SetInt("stage", i);
            isEnd = i == FFTPow - 1 ? 1 : 0;
            ComputeFFTCs.SetInt("isEnd", isEnd);
            ComputeFFT(kernelComputeFFTV, ref HeightSpectrumRT);
            ComputeFFT(kernelComputeFFTV, ref DisplacementSpectrumRT);
            ComputeFFT(kernelComputeFFTV, ref GradientSpectrumRT);
        }
    }
    private void ComputeFFT(int kernelID, ref RenderTexture input)
    {
        ComputeFFTCs.SetTexture(kernelID, "InputRT", input);
        ComputeFFTCs.SetTexture(kernelID, "OutputRT", TempRT);
        ComputeFFTCs.Dispatch(kernelID, fftSize / 8, fftSize / 8, 1);

        RenderTexture rt = input;
        input = TempRT;
        TempRT = rt;
    }
    /// <summary>
    /// 创建渲染纹理
    /// </summary>
    private RenderTexture CreateRenderTexture(int size)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }
    /// <summary>
    /// 设置渲染纹理
    /// </summary>
    private void SetMaterialTexture()
    {
        float divideOceanL = OceanLength == 0 ? 0 : 1.0f / (float)OceanLength;
        ComputeWithTimeCs.SetFloat("divideOceanL", divideOceanL);
        ComputeWithTimeCs.SetTexture(kernelCreateRenderTextureWithTime,"HeightSpectrumRT", HeightSpectrumRT);
        ComputeWithTimeCs.SetTexture(kernelCreateRenderTextureWithTime,"DisplacementSpectrumRT", DisplacementSpectrumRT);
        ComputeWithTimeCs.SetTexture(kernelCreateRenderTextureWithTime, "GradientSpectrumRT", GradientSpectrumRT);
        ComputeWithTimeCs.SetTexture(kernelCreateRenderTextureWithTime, "NormalRT", NormalRT);
        ComputeWithTimeCs.SetTexture(kernelCreateRenderTextureWithTime, "DisplaceRT", DisplaceRT);
        ComputeWithTimeCs.Dispatch(kernelCreateRenderTextureWithTime, fftSize / 8, fftSize / 8, fftSize / 8);

        oceanMaterial.SetTexture("_Displace", DisplaceRT);
        oceanMaterial.SetTexture("_Normal", NormalRT);
        //NormalMat.SetTexture("_MainTex", HeightSpectrumRT);
        DisplaceMat.SetTexture("_MainTex", DisplaceRT);
        //NormalMat.SetTexture("_MainTex", DeviationZSpectrumRT);
        NormalMat.SetTexture("_MainTex", NormalRT);
    }

    /// <summary>
    /// 将生成的纹理图储存到文件
    /// </summary>
    private void SaveIntoFile(string name, Texture2D texture)
    {
        string filename = name + "Texture" + fftSize.ToString() + "x" + fftSize.ToString();
        string path = "Assets/Resources/Textures/";
        UnityEditor.AssetDatabase.CreateAsset(texture, path + filename + ".asset");
    }
    /// <summary>
    /// 调试，查看中间输出纹理
    /// </summary>
    private void SetDebugTexture()
    {
        DebugMat.SetTexture("_MainTex", DisplacementSpectrumRT);
    }
    /// <summary>
    /// 生成蝶形纹理需要用到的比特位调换
    /// </summary>
    int BitReverse(int i)
    {
        int j = i;
        int Sum = 0;
        int W = 1;
        int M = fftSize / 2;
        while (M != 0)
        {
            j = ((i & M) > M - 1) ? 1 : 0;
            Sum += j * W;
            W *= 2;
            M /= 2;
        }
        return Sum;
    }
    // 结束时将buffer释放
    private void OnDisable()
    {
    }
}
