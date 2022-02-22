using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanPanel : MonoBehaviour
{

    // 网格生成
    private Mesh mesh;
    private MeshFilter filter;
    private MeshRenderer render;
    public int meshSize = 100;      // 网格大小，每行/列的顶点数
    public int meshLength = 512;    // 整个网格的长宽（2的指数幂）
    private int[] vertIndex;        // 网格三角形索引
    private Vector3[] position;     // 网格顶点位置
    private Vector2[] uv;           // 顶点uv坐标
    private Vector3[] normal;       // 顶点法向量
    // 材质
    public Material oceanMaterial;
    // 海洋参数
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
        CreateMesh();
        //render.material = oceanMaterial;
    }

    // Update is called once per frame
    void Update()
    {

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
}
