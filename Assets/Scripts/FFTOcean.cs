using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class FFTOcean : MonoBehaviour
{
    // 网格生成
    private Mesh mesh;
    private MeshFilter filter;
    private MeshRenderer render;
    public int meshSize = 100;      // 网格大小，每行/列的顶点数
    public int meshLength = 512;      // 整个网格的长宽,海平面大小，对应Lx与Lz(方形海域Lx=Lz)
    public int samplingPoint = 9;   // 频域坐标采样点数量（2的指数幂），对应N,M (N=M)
    public float DisplaceScale = 1;   // 水平偏移
    public float HeightScale = 1;     // 数值偏移
    private int fftSize;          // pow(2,samplingPoint)
    private int[] vertIndex;        // 网格三角形索引
    private Vector3[] position;     // 网格顶点位置
    private Vector2[] uv;           // 顶点uv坐标
    private Vector3[] normal;       // 顶点法向量
    // 材质
    public Material oceanMaterial;              // 海洋材质
    public Material DisplaceMat;                // 显示偏移纹理材质
    public Material NormalMat;                  // 显示法线纹理材质

    private RenderTexture DisplaceRT;           // 偏移纹理
    private RenderTexture NormalRT;             // 法线纹理
    // 频谱数据
    private RenderTexture HeightSpectrumRT;     // 高度频谱
    private RenderTexture DeviationXSpectrumRT; // x偏移频谱
    private RenderTexture DeviationZSpectrumRT; // z偏移频谱
    private RenderTexture GradientXSpectrumRT;  // x梯度频谱
    private RenderTexture GradientZSpectrumRT;  // z梯度频谱
    private RenderTexture TempRT;               // 临时存储中间过程
    // 海洋参数
    public ComputeShader oceanCS;             // 计算海洋的compute shader
    private float[] gaussianRandom;           // h0高斯随机数
    private float[] gaussianRandomConj;       // h0Conj高斯随机数
    public Vector2 windDir;                   // 风向
    public float windSpeed;                   // 风速
    public float amplitude;                   // 菲利普参数，影响波浪高度
    public float timeScale;                   // 时间系数


    private ComputeBuffer gaussBuff;            // h0高斯随机数缓冲区
    private ComputeBuffer gaussBuffConj;           // hoConj高斯随机数缓冲区
    private float time = 0;                     // 时间

    // KernelID
    private int kernelCreateHeightSpectrum;      // 生成高度频谱
    private int kernelCreateDeviationSpectrum;   // 生成偏移频谱
    private int kernelCreateGradientSpectrum;    // 生成梯度频谱
    private int kernelFFT;                       // FFT运算
    private int kernelCreateRenderTexture;       // 生成纹理
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

    // Start is called before the first frame update
    void Start()
    {
        fftSize = (int)Mathf.Pow(2, samplingPoint);
        CreateMesh();
        InitialComputeShaderValue();
    }

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime * timeScale;
        CreateComputeShaderValue();
    }

    // 创建海面网格
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
                    position[index] = new Vector3((j - meshSize / 2.0f) * meshLength / meshSize, 0, (i - meshSize / 2.0f) * meshLength / meshSize);
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

    // 生成高斯随机数
    private void CreateGaussianRandom()
    {
        float pi = 3.1415926535f;
        gaussianRandom = new float[fftSize * fftSize * 2];
        gaussianRandomConj = new float[fftSize * fftSize * 2];
        for (int i = 0; i < gaussianRandom.Length; i+=2)
        {
            float u1 = Random.Range(0, 1f);
            float u2 = Random.Range(0, 1f);
            float x1 = Mathf.Cos(2 * pi * u1) * Mathf.Sqrt(-2 * Mathf.Log(u2));
            float x2 = Mathf.Sin(2 * pi * u1) * Mathf.Sqrt(-2 * Mathf.Log(u2));
            gaussianRandom[i] = x1;
            gaussianRandom[i + 1] = x2;

            float u3 = Random.Range(0, 1f);
            float u4 = Random.Range(0, 1f);
            float x3 = Mathf.Cos(2 * pi * u3) * Mathf.Sqrt(-2 * Mathf.Log(u4));
            float x4 = Mathf.Sin(2 * pi * u3) * Mathf.Sqrt(-2 * Mathf.Log(u4));
            gaussianRandomConj[i] = x3;
            gaussianRandomConj[i + 1] = x4;
        }

        //DebugData();
    }
    // 初始化computer Shader相关数据值
    private void InitialComputeShaderValue()
    {
        if (HeightSpectrumRT != null && HeightSpectrumRT.IsCreated())
        {
            HeightSpectrumRT.Release();
            DeviationXSpectrumRT.Release();
            DeviationZSpectrumRT.Release();
            GradientXSpectrumRT.Release();
            GradientZSpectrumRT.Release();
            TempRT.Release();
            DisplaceRT.Release();
            NormalRT.Release();
        }
        // 创建RenderTexture
        HeightSpectrumRT = CreateRenderTexture(fftSize);
        DeviationXSpectrumRT = CreateRenderTexture(fftSize);
        DeviationZSpectrumRT = CreateRenderTexture(fftSize);
        GradientXSpectrumRT = CreateRenderTexture(fftSize);
        GradientZSpectrumRT = CreateRenderTexture(fftSize);
        TempRT = CreateRenderTexture(fftSize);
        DisplaceRT = CreateRenderTexture(fftSize);
        NormalRT = CreateRenderTexture(fftSize);
        // 获取kernelID
        kernelCreateHeightSpectrum = oceanCS.FindKernel("CreateHeightSpectrum");
        kernelCreateDeviationSpectrum = oceanCS.FindKernel("CreateDeviationSpectrum");
        kernelCreateGradientSpectrum = oceanCS.FindKernel("CreateGradientSpectrum");
        kernelFFT = oceanCS.FindKernel("FFT");
        kernelCreateRenderTexture = oceanCS.FindKernel("CreateRenderTexture");
        // 设置初始Compute Shader 数据
        oceanCS.SetInt("fftSize", fftSize);
        oceanCS.SetInt("oceanLength", meshLength);
        // 生成高斯随机数
        CreateGaussianRandom();
        gaussBuff = new ComputeBuffer(gaussianRandom.Length, 4);
        gaussBuffConj = new ComputeBuffer(gaussianRandomConj.Length, 4);
        gaussBuff.SetData(gaussianRandom);
        gaussBuffConj.SetData(gaussianRandomConj);
        oceanCS.SetBuffer(kernelCreateHeightSpectrum, "GaussianRandomList", gaussBuff);
        oceanCS.SetBuffer(kernelCreateHeightSpectrum, "GaussianRandomListConj", gaussBuffConj);
    }
    // 每帧计算海洋数据
    private void CreateComputeShaderValue()
    {
        windDir.Normalize();
        oceanCS.SetFloat("A", amplitude);
        oceanCS.SetFloat("time", time);
        oceanCS.SetVector("windDir", new Vector2(windDir.x,windDir.y));
        oceanCS.SetFloat("windSpeed", windSpeed);

        // 生成高度频谱
        oceanCS.SetTexture(kernelCreateHeightSpectrum, "HeightSpectrumRT", HeightSpectrumRT);
        oceanCS.Dispatch(kernelCreateHeightSpectrum, fftSize / 8, fftSize / 8, 1);
        // 生成偏移频谱
        oceanCS.SetTexture(kernelCreateDeviationSpectrum, "HeightSpectrumRT", HeightSpectrumRT);
        oceanCS.SetTexture(kernelCreateDeviationSpectrum, "DeviationXSpectrumRT", DeviationXSpectrumRT);
        oceanCS.SetTexture(kernelCreateDeviationSpectrum, "DeviationZSpectrumRT", DeviationZSpectrumRT);
        oceanCS.Dispatch(kernelCreateDeviationSpectrum, fftSize / 8, fftSize / 8, 1);
        // 生成梯度频谱
        oceanCS.SetTexture(kernelCreateGradientSpectrum, "HeightSpectrumRT", HeightSpectrumRT);
        oceanCS.SetTexture(kernelCreateGradientSpectrum, "GradientXSpectrumRT", GradientXSpectrumRT);
        oceanCS.SetTexture(kernelCreateGradientSpectrum, "GradientZSpectrumRT", GradientZSpectrumRT);
        oceanCS.Dispatch(kernelCreateGradientSpectrum, fftSize / 8, fftSize / 8, 1);

        for (int i = 1; i <= samplingPoint; i++)
        {
            // 横向向fft
            oceanCS.SetInt("isHV", 2);
            // 设置当前的阶段
            oceanCS.SetInt("stage", i);
            int isFFTEnd = i == samplingPoint ? -1 : 1;
            // 是否是FFT的最后一个阶段
            oceanCS.SetInt("isFFTEnd", isFFTEnd);
            
            ComputeFFT(kernelFFT, ref HeightSpectrumRT);
            ComputeFFT(kernelFFT, ref DeviationXSpectrumRT);
            ComputeFFT(kernelFFT, ref DeviationZSpectrumRT);
            ComputeFFT(kernelFFT, ref GradientXSpectrumRT);
            ComputeFFT(kernelFFT, ref GradientZSpectrumRT);
        }
        
        for (int j = 1; j <= samplingPoint ; j++)
        {
            // 纵向向fft
            oceanCS.SetInt("isHV", 1);
            // 设置当前的阶段
            oceanCS.SetInt("stage", j);
            int isFFTEnd = j == samplingPoint ? -1 : 1;
            // 是否是FFT的最后一个阶段
            oceanCS.SetInt("isFFTEnd", isFFTEnd);

            ComputeFFT(kernelFFT, ref HeightSpectrumRT);
            ComputeFFT(kernelFFT, ref DeviationXSpectrumRT);
            ComputeFFT(kernelFFT, ref DeviationZSpectrumRT);
            ComputeFFT(kernelFFT, ref GradientXSpectrumRT);
            ComputeFFT(kernelFFT, ref GradientZSpectrumRT);
        }
        
        // 生成纹理
       oceanCS.SetFloat("HeightScale", HeightScale);
       oceanCS.SetFloat("DisplaceScale", DisplaceScale);

       oceanCS.SetTexture(kernelCreateRenderTexture, "HeightSpectrumRT", HeightSpectrumRT);
       oceanCS.SetTexture(kernelCreateRenderTexture, "DeviationXSpectrumRT", DeviationXSpectrumRT);
       oceanCS.SetTexture(kernelCreateRenderTexture, "DeviationZSpectrumRT", DeviationZSpectrumRT);
       oceanCS.SetTexture(kernelCreateRenderTexture, "GradientXSpectrumRT", GradientXSpectrumRT);
       oceanCS.SetTexture(kernelCreateRenderTexture, "GradientZSpectrumRT", GradientZSpectrumRT);
       oceanCS.SetTexture(kernelCreateRenderTexture, "DisplaceRT", DisplaceRT);
       oceanCS.SetTexture(kernelCreateRenderTexture, "NormalRT", NormalRT);
       oceanCS.Dispatch(kernelCreateRenderTexture, fftSize / 8, fftSize / 8, 1);
       
        SetMaterialTexture();
    }
    // 创建渲染纹理
    private RenderTexture CreateRenderTexture(int size)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }
    // 设置渲染纹理
    private void SetMaterialTexture()
    {
        oceanMaterial.SetTexture("_Displace", DisplaceRT);
        oceanMaterial.SetTexture("_Normal", NormalRT);
        //NormalMat.SetTexture("_MainTex", HeightSpectrumRT);
        DisplaceMat.SetTexture("_MainTex", DisplaceRT);
        //NormalMat.SetTexture("_MainTex", DeviationZSpectrumRT);
        NormalMat.SetTexture("_MainTex", NormalRT);
    }
    private void ComputeFFT(int kernelID, ref RenderTexture inputRT)
    {
        oceanCS.SetTexture(kernelID, "InputFFT", inputRT);
        oceanCS.SetTexture(kernelID, "OutputFFT", TempRT);
        oceanCS.Dispatch(kernelID, fftSize / 8, fftSize / 8, 1);

        RenderTexture rt = inputRT;
        inputRT = TempRT;
        TempRT = rt;
    }
    private void DebugData()
    {
        string filePath = "../Debug/GaussianData.csv";

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
        }
        StringBuilder builder = new StringBuilder();
        for (int i=0;i<gaussianRandom.Length;i++)
        {
            builder.Append(gaussianRandom[i] + "\n");
        }

        File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
    }
    // 结束时将buffer释放
    private void OnDisable()
    {
        gaussBuff.Dispose();
        gaussBuffConj.Dispose();
    }
}
