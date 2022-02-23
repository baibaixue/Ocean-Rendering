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
    private int fftSize;          // pow(2,samplingPoint)
    private int[] vertIndex;        // 网格三角形索引
    private Vector3[] position;     // 网格顶点位置
    private Vector2[] uv;           // 顶点uv坐标
    private Vector3[] normal;       // 顶点法向量
    // 材质
    public Material oceanMaterial;
    private RenderTexture HeightSpectrumRT;     // 高度频谱
    // 海洋参数
    public ComputeShader oceanCS;             // 计算海洋的compute shader
    private float[] gaussianRandom;           // 高斯随机数
    public Vector2 windDir;                   // 风向
    public float windSpeed;                   // 风速
    public float amplitude;                   // 菲利普参数，影响波浪高度
    public float timeScale;                   // 时间系数


    private ComputeBuffer gaussBuff;            // 高斯随机数缓冲区
    private float time = 0;                     // 时间

    // KernelID
    private int kernelCreateHightSpectrum;      // 生成高度频谱

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
        for (int i = 0; i < gaussianRandom.Length; i+=2)
        {
            float u1 = Random.Range(0, 1.0f);
            float u2 = Random.Range(0, 1.0f);
            float x1 = Mathf.Cos(2 * pi * u1) * Mathf.Sqrt(-2 * Mathf.Log(u2));
            float x2 = Mathf.Sin(2 * pi * u1) * Mathf.Sqrt(-2 * Mathf.Log(u2));
            gaussianRandom[i] = x1;
            gaussianRandom[i + 1] = x2;
        }

        //DebugData();
    }
    // 初始化computer Shader相关数据值
    private void InitialComputeShaderValue()
    {
        if (HeightSpectrumRT != null && HeightSpectrumRT.IsCreated())
        {
            HeightSpectrumRT.Release();
        }
        // 创建RenderTexture
        HeightSpectrumRT = CreateRenderTexture(fftSize);
        // 获取kernelID
        kernelCreateHightSpectrum = oceanCS.FindKernel("CreateHightSpectrum");
        // 设置初始Compute Shader 数据
        oceanCS.SetInt("fftSize", fftSize);
        oceanCS.SetInt("oceanLength", meshLength);
        // 生成高斯随机数
        CreateGaussianRandom();
        gaussBuff = new ComputeBuffer(gaussianRandom.Length, 4);
        gaussBuff.SetData(gaussianRandom);
        oceanCS.SetBuffer(kernelCreateHightSpectrum, "GaussianRandomList", gaussBuff);
    }
    // 每帧计算海洋数据
    private void CreateComputeShaderValue()
    {
        oceanCS.SetFloat("A", amplitude);
        oceanCS.SetFloat("time", time);
        windDir.Normalize();
        oceanCS.SetVector("windDir", windDir * windSpeed);
        oceanCS.SetFloat("windSpeed", windSpeed);

        // 生成高度频谱
        oceanCS.SetTexture(kernelCreateHightSpectrum, "CreateHightSpectrum", HeightSpectrumRT);
        oceanCS.Dispatch(kernelCreateHightSpectrum, fftSize / 8, fftSize / 8, 1);
    }
    // 创建渲染纹理
    private RenderTexture CreateRenderTexture(int size)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
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
}
