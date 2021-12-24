// Gerstner波 尖峰波
// 表面的的每个点都绕着一个固定的锚点做圆周运动，整个水面的效果就像是水在波峰中聚集，在波谷中扩散
// 特点是波峰尖锐，波谷宽阔，适合模拟海洋等较为粗犷的水面
Shader "Unlit/Gerstner Wave"
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
        //_Height ("Height" , Range(-1,1)) = 0
        // 两个波叠加，每个波的参数为(振幅，频率，波矢量)
        //_Wave0 ("Wave0(Amplitude,Frequence,Wave(x,z))",Vector) = (1,1,1,1)
        //_Wave1 ("Wave1(Amplitude,Frequence,Wave(x,z))",Vector) = (1,1,1,1)
        // 四个波叠加
        // 振幅
        _Amplitude ("Amplitude", Vector) = (1,1,1,1)
        // 波长
        _WaveLength ("Wave Length",Vector) = (1,1,1,1)
        // 波速
        _Speed ("Speed", Vector) = (1,1,1,1)
        // 波矢量的x方向分量
        _WaveX ("WaveX", Vector) = (1,1,1,1)
        // 波矢量的z方向分量
        _WaveZ ("WaveZ", Vector) = (1,1,1,1)
        // 波峰的锐利程度
        _Sharpness("Sharpness",Range(0,1)) = 1
        
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
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 TtoW0 : TEXCOORD1;
                float4 TtoW1 : TEXCOORD2;
                float4 TtoW2 : TEXCOORD3;
                //float3 worldNormal :TEXCOORD4;
                //float3 worldTangent : TEXCOORD5;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            sampler2D _BumpTex;
            float4 _BumpTex_ST;
            float _BumpScale;
            fixed4 _Specular;
            float _Gloss;
            float4 _Amplitude;
            float4 _WaveLength;
            float4 _Speed;
            float4 _WaveX;
            float4 _WaveZ;
            fixed _Sharpness;
            float4 Multi(float4 v1,float4 v2)
            {
                return float4(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z , v1.w * v2.w);
            };
            float4 Division (float4 v1,float4 v2)
            {
                return float4(v1.x / v2.x, v1.y / v2.y, v1.z / v2.z , v1.w / v2.w);
            };
            v2f vert (appdata v)
            {
                v2f o;
                float piX2 = 6.28;
                v.vertex.xyz = mul(unity_ObjectToWorld,v.vertex.xyz);
                float4 wave = float4((piX2 / _WaveLength.x) * (v.vertex.x * _WaveX.x + v.vertex.z * _WaveZ.x + _Time.y * _Speed.x),
                                    (piX2 / _WaveLength.y) * (v.vertex.x * _WaveX.y + v.vertex.z * _WaveZ.y + _Time.y * _Speed.y),
                                    (piX2 / _WaveLength.z) * (v.vertex.x * _WaveX.z + v.vertex.z * _WaveZ.z + _Time.y * _Speed.z),
                                    (piX2 / _WaveLength.w) * (v.vertex.x * _WaveX.w + v.vertex.z * _WaveZ.w + _Time.y * _Speed.w));
                //float4 Qi = float4(_Sharpness / ( piX2 * _Amplitude.x ),
                //                    _Sharpness / ( piX2 * _Amplitude.y ),
                //                    _Sharpness / ( piX2 * _Amplitude.z ),
                //                    _Sharpness / ( piX2 * _Amplitude.w ));
                float3 offset;
                //offset.x = dot(float4(Qi.x * _Amplitude.x * _WaveX.x,Qi.y * _Amplitude.y * _WaveX.y, Qi.z * _Amplitude.z * _WaveX.z, Qi.w * _Amplitude.w * _WaveX.w),float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w)));//Qi * _Amplitude * _WaveX * float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w));
                //offset.z = dot(float4(Qi.x * _Amplitude.x * _WaveZ.x,Qi.y * _Amplitude.y * _WaveZ.y, Qi.z * _Amplitude.z * _WaveZ.z, Qi.w * _Amplitude.w * _WaveZ.w),float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w)));
                offset.x = dot(_Sharpness / piX2 * _WaveX,float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w)));
                offset.z = dot(_Sharpness / piX2 * _WaveZ,float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w)));
                offset.y = dot(_Amplitude,float4(sin(wave.x),sin(wave.y),sin(wave.z),sin(wave.w))) - v.vertex.y;
                //offset.y = _Amplitude.x * sin(wave.x) + _Amplitude.y * sin(wave.y) + _Amplitude.z * sin(wave.z) + _Amplitude.w * sin(wave.w) - v.vertex.y;
                
                float3 worldTangent = float3(1 + dot(_Sharpness  * Division(Multi(_WaveX,_WaveX),_WaveLength),float4(-sin(wave.x),-sin(wave.y),-sin(wave.z),-sin(wave.w))),
                                        dot(piX2 * Division(Multi(_WaveX,_Amplitude),_WaveLength),float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w))),
                                        dot(_Sharpness  * Division(Multi(_WaveX,_WaveZ),_WaveLength),float4(-sin(wave.x),-sin(wave.y),-sin(wave.z),-sin(wave.w))));
                float3 worldBinormal = float3(dot(_Sharpness  * Division(Multi(_WaveX,_WaveZ),_WaveLength),float4(-sin(wave.x),-sin(wave.y),-sin(wave.z),-sin(wave.w))),
                                       dot(piX2 * Division(Multi(_WaveZ,_Amplitude),_WaveLength),float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w))),
                                       1 + dot(_Sharpness  * Division(Multi(_WaveZ,_WaveZ),_WaveLength),float4(-sin(wave.x),-sin(wave.y),-sin(wave.z),-sin(wave.w))));
                float3 worldNormal = cross(worldBinormal,worldTangent);
                v.vertex.xyz = mul(unity_WorldToObject,v.vertex.xyz + offset);


                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv, _BumpTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.TtoW0 = float4(worldTangent.x,worldBinormal.x,worldNormal.x,worldPos.x);
                o.TtoW1 = float4(worldTangent.y,worldBinormal.y,worldNormal.y,worldPos.y);
                o.TtoW2 = float4(worldTangent.z,worldBinormal.z,worldNormal.z,worldPos.z);
                //o.worldNormal = worldNormal;
                //o.worldTangent = worldTangent;
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
                //return float4(i.worldNormal,1.0);
            }
            ENDCG
        }
    }
}
