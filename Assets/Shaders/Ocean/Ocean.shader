/// 海水着色方法
Shader "Unlit/Ocean"
{
    Properties
    {
        _Color ("Color",Color) = (1,1,1,1)
        _Displace ("Displace",2D) = "white" {}
        _Normal ("Normal", 2D) = "white" {}
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
            };

            float4 _Color;

            sampler2D _Displace;
            sampler2D _Normal;
            float4 _Displace_ST;

            float _Gloss;
            fixed4 _Specular;

            float _FresnelScale;
            samplerCUBE _Cubemap;

            fixed4 _SSSColor;
            float _SSSScale;
            float _SSSDistortion;
            float _SSSPow;
            v2f vert (appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv,_Displace);  // 对偏移纹理进行采样
                float4 displace = tex2Dlod(_Displace,float4(o.uv,0,0));
                v.vertex += float4(displace.xyz,0);
                o.vertex = UnityObjectToClipPos(v.vertex);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 normal = UnityObjectToWorldNormal(tex2D(_Normal, i.uv).rgb);

                fixed3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));   
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                fixed3 reflectDir = reflect(-viewDir, normal);
                fixed3 halfDir = normalize(lightDir + viewDir);
                // 反射
                fixed3 reflection = texCUBE(_Cubemap, reflectDir).rgb;
                // 菲涅尔系数
                fixed fresnel = _FresnelScale + (1 - _FresnelScale) * pow(1 - dot(viewDir,normal),5);
                // 光照衰减
                UNITY_LIGHT_ATTENUATION(atten,i,i.worldPos);

                fixed3 SSSH = normalize(1.0f * lightDir + normal * _SSSDistortion);

                fixed Iback = atten * _SSSScale * pow(saturate(dot(viewDir,SSSH)),_SSSPow) * i.worldPos.y * 1e-2;

                //fixed3 SSSColor = lerp(_Color,_SSSColor,saturate(Iback)); 

                fixed3 SSSColor = _Color + _SSSColor * saturate(Iback);


                // 环境光
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                // 漫反射
                fixed3 Diffuse = SSSColor.rgb * _LightColor0.rgb * saturate(dot(lightDir, normal));
                // 镜面反射
                fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0,dot(normal, halfDir)), _Gloss);
                // 菲涅尔反射
                fixed3 fresnelReflection = lerp(Diffuse, reflection, saturate(fresnel)) * atten;
                // sample the texture
                fixed4 col = float4(ambient + fresnelReflection + specular,0);

                return col;
            }
            ENDCG
        }
    }
}
