using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanMesh : MonoBehaviour
{
    Transform viewer;               // 相机视点
    [SerializeField, Range(1,50)]
    int GridDensity = 25;           // 网格复杂度，实际网格数量 = 4 * GridDensity
    [SerializeField]
    int OceanSize = 512;            // 海面大小
    // 三种波浪大小
    [SerializeField]
    int WaveSize_0 = 250;               
    [SerializeField]
    int WaveSize_1 = 20;
    [SerializeField]
    int WaveSize_2 = 5;

    [SerializeField]
    int SimplifyLevel = 5;          // 简化层次

    int GridSize;                   // 网格数量
    public Material OceanMaterial;      // 材质
    Material[] materials;
    // 海洋数据相关计算               
    OceanCompute oceanCompute0;          
    OceanCompute oceanCompute1;
    OceanCompute oceanCompute2;

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
    public float lambda = 1.0f;                     // 偏移系数
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
    List<MeshElement> rings = new List<MeshElement>();  // 环状网格
    List<MeshElement> trims = new List<MeshElement>();  // 不同网格对象的接缝处
    MeshElement center;     // 中心网格

    Quaternion[] trimRotations; // 接缝的旋转方向
    /// <summary>
    /// 不同网格对象的接缝处理
    /// </summary>
    [System.Flags]
    enum Seams
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        All = Left | Right | Top | Bottom
    };
    
    void Start()
    {
        if (viewer == null)
            viewer = Camera.main.transform;
        // 材质初始化
        materials = new Material[3];
        materials[0] = new Material(OceanMaterial);
        materials[0].EnableKeyword("CLOSE");

        materials[1] = new Material(OceanMaterial);
        materials[1].EnableKeyword("MID");
        materials[1].DisableKeyword("CLOSE");

        materials[2] = new Material(OceanMaterial);
        materials[2].DisableKeyword("MID");
        materials[2].DisableKeyword("CLOSE");

        trimRotations = new Quaternion[]
        {
            Quaternion.AngleAxis(180, Vector3.up),
            Quaternion.AngleAxis(90, Vector3.up),
            Quaternion.AngleAxis(270, Vector3.up),
            Quaternion.identity,
        };
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
    public void InitOceanData()
    {
        float boundary1 = 2 * Mathf.PI / WaveSize_1 * 6f;
        float boundary2 = 2 * Mathf.PI / WaveSize_2 * 6f;
        oceanCompute0 = new OceanCompute(WaveSize_0,FFTPow,
                                        OceanMaterial, DisplaceMat, NormalMat, DebugMat, 
                                        OceanCs, InitSpectrumCs, ComputeFFTCs, ComputeWithTimeCs,
                                        lambda, windData, waveA, depth,0.0001f,boundary1);
        oceanCompute0.GetNoiseAndButterFlyTexture();
        oceanCompute0.GetInitSpectrum();

        oceanCompute1 = new OceanCompute(WaveSize_1, FFTPow,
                                OceanMaterial, DisplaceMat, NormalMat, DebugMat,
                                OceanCs, InitSpectrumCs, ComputeFFTCs, ComputeWithTimeCs,
                                lambda, windData, waveA, depth,boundary1,boundary2);
        oceanCompute1.GetNoiseAndButterFlyTexture();
        oceanCompute1.GetInitSpectrum();

        oceanCompute2 = new OceanCompute(WaveSize_2, FFTPow,
                        OceanMaterial, DisplaceMat, NormalMat, DebugMat,
                        OceanCs, InitSpectrumCs, ComputeFFTCs, ComputeWithTimeCs,
                        lambda, windData, waveA, depth,boundary2,9999);
        oceanCompute2.GetNoiseAndButterFlyTexture();
        oceanCompute2.GetInitSpectrum();
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
        rings.Clear();
        trims.Clear();


        GridSize = OceanSize / GridNum();
    }
    /// <summary>
    /// 创建网格，包括中心网格和环状网格
    /// </summary>
    void CreateMesh()
    {
        int k = GridNum();
        center = InstanceElement("Center", CreatePlaneMesh(2 * k, 2 * k, 1, Seams.All), OceanMaterial);
        Mesh ring = CreateExpandMesh(k, 1);
        Mesh trim = CreateTrimMesh(k, 1);

        for (int i = 0; i < SimplifyLevel; i++)
        {
            rings.Add(InstanceElement("Ring" + i.ToString(), ring, OceanMaterial));
            trims.Add(InstanceElement("Trim" + i.ToString(), trim, OceanMaterial));
        }
        UpdateMeshPosition();
    }
    /*
    void CreateMesh()
    {
        int halfLevel = Mathf.FloorToInt(SimplifyLevel / 3) ;
        int k = GridNum();
        Mesh ExpandMesh = CreateExpandMesh(k);
        for (int i = 0; i <= SimplifyLevel; i++)
        {
            float scale = Mathf.Pow(2, i - halfLevel);
            if (i == 0)
                meshElements.Add(InstanceElement("Center", CreatePlaneMesh(k, k), OceanMaterial, scale));
            else
                meshElements.Add(InstanceElement("Expand" + i.ToString(), ExpandMesh, OceanMaterial, scale));
        }
    }
    */
    /// <summary>
    /// 初始化网格元素
    /// </summary>
    /// <param name="name">网格元素名称</param>
    /// <param name="mesh">对应网格</param>
    /// <param name="material"> 材质</param>
    /// <returns></returns>
    MeshElement InstanceElement(string name, Mesh mesh, Material material)
    {
        GameObject go = new GameObject();
        go.name = name;
        go.transform.SetParent(transform);  // 挂载在OceanObj下
        go.transform.localPosition = Vector3.zero;
        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        meshRenderer.material = material;
        meshRenderer.allowOcclusionWhenDynamic = false;
        return new MeshElement(go, meshRenderer);
    }
    /*
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
        //meshRenderer.material = material;
        return new MeshElement(go.transform,go, meshRenderer);
    }
    */
    /// <summary>
    /// 创建平面网格
    /// </summary>
    /// <param name="Width">平面网格宽</param>
    /// <param name="Height">平面网格高</param>
    /// <param name="lengthScale">网格长度的放缩倍数</param>
    /// <param name="seams">接缝处</param>
    /// <param name="trianglesShift">三角形位移</param>
    /// <returns></returns>
    Mesh CreatePlaneMesh(int width, int height, float legthScale, Seams seams = Seams.None, int trianglesShift = 0)
    {
        Mesh mesh = new Mesh();
        if ((width + 1) * (height + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;    // 扩大网格缓冲区

        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];   // 顶点数量
        int[] triangles = new int[width * height * 6];  // 三角形
        Vector3[] normals = new Vector3[(width + 1) * (height + 1)];    // 法向量

        for ( int i = 0; i <= height; i++)
        {
            for (int j = 0; j <= width; j++)
            {
                int x = j;
                int z = i;

                if ((i == 0 && seams.HasFlag(Seams.Bottom)) || (i == height && seams.HasFlag(Seams.Top)))
                    x = x / 2 * 2;
                if ((j == 0 && seams.HasFlag(Seams.Left)) || (j == width && seams.HasFlag(Seams.Right)))
                    z = z / 2 * 2;

                vertices[j + i * (width + 1)] = new Vector3(x, 0, z) * legthScale;
                normals[j + i * (width + 1)] = Vector3.up;
            }
        }

        int tris = 0;
        for ( int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                int k = j + i * (width + 1);
                if ((i + j + trianglesShift) % 2 == 0)
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;

                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 2;
                    triangles[tris++] = k + 1;
                }
                else
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + 1;

                    triangles[tris++] = k + 1;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;
                }
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        return mesh;
    }
    /*
    Mesh CreatePlaneMesh(int Width, int Height)
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
    */
    /// <summary>
    /// 创建环状网格
    /// </summary>
    /// <param name="k">网格数量</param>
    /// <param name="lengthScale">最外层边界的放缩大小</param>
    /// <returns></returns>
    Mesh CreateExpandMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();
        if ((2 * k + 1) * (2 * k + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        CombineInstance[] combine = new CombineInstance[4]; // 四个四边形平面网格进行合并

        combine[0].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left);
        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
        
        combine[0].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left);
        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Top | Seams.Right | Seams.Left);
        combine[1].transform = Matrix4x4.TRS(new Vector3(0, 0, k + 1 + (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[2].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Left);
        combine[2].transform = Matrix4x4.TRS(new Vector3(0, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[3].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Right);
        combine[3].transform = Matrix4x4.TRS(new Vector3(k + 1 + (k - 1) / 2, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }
    /// <summary>
    /// 创建接缝处网格
    /// </summary>
    /// <param name="k">网格数量</param>
    /// <param name="lengthScale">最外层边界放缩大小</param>
    /// <returns></returns>
    Mesh CreateTrimMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();

        CombineInstance[] combine = new CombineInstance[2]; // 两个网格拼接

        combine[0].mesh = CreatePlaneMesh(k + 1, 1, lengthScale, Seams.None, 1);
        combine[0].transform = Matrix4x4.TRS(new Vector3(-k - 1, 0, -1) * lengthScale, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePlaneMesh(1, k, lengthScale, Seams.None, 1);
        combine[1].transform = Matrix4x4.TRS(new Vector3(-1, 0, -k - 1) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }
    /*
    Mesh CreateExpandMesh(int GridNum)
    {
        Mesh mesh = new Mesh();
        if ((GridNum + 1) * (GridNum + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        CombineInstance[] combine = new CombineInstance[4];
        combine[0].mesh = CreatePlaneMesh(GridNum - GridDensity, GridDensity);
        combine[0].transform = Matrix4x4.TRS(new Vector3(0.5f,0,-0.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        combine[1].mesh = CreatePlaneMesh(GridDensity,GridNum - GridDensity);
        combine[1].transform = Matrix4x4.TRS(new Vector3(-1.5f, 0, -1.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        combine[2].mesh = CreatePlaneMesh(GridNum - GridDensity, GridDensity);
        combine[2].transform = Matrix4x4.TRS(new Vector3(-0.5f, 0, 2.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        combine[3].mesh = CreatePlaneMesh(GridDensity, GridNum - GridDensity);
        combine[3].transform = Matrix4x4.TRS(new Vector3(1.5f, 0, -0.5f) * GridDensity * GridSize, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }
    */
    /// <summary>
    /// 网格数量
    /// </summary>
    int GridNum()
    {
        return 4 * GridDensity + 1;
    }

    void Update()
    {
        //UpdateMeshPosition();
        time += Time.deltaTime * timeScale;

        oceanCompute0.GetSpectrum(time);
        oceanCompute0.FastFourierTransform();
        oceanCompute0.SetMaterialTexture(0);

        oceanCompute1.GetSpectrum(time);
        oceanCompute1.FastFourierTransform();
        oceanCompute1.SetMaterialTexture(1);

        oceanCompute2.GetSpectrum(time);
        oceanCompute2.FastFourierTransform();
        oceanCompute2.SetMaterialTexture(2);
        
        UpdateMaterial();
    }
    /// <summary>
    /// 材质更新
    /// </summary>
    void UpdateMaterial()
    {
        for(int i = 0; i < 3; i++)
        {
            materials[i].CopyPropertiesFromMaterial(OceanMaterial);
        }
      
        materials[0].EnableKeyword("CLOSE");
        materials[1].EnableKeyword("MID");
        materials[1].DisableKeyword("CLOSE");
        materials[2].DisableKeyword("MID");
        materials[2].DisableKeyword("CLOSE");

        center.MeshRenderer.material = GetMaterial(-1);
        for(int i = 0; i < SimplifyLevel; i++)
        {
            rings[i].MeshRenderer.material = GetMaterial(i);
            trims[i].MeshRenderer.material = GetMaterial(i);
        }
    }
    /// <summary>
    /// 根据层级决定材质
    /// </summary>
    /// <param name="level">网格层级</param>
    /// <returns></returns>
    Material GetMaterial(int level)
    {
        if (level - 2 <= 0)
            return materials[0];
        else if (level - 2 <= 2)
            return materials[1];
        return materials[2];
    }
    /// <summary>
    /// 更新网格位置
    /// </summary>
    void UpdateMeshPosition()
    {
        
        int k = GridNum();
        // 层级为-1 即为中心网格
        float scale = ClipLevelScale(-1);
        Vector3 previousSnappedPosition = Snap(viewer.position, scale * 2);
        // 中心位置和中心放缩大小
        center.MeshGo.transform.position = previousSnappedPosition + OffsetFromCenter(-1);
        center.MeshGo.transform.localScale = new Vector3(scale, 1, scale);
        for (int i = 0; i < SimplifyLevel; i++)
        {
            // 计算缩放比例
            scale = ClipLevelScale(i);
            // 距离中心偏移量
            Vector3 centerOffset = OffsetFromCenter(i);

            Vector3 snappedPosition = Snap(viewer.position, scale * 2);

            Vector3 trimPosition = centerOffset + snappedPosition + scale * (k - 1) / 2 * new Vector3(1, 0, 1);
            int shiftX = previousSnappedPosition.x - snappedPosition.x < float.Epsilon ? 1 : 0;
            int shiftZ = previousSnappedPosition.z - snappedPosition.z < float.Epsilon ? 1 : 0;
            trimPosition += shiftX * (k + 1) * scale * Vector3.right;
            trimPosition += shiftZ * (k + 1) * scale * Vector3.forward;
            trims[i].MeshGo.transform.position = trimPosition;
            trims[i].MeshGo.transform.rotation = trimRotations[shiftX + 2 * shiftZ];
            trims[i].MeshGo.transform.localScale = new Vector3(scale, 1, scale);

            rings[i].MeshGo.transform.position = snappedPosition + centerOffset;
            rings[i].MeshGo.transform.localScale = new Vector3(scale, 1, scale);
            previousSnappedPosition = snappedPosition;
        }
    }
    /// <summary>
    /// 每一层的放缩大小
    /// </summary>
    /// <param name="level">层级</param>
    /// <returns></returns>
    float ClipLevelScale(int level)
    {
        return OceanSize * 1.0f / GridNum() * Mathf.Pow(2, level + 1);
    }
    /// <summary>
    /// 距离中心网格的偏移量
    /// </summary>
    /// <param name="level">当前层级，为-1即网格为中心网格</param>
    /// <returns></returns>
    Vector3 OffsetFromCenter(int level)
    {
        int k = GridNum();
        return (Mathf.Pow(2, SimplifyLevel) + GeometricProgressionSum(2, 2, level + 1, SimplifyLevel - 1))
                * OceanSize / k * (k - 1) / 2 * new Vector3(-1, 0, -1);
    }
    /// <summary>
    /// 几何级数和
    /// </summary>
    /// <returns></returns>
    float GeometricProgressionSum(float b0, float q, int n1, int n2)
    {
        return b0 / (1 - q) * (Mathf.Pow(q, n2) - Mathf.Pow(q, n1));
    }

    /// <summary>
    /// 相机坐标修正
    /// </summary>
    /// <param name="coords">视点坐标</param>
    /// <param name="scale">放缩大小</param>
    /// <returns></returns>
    Vector3 Snap(Vector3 coords, float scale)
    {
        if (coords.x >= 0)
            coords.x = Mathf.Floor(coords.x / scale) * scale;
        else
            coords.x = Mathf.Ceil((coords.x - scale + 1) / scale) * scale;

        if (coords.z < 0)
            coords.z = Mathf.Floor(coords.z / scale) * scale;
        else
            coords.z = Mathf.Ceil((coords.z - scale + 1) / scale) * scale;

        coords.y = 0;
        return coords;
    }
}
