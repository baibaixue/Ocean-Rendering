// 正弦波
// 采用高低振幅的正弦波曲线的序列组合来模拟水面起伏
// 特点是平滑，圆润，适合表达如池塘一样平静的水面
Shader "Unlit/Sinusoids Wave"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _BumpTex ("BumpTex", 2D) = "bump" {}
        _BumpScale ("Bump Scale",Float) = 1.0
        // 高光系数
        _Specular ("Specular", Color) = (1,1,1,1)
        // 高光指数
        _Gloss ("Gloss", Range(8.0,256)) = 20
        // 自由表面高度
        _Height ("Height" , Range(-1,1)) = 0
        // 两个波叠加，每个波的参数为(振幅，频率，波矢量)
        _Wave0 ("Wave0(Amplitude,Frequence,Wave(x,z))",Vector) = (1,1,1,1)
        _Wave1 ("Wave1(Amplitude,Frequence,Wave(x,z))",Vector) = (1,1,1,1)
        // 振幅
        //_Amplitude ("Amplitude", Float) = 1.0
        // 频率
        //_Frequency ("Frequency", Float) = 1.0
        // 波矢量的x方向分量
        //_WaveX ("WaveX", Float) = 1.0
        // 波矢量的z方向分量
        //_WaveZ ("WaveZ", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True"}

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
                float4 Tangent : TANGENT;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                //TBN矩阵
                float4 TtoW0 : TEXCOORD2;
                float4 TtoW1 : TEXCOORD3;
                float4 TtoW2 : TEXCOORD4;

            };
            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpTex;
            float4 _BumpTex_ST;
            float _BumpScale;
            float4 _Wave0;
            float4 _Wave1;
            float _Height;
            fixed4 _Specular;
            float _Gloss;
            v2f vert (appdata v)
            {
                v2f o;
                // 2Π
                fixed piX2 = 0.628; 
                float wave0 = piX2 * _Wave0.z * v.vertex.x + piX2 * _Wave0.w * v.vertex.z + _Wave0.y * _Time.y;
                float wave1 = piX2 * _Wave1.z * v.vertex.x + piX2 * _Wave1.w * v.vertex.z + _Wave1.y * _Time.y;
                v.vertex.y += _Height;
                v.vertex.y +=  _Wave0.x * sin(wave0);
                v.vertex.y +=  _Wave1.x * sin(wave1);
                //o.ModelPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv, _BumpTex);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                //o.worldNormal = UnityObjectToWorldNormal(v.normal);
                // 表面法线
                float3 worldNormal = UnityObjectToWorldNormal(
                                            float3(- (_Wave0.x * cos(wave0) * piX2 * _Wave0.z + _Wave1.x * cos(wave1) * piX2 * _Wave1.z),1,
                                            -(_Wave0.x * cos(wave0) * piX2 * _Wave0.w + _Wave1.x * cos(wave1) * piX2 * _Wave1.w)));
                // 切线
                float3 worldTangent = UnityObjectToWorldDir(float3(1,worldNormal.x,0));

                //float3 worldBinormal = UnityObjectToWorldDir(float3(0,worldNormal.z,1));  // 副切线
                float3 worldBinormal = cross(worldNormal,worldTangent); 
                o.TtoW0 = float4(worldTangent.x,worldBinormal.x,worldNormal.x,worldPos.x);
                o.TtoW1 = float4(worldTangent.y,worldBinormal.y,worldNormal.y,worldPos.y);
                o.TtoW2 = float4(worldTangent.z,worldBinormal.z,worldNormal.z,worldPos.z);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 worldPos = float3(i.TtoW0.w,i.TtoW1.w,i.TtoW2.w);
                fixed4 col = tex2D(_MainTex, i.uv);
                col.rgb *= _Color.rgb;
                fixed3 worldLightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                fixed3 halfDir = normalize(worldLightDir + viewDir);

                fixed3 bump = UnpackNormal(tex2D(_BumpTex,i.uv.zw));
                bump.xy *= _BumpScale; 
                bump.z = sqrt(1.0-saturate(dot(bump.xy,bump.xy)));
                bump = normalize(half3(dot(i.TtoW0.xyz,bump),dot(i.TtoW1.xyz,bump),dot(i.TtoW2.xyz,bump)));

                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * col.rgb;
                fixed3 diffuse = _LightColor0.rgb * col.rgb * saturate(dot(bump,worldLightDir));
                // sample the texture
                fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(saturate(dot(bump,halfDir)),_Gloss);

                return float4(ambient + diffuse + specular,1.0);
            }
            ENDCG
        }
    }
}
