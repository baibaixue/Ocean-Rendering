/// 海水着色方法
Shader "Unlit/Ocean"
{
    Properties
    {
        _Color ("Color",Color) = (1,1,1,1)
        // 第一个波数据
        _Displace0 ("Displace0",2D) = "white" {}
        _Normal0 ("Normal0", 2D) = "white" {}
        // 白沫
        _ChoppyWavesRT0 ("Choopy Waves0",2D) = "white" {}
        // 第二个波数据
        _Displace1 ("Displace1",2D) = "white" {}
        _Normal1 ("Normal1", 2D) = "white" {}
        // 白沫
        _ChoppyWavesRT1 ("Choopy Waves1",2D) = "white" {}
        // 第三个波数据
        _Displace2 ("Displace2",2D) = "white" {}
        _Normal2 ("Normal2", 2D) = "white" {}
        // 白沫
        _ChoppyWavesRT2 ("Choopy Waves2",2D) = "white" {}

        // 高光系数
        _Specular ("Specular", Color) = (1,1,1,1)
        // 高光指数
        _Gloss ("Gloss", Range(8.0,256)) = 20
        // 菲涅尔系数
        _FresnelScale ("Fresnel Scale",Range(0,1)) = 0.5
        // 反射天空盒
        _Cubemap ("Reflection Cubemap", Cube) = "_Skybox"{}

        // 次表面散射(SSS) 
        // 次表面散射颜色
        _SSSColor ("SSS Color", Color) = (1,1,1,1)
        // 次表面散射强度
        _SSSScale ("SSS Scale", Range(0,10)) = 1
        // 次表面失真
        _SSSDistortion ("SSS Distortion", Range(0,1)) = 1
        _SSSPow ("SSS Pow", Range(0,1)) = 1
        // 白沫程度
        _foamScale ("Foam Scale", Range(0,1)) = 0
        // 白沫颜色
        _foamColor ("Foam Color", Color) = (1,1,1,1)
        // 白沫纹理
        _foamTexture ("Foam Texture", 2D) = "white" {}

        //_LOD_scale("LOD_scale", Range(-100,100)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile _ MID CLOSE
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos: TEXCOORD1;     // 世界空间顶点坐标
                //float3 debug : TEXCOORD2;
            };

            float4 _Color;

            sampler2D _Displace0;
            sampler2D _Normal0;
            sampler2D _ChoppyWavesRT0;
            float4 _Displace0_ST;

            sampler2D _Displace1;
            sampler2D _Normal1;
            sampler2D _ChoppyWavesRT1;

            sampler2D _Displace2;
            sampler2D _Normal2;
            sampler2D _ChoppyWavesRT2;

            float _Gloss;
            fixed4 _Specular;

            float _FresnelScale;
            samplerCUBE _Cubemap;

            fixed4 _SSSColor;
            float _SSSScale;
            float _SSSDistortion;
            float _SSSPow;

            float _foamScale;
            fixed4 _foamColor;
            sampler2D _foamTexture;

            float OceanLength0;
            float OceanLength1;
            float OceanLength2;



            float _LOD_scale;
            v2f vert (appdata v)
            {
                v2f o;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float4 worldUV = float4(o.worldPos.xz,0,0);
                // 根据视角距离对网格精细化
                //float3 viewVector = _WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex);
                //float viewDist = length(viewVector);
                o.uv = worldUV.xy / OceanLength2 + float2(0.5,0.5);
                o.uv = TRANSFORM_TEX(o.uv,_Displace0);  // 对偏移纹理进行采样

                float4 displace = 0;
                //float lod = min(_LOD_scale * _OceanLength / viewDist,1);
                displace += tex2Dlod(_Displace0,worldUV / OceanLength0) ;
                #if defined(MID) || defined(CLOSE)
                displace += tex2Dlod(_Displace1,worldUV / OceanLength1) ;
                #endif
                #if defined(CLOSE)
                displace += tex2Dlod(_Displace2,worldUV / OceanLength2) ;
                #endif
                //displace += tex2Dlod(_Displace,worldUV / _OceanLength) * lod;
                //o.debug = v.vertex.xyz + displace.xyz;
                // 顶点坐标修正
                v.vertex.xyz += mul(unity_WorldToObject,displace).xyz;
                
                // 新的顶点坐标和世界坐标
                o.vertex = UnityObjectToClipPos(v.vertex);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 normal = UnityObjectToWorldNormal(tex2D(_Normal2, i.uv).rgb);

                fixed3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));   
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                fixed3 reflectDir = reflect(-viewDir, normal);
                fixed3 halfDir = normalize(lightDir + viewDir);
                // 反射
                fixed3 reflection = texCUBE(_Cubemap, reflectDir).rgb;
                // 菲涅尔系数
                fixed fresnel = _FresnelScale + (1 - _FresnelScale) * pow(1 - dot(viewDir,normal),5);

                fixed3 SSSH = normalize(1.0f * lightDir + normal * _SSSDistortion);

                fixed Iback =_SSSScale * pow(saturate(dot(viewDir,SSSH)),_SSSPow) * i.worldPos.y * 1e-2;

                //fixed3 SSSColor = lerp(_Color,_SSSColor,saturate(Iback)); 

                fixed3 SSSColor = _Color + _SSSColor * saturate(Iback);


                // 环境光
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                // 漫反射
                fixed3 Diffuse = SSSColor.rgb * _LightColor0.rgb * saturate(dot(lightDir, normal));
                // 镜面反射
                fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0,dot(normal, halfDir)), _Gloss);
                // 菲涅尔反射
                fixed3 fresnelReflection = lerp(Diffuse, reflection, saturate(fresnel));
                // 白沫
                fixed3 ChoppyWavesUV = tex2D(_ChoppyWavesRT0,i.uv).rgb;
                fixed3 foamUV = tex2D(_foamTexture,i.uv).rgb;
                fixed ChoppyWaves = saturate((ChoppyWavesUV.r - _foamScale) * -1.0f);
                fixed3 foam = lerp(0,foamUV,ChoppyWaves) * _foamColor.rgb;
                
                //fixed4 col = float4(ambient + fresnelReflection + specular + foam,0);
                fixed4 col = float4(ambient + Diffuse + specular + foam,0);
                return col;
            }
            ENDCG
        }
    }
}
