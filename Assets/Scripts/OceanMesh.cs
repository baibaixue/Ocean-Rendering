using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanMesh : MonoBehaviour
{
    [SerializeField, Range(1,50)]
    int GridDensity = 25;           // 网格复杂度，实际网格数量 = 4 * GridDensity
    [SerializeField]
    int OceanSize = 512;            // 海面大小
    [SerializeField]
    int SimplifyLevel = 5;          // 简化层次

    int GridSize;                   // 网格数量
    public Material OceanMaterial;      // 材质

    OceanCompute oceanCompute;          // 海洋数据相关计算
    float time = 0;

    public int FFTPow;
    // FFTOcean的相关数据
    public Material DisplaceMat;                    // 显示偏移纹理材质
    public Material NormalMat;                      // 显示法线纹理材质
    public Material DebugMat;                       // debug材质，查看中间数据

    public ComputeShader OceanCs;                   // 计算海洋的compute shader
    public ComputeShader InitSpectrumCs;            // 计算初始频谱的compute shader
    public ComputeShader ComputeFFTCs;              // 进行FFT运算的compute shader
    public ComputeShader ComputeWithTimeCs;         // 计算随时间变换的频谱和纹理的 compute shader

    [Range(-1.0f, 1.0f)]
    public float lambda = 1.0f;                            // 偏移系数
    public List<WindData> windData;                 // 风信息
    public float waveA;                             // 菲利普参数，影响波浪高度
    public float depth;                             // 水深
    public float timeScale;                         // 时间系数
    /// <summary>
    /// 网格元素，包含网格对象MeshGo 和 渲染材质 MeshRenderer
    /// </summary>
    class MeshElement
    {
        public GameObject MeshGo;
        public MeshRenderer MeshRenderer;

        public MeshElement(GameObject meshGo, MeshRenderer meshRemderer)
        {
            MeshGo = meshGo;
            MeshRenderer = meshRemderer;
        }
    }

    List<MeshElement> meshElements = new List<MeshElement>();

    void Start()
    {
        // 初始化网格
        InitMeshes();
        //创建网格
        CreateMesh();

        // 初始化海洋相关数据
        InitOceanData();
    }
    /// <summary>
    /// 初始化海洋相关数据
    /// </summary>
    void InitOceanData()
    {
        oceanCompute = new OceanCompute(OceanSize,FFTPow,
                                        OceanMaterial, DisplaceMat, NormalMat, DebugMat, 
                                        OceanCs, InitSpectrumCs, ComputeFFTCs, ComputeWithTimeCs,
                                        lambda, windData, waveA, depth);
        oceanCompute.GetNoiseAndButterFlyTexture();
        oceanCompute.GetInitSpectrum();
    }
    /// <summary>
    /// 初始化网格用到的数据以及重置参数
    /// </summary>
    void InitMeshes()
    {
        foreach (var child in gameObject.GetComponentsInChildren<Transform>())
        {
            if (child != transform) Destroy(child.gameObject);
        }
        meshElements.Clear();

        GridSize = OceanSize / GridNum();
    }
    /// <summary>
    /// 创建网格，包括中心网格和环状网格
    /// </summary>
    void CreateMesh()
    {
        int halfLevel = Mathf.FloorToInt(SimplifyLevel) ;
        int k = GridNum();
        Mesh ExpandMesh = CreateExpandMesh(k);
        for (int i = 0; i <= SimplifyLevel; i++)
        {
            float scale = Mathf.Pow(2, i - halfLevel);
            if (i == 0)
                meshElements.Add(InstanceElement("Center", CreatePanelMesh(k, k), OceanMaterial, scale));
            else
                meshElements.Add(InstanceElement("Expand" + i.ToString(), ExpandMesh, OceanMaterial, scale));
        }
    }
    /// <summary>
    /// 初始化网格元素
    /// </summary>
    /// <param name="name">网格元素名称</param>
    /// <param name="mesh">对应网格</param>
    /// <param name="material"> 材质</param>
    /// <param name="Scale"> 放缩倍数</param>
    /// <returns></returns>
    MeshElement InstanceElement(string name, Mesh mesh, Material material,float Scale)
    {
        GameObject go = new GameObject();
        go.name = name;
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = new Vector3(Scale, 1, Scale);
        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.material = material;
        return new MeshElement(go, meshRenderer);
    }
    /// <summary>
    /// 创建平面网格
    /// </summary>
    /// <param name="Width">平面网格宽</param>
    /// <param name="Height">平面网格高</param>
    /// <returns></returns>
    Mesh CreatePanelMesh(int Width, int Height)
    {
        int pointx = Width + 1;
        int pointy = Height + 1;
        Mesh mesh = new Mesh();
        if (pointx * pointy >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        // 顶点和三角形面数据
        int[] vertIndex = new int[Width * Height * 6];
        Vector3[] position = new Vector3[pointx * pointy];
        Vector2[] uv = new Vector2[pointx * pointy];
        Vector3[] normal = new Vector3[pointx * pointy];

        int count = 0;
        for (int i = 0; i < pointy; i++)
        {
            for (int j = 0; j < pointx; j++)
            {
                int index = i * pointx + j;
                position[index] = new Vector3((j - Width / 2.0f), 0, (i - Width / 2.0f)) * GridSize;
                uv[index] = new Vector2(1.0f * j / GridSize, 1.0f * i / GridSize);
                normal[index] = new Vector3(0, 1, 0);
                if (i != pointy - 1 && j != pointx - 1)
                {
                    vertIndex[count++] = index;
                    vertIndex[count++] = index + pointx;
                    vertIndex[count++] = index + pointx + 1;

                    vertIndex[count++] = index;
                    vertIndex[count++] = index + pointx + 1;
                    vertIndex[count++] = index + 1;
                }
            }
        }
        mesh.vertices = position;
        mesh.SetIndices(vertIndex, MeshTopology.Triangles, 0);
        mesh.uv = uv;
        mesh.normals = normal;
        return mesh;
    }
    /// <summary>
    /// 创建环状网格
    /// </summary>
    /// <param name="GridNum">网格数量</param>
    /// <returns></returns>
    Mesh CreateExpandMesh(int GridNum)
    {
        Mesh mesh = new Mesh();
        if ((GridNum + 1) * (GridNum + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        CombineInstance[] combine = new CombineInstance[4];
        combine[0].mesh = CreatePanelMesh(GridNum - GridDensity, GridDensity);
        combine[0].transform = Matrix4x4.TRS(new Vector3(0.5f,0,-0.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePanelMesh(GridDensity,GridNum - GridDensity);
        combine[1].transform = Matrix4x4.TRS(new Vector3(-1.5f, 0, -1.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        combine[2].mesh = CreatePanelMesh(GridNum - GridDensity, GridDensity);
        combine[2].transform = Matrix4x4.TRS(new Vector3(-0.5f, 0, 2.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        combine[3].mesh = CreatePanelMesh(GridDensity, GridNum - GridDensity);
        combine[3].transform = Matrix4x4.TRS(new Vector3(1.5f, 0, -0.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }
    /// <summary>
    /// 网格数量
    /// </summary>
    int GridNum()
    {
        return 4 * GridDensity;
    }
    void Update()
    {
        time += Time.deltaTime * timeScale;

        oceanCompute.GetSpectrum(time);
        oceanCompute.FastFourierTransform();
        oceanCompute.SetMaterialTexture();
    }
}
