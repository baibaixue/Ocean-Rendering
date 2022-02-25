Shader "Unlit/FFTOcean"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Displace ("Displace", 2D) = "white" {}
        _Normal ("Normal",2D) = "white"{}
        // 高光系数
        _Specular ("Specular", Color) = (1,1,1,1)
        // 高光指数
        _Gloss ("Gloss", Range(8.0,256)) = 20

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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _Displace;
            sampler2D _Normal;
            float4 _Displace_ST;
            fixed4 _Specular;
            float _Gloss;

            v2f vert (appdata v)
            {
                v2f o;
                float2 displaceUV = TRANSFORM_TEX(v.uv, _Displace);
                float4 displace = tex2Dlod(_Displace, float4(displaceUV, 0, 0));
                v.vertex += float4(displace.xyz, 0);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _Displace);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 normal = UnityObjectToWorldNormal(tex2D(_Normal, i.uv).rgb);
                fixed3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                fixed3 reflectDir = reflect(-viewDir, normal);  
                
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                fixed3 Diffuse = _LightColor0.rgb * saturate(dot(lightDir, normal));
                fixed3 halfDir = normalize(lightDir + viewDir);
                fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0, dot(normal, halfDir)), _Gloss);
                
                
                fixed3 col = ambient + Diffuse + specular ;
                
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
