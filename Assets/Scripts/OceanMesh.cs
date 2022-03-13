using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanMesh : MonoBehaviour
{
    [SerializeField, Range(1,50)]
    int GridDensity = 25;
    [SerializeField]
    int OceanSize = 512;
    [SerializeField]
    int SimplifyLevel = 5;

    int GridSize;
    public Material OceanMaterial;      // 材质

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
    //public int Boundary;
    //public FFTOcean Ocean;

    int meshSize;
    // Start is called before the first frame update
    void Start()
    {

        //CreateCenterMesh();
        //SimplifyTimes = (int)Mathf.Log(CenterGridNum, 2) - 1;
        //ExpandLayers = (int)Mathf.Pow(2, Mathf.Max(SimplifyTimes - 1,0));
        //CreateSimplifyMesh(1, CenterGridNum, CenterGridSize);
        InitMeshes();
        CreateMesh();
    }

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
    /// 创建中心网格
    /// </summary>
    void CreateCenterMesh()
    {
        /*
        // 挂载Compontent
        GameObject go = new GameObject("Center");
        Mesh mesh = new Mesh();
        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        mesh.name = "Center";
        meshFilter.mesh = mesh;
        meshRenderer.material = OceanMaterial;
        go.gameObject.transform.SetParent(gameObject.transform, false);

        // 顶点和三角形面数据
        CenterPointNum = CenterGridNum + 1;
        int[] vertIndex = new int[CenterGridNum * CenterGridNum * 6];
        Vector3[] position = new Vector3[CenterPointNum * CenterPointNum];
        Vector2[] uv = new Vector2[CenterPointNum * CenterPointNum];
        Vector3[] normal = new Vector3[CenterPointNum * CenterPointNum];

        if (CenterPointNum == 1)
        {
            position[0] = new Vector3(0, 0, 0);
            uv[0] = new Vector2(0, 0);
            normal[0] = new Vector3(0, 1, 0);
        }
        else
        {
            int count = 0;
            for (int i = 0; i < CenterPointNum; i++)
            {
                for (int j = 0; j < CenterPointNum; j++)
                {
                    int index = i * CenterPointNum + j;
                    position[index] = new Vector3((j - CenterGridNum / 2.0f) * CenterGridSize, 0, (i - CenterGridNum / 2.0f) * CenterGridSize);
                    uv[index] = new Vector2(1.0f * j / CenterGridNum, 1.0f * i / CenterGridNum);
                    normal[index] = new Vector3(0, 1, 0);
                    if (i != CenterPointNum - 1 && j != CenterPointNum - 1)
                    {
                        vertIndex[count++] = index;
                        vertIndex[count++] = index + CenterPointNum;
                        vertIndex[count++] = index + CenterPointNum + 1;

                        vertIndex[count++] = index;
                        vertIndex[count++] = index + CenterPointNum + 1;
                        vertIndex[count++] = index + 1;
                    }
                }
            }
        }
        mesh.vertices = position;
        mesh.SetIndices(vertIndex, MeshTopology.Triangles, 0);
        mesh.uv = uv;
        mesh.normals = normal;
        */
    }
    void CreateMesh()
    {
        int halfLevel = Mathf.FloorToInt((SimplifyLevel + 1) / 2) ;
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
        meshRenderer.material = OceanMaterial;
        return new MeshElement(go, meshRenderer);
    }
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
    /// <summary>
    /// 创建扩展网格
    /// </summary>
    /// <param name="_Level">当前的简化层级</param>
    /// <param name="_CenterGrid">中心网格的最外层网格数量</param>
    /// <param name="_GrideSize">单个网格的边长</param>
     /*
    void CreateSimplifyMesh(int _Level,int _CenterGrid, int _GrideSize)
    {
        if (_Level > SimplifyTimes || _CenterGrid == 0) return;
        int GridNum = (int)Mathf.Floor(_CenterGrid / 2.0f) + 2 * (ExpandLayers);  // 最外层网格数
        int pointNum = GridNum + 1; // 最外层顶点数
        int Size = _GrideSize * 2 ;   // 最外层网格边长度

        // 挂载Compontent
        GameObject go = new GameObject("Simplify" + _Level.ToString());
        Mesh mesh = new Mesh();
        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        mesh.name = "Simplify" + _Level.ToString();
        meshFilter.mesh = mesh;
        meshRenderer.material = OceanMaterial;
        go.gameObject.transform.SetParent(gameObject.transform, false);

        // 顶点和三角形面数据
        int halfCenterGrid = _CenterGrid / 2;
        
        int[] vertIndex = new int[(GridNum * GridNum ) * 6];
        Vector3[] position = new Vector3[pointNum * pointNum];
        Vector2[] uv = new Vector2[pointNum * pointNum];
        Vector3[] normal = new Vector3[pointNum * pointNum];
        //int[] vertIndex = new int[(GridNum * GridNum - halfCenterGrid * halfCenterGrid / 4) * 6];
        //Vector3[] position = new Vector3[pointNum * pointNum - (halfCenterGrid - 1) * (halfCenterGrid - 1)];
        //Vector2[] uv = new Vector2[pointNum * pointNum - (halfCenterGrid - 1) * (halfCenterGrid - 1)];
        //Vector3[] normal = new Vector3[pointNum * pointNum - (halfCenterGrid - 1) * (halfCenterGrid - 1)];


        int count = 0;
        for (int i = 0; i < pointNum; i++)
        {
            for (int j = 0; j < pointNum; j++)
            {
                int index = i * pointNum + j;
                int indx = j - GridNum / 2;
                int indy = i - GridNum / 2;
                
                //if (!(indx > halfCenterGrid * -0.5 && indx < halfCenterGrid * 0.5 && indy > halfCenterGrid * -0.5 && indy < halfCenterGrid * 0.5))
                //{
                    position[index] = new Vector3(indx * Size , 0, indy * Size );
                    uv[index] = new Vector2(1.0f * j /  GridNum , 1.0f * i / GridNum);
                    normal[index] = new Vector3(0, 1, 0);
                    bool isCenter = ((indx == halfCenterGrid * -0.5 && indy >= halfCenterGrid * -0.5 && indy < halfCenterGrid * 0.5 -1) 
                                    || (indy == halfCenterGrid * -0.5 && indx >= halfCenterGrid * -0.5 && indx < halfCenterGrid * 0.5 - 1));
                    //bool isCenter = indx == halfCenterGrid * -0.5 && indy == halfCenterGrid * -0.5;
                    if (i != pointNum - 1 && j != pointNum - 1)
                    {
                        //int column = (indx >= halfCenterGrid  * 0.5 & (indy > halfCenterGrid * -0.5) & (indy < halfCenterGrid * 0.5)) ? 1:0;
                        //int row = Mathf.Min(Mathf.Max(0,indy - halfCenterGrid / (-2) -1),halfCenterGrid - 1);
                        int nextPoint = index + pointNum;// - (halfCenterGrid - 1) * (row + column);
                        vertIndex[count++] = index;
                        vertIndex[count++] = nextPoint;
                        vertIndex[count++] = nextPoint + 1;

                        vertIndex[count++] = index;
                        vertIndex[count++] = nextPoint + 1;
                        vertIndex[count++] = index + 1;
                    }
                //}

            }
        }
        mesh.vertices = position;
        mesh.SetIndices(vertIndex, MeshTopology.Triangles, 0);
        mesh.uv = uv;
        mesh.normals = normal;

        CreateSimplifyMesh(_Level + 1, GridNum, Size);
    }*/
    void Update()
    {
        
    }
}
