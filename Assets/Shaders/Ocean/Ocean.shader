/// 海水着色方法
Shader "Unlit/Ocean"
{

    Properties
    {
        // 海洋颜色
        _Color("Color", Color) = (1,1,1,1)
        // 次表面散射颜色
        _SSSColor("SSS Color", Color) = (1,1,1,1)
        // 次表面散射强度
        _SSSStrength("SSSStrength", Range(0,1)) = 1
        _SSSScale("SSS Scale", Range(0.1,50)) = 4.0
        _SSSPow ("SSS Pow", Range(0,10)) = 5
        // 次表面散射的基础范围
        _SSSBase("SSS Base", Range(-5,1)) = 0
        // 次表面失真
        _SSSDistortion ("SSS Distortion", Range(0,1)) = 1
        // 网格精细度
        _LOD_scale("LOD_scale", Range(0,10)) = 0
        // 粗糙程度
        _Roughness("Roughness", Range(0,1)) = 0
        // 菲涅尔系数
        _FresnelScale ("Fresnel Scale",Range(0,1)) = 0.5
        // 海面白沫颜色
        _FoamColor("Foam Color", Color) = (1,1,1,1)
        // 海面白沫贴图
        _FoamTexture("Foam Texture", 2D) = "grey" {}
        // 出现白沫的范围,分别对应，近，中，远
        _FoamRangeLOD0("Foam Range LOD0", Range(0,5)) = 1
        _FoamRangeLOD1("Foam Range LOD1", Range(0,5)) = 1
        _FoamRangeLOD2("Foam Range LOD2", Range(0,5)) = 1
        // 海面白沫程度
        _FoamScale("Foam Scale", Range(0,10)) = 1

        // 第一个波数据， 顶点位移、 偏导数(法向量数据)、 尖浪信息
        _Displace0("Displace0", 2D) = "black" {}
        _Derivatives0("Derivatives0", 2D) = "black" {}
        _ChoppyWavesRT0("ChoppyWavesRT0", 2D) = "white" {}
        // 第二个波数据
        _Displace1("Displace1", 2D) = "black" {}
        _Derivatives1("Derivatives1", 2D) = "black" {}
        _ChoppyWavesRT1("ChoppyWavesRT1", 2D) = "white" {}
        // 第三个波数据
        _Displace2("Displace2", 2D) = "black" {}
        _Derivatives2("Derivatives2", 2D) = "black" {}
        _ChoppyWavesRT2("ChoppyWavesRT2", 2D) = "white" {}


    }
        SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma multi_compile _ MID CLOSE
        // 指定表面着色器的表面函数和光照模型
        // 表面函数 ： surf
        // 光照模型 ： Standard 基于物理的标准光照模型
        // fullforwardshadows : 在正向渲染路径中支持所有的阴影类型，在前向渲染路径中支持所有光源类型的阴影
        // addshadow : 添加阴影投射器和集合通道
        // vertex:vert : 顶点修改函数->vert
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 4.0

        // 表面属性的数据来源，作为表面函数的输入结构体
        struct Input
        {
            float2 worldUV;     // 世界空间的UV坐标
            float4 lodScales;   // 网格缩放程度
            float3 viewVector;  // 视角方向
            float3 worldNormal; // 世界空间的法线方向
            float4 screenPos;   // 屏幕空间坐标
            INTERNAL_DATA
        };

        sampler2D _Displace0;
        sampler2D _Derivatives0;
        sampler2D _ChoppyWavesRT0;

        sampler2D _Displace1;
        sampler2D _Derivatives1;
        sampler2D _ChoppyWavesRT1;

        sampler2D _Displace2;
        sampler2D _Derivatives2;
        sampler2D _ChoppyWavesRT2;

        float OceanLength0;
        float OceanLength1;
        float OceanLength2;
        float _LOD_scale;
        float _SSSBase;
        float _SSSScale;
        // 顶点修改函数
        void vert(inout appdata_full v, out Input o)
        {
            // 初始化表面输出属性数据
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // 世界坐标
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
            // uv坐标为世界坐标的xz轴坐标
            float4 worldUV = float4(worldPos.xz, 0, 0);
            o.worldUV = worldUV.xy;
            // 视角方向
            o.viewVector = _WorldSpaceCameraPos.xyz - worldPos;
            // 视角距离
            float viewDist = length(o.viewVector);
            
            float lod_c0 = min(_LOD_scale * OceanLength0 / viewDist, 1);
            float lod_c1 = min(_LOD_scale * OceanLength1 / viewDist, 1);
            float lod_c2 = min(_LOD_scale * OceanLength2 / viewDist, 1);
            
            // 偏移坐标
            float3 displacement = 0;
            // 最大的海浪高度
            float largeWavesBias = 0;

            
            displacement += tex2Dlod(_Displace0, worldUV / OceanLength0) * lod_c0;
            // 记录一下波峰高度用于次表面散射
            largeWavesBias = displacement.y;
            #if defined(MID) || defined(CLOSE)
            displacement += tex2Dlod(_Displace1, worldUV / OceanLength1) * lod_c1;
            #endif
            #if defined(CLOSE)
            displacement += tex2Dlod(_Displace2, worldUV / OceanLength2) * lod_c2;
            #endif
            // 顶点修正
            v.vertex.xyz += mul(unity_WorldToObject,displacement);

            o.lodScales = float4(lod_c0, lod_c1, lod_c2, max(displacement.y - largeWavesBias * 0.5f - _SSSBase, 0) / _SSSScale);

        }

        fixed4 _Color, _FoamColor, _SSSColor;
        float _SSSStrength,_SSSDistortion, _SSSPow;
        float _Roughness,_FresnelScale;
        float _FoamBiasLOD0, _FoamRangeLOD1, _FoamRangeLOD2, _FoamScale;
        sampler2D _CameraDepthTexture;
        sampler2D _FoamTexture;

        // 根据TBN矩阵得到表面法线
        float3 WorldToTangentNormalVector(Input IN, float3 normal) {
            float3 t2w0 = WorldNormalVector(IN, float3(1, 0, 0));
            float3 t2w1 = WorldNormalVector(IN, float3(0, 1, 0));
            float3 t2w2 = WorldNormalVector(IN, float3(0, 0, 1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, normal));
        }

        // 表面着色
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
           
            // 根据导数数据得到法线
            float4 derivatives = tex2D(_Derivatives0, IN.worldUV / OceanLength0) * IN.lodScales.x;
            #if defined(MID) || defined(CLOSE)
            derivatives += tex2D(_Derivatives1, IN.worldUV / OceanLength1) * IN.lodScales.y;
            #endif
            #if defined(CLOSE)
            derivatives += tex2D(_Derivatives2, IN.worldUV / OceanLength2) * IN.lodScales.z;
            #endif
           
            // 世界空间法线
            float3 worldNormal = normalize(float3(-derivatives.x , 1 , -derivatives.y));
            // 由TBN矩阵得到表面法线, 表面着色器自动计算漫反射
            o.Normal = WorldToTangentNormalVector(IN, worldNormal);
            // 尖浪处的白沫范围计算
            #if defined(CLOSE)
            float jacobian = tex2D(_ChoppyWavesRT0, IN.worldUV / OceanLength0).x
                + tex2D(_ChoppyWavesRT1, IN.worldUV / OceanLength1).x
                + tex2D(_ChoppyWavesRT2, IN.worldUV / OceanLength2).x;
            jacobian = min(1, max(0, (-jacobian + _FoamRangeLOD2) * _FoamScale));
            #elif defined(MID)
            float jacobian = tex2D(_ChoppyWavesRT0, IN.worldUV / OceanLength0).x
                + tex2D(_ChoppyWavesRT1, IN.worldUV / OceanLength1).x;
            jacobian = min(1, max(0, (-jacobian + _FoamRangeLOD1) * _FoamScale));
            #else
            float jacobian = tex2D(_ChoppyWavesRT0, IN.worldUV / OceanLength0).x;
            jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD0) * _FoamScale));
            #endif
            // 白沫
            float foamUV = tex2D(_FoamTexture, IN.worldUV).r;
            float3 foam = foamUV * jacobian * _FoamColor.rgb;

            // 用于高光的粗糙度
            o.Smoothness = _Roughness;
           
            fixed3 lightDir = normalize(_WorldSpaceLightPos0);
            float3 viewDir = normalize(IN.viewVector);  // 视角方向
            float3 SSSH = normalize(-worldNormal * _SSSDistortion + lightDir);  
            float ViewDotH = pow(saturate(dot(viewDir, -SSSH)), _SSSPow) * _SSSStrength * IN.lodScales.w;

            fixed3 color = _Color + _SSSColor.rgb * saturate(ViewDotH);
            // 菲涅尔反射
            float fresnel = dot(worldNormal, viewDir);
            fresnel = saturate(1 - fresnel);
            fresnel = (1 - _FresnelScale) * pow(fresnel,5.0f);
            // 菲涅尔系数
            o.Emission = color * (1 - fresnel)  + foam;
            
        }
        ENDCG
    }
        FallBack "Diffuse"
        
}
