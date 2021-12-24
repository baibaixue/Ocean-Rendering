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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
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
            v2f vert (appdata v)
            {
                v2f o;
                float piX2 = 6.28;
                float4 wave = float4((piX2 / _WaveLength.x) * (v.vertex.x * _WaveX.x + v.vertex.z * _WaveZ.x + _Time.y * _Speed.x),
                                    (piX2 / _WaveLength.y) * (v.vertex.x * _WaveX.y + v.vertex.z * _WaveZ.y + _Time.y * _Speed.y),
                                    (piX2 / _WaveLength.z) * (v.vertex.x * _WaveX.z + v.vertex.z * _WaveZ.z + _Time.y * _Speed.z),
                                    (piX2 / _WaveLength.w) * (v.vertex.x * _WaveX.w + v.vertex.z * _WaveZ.w + _Time.y * _Speed.w));
                float4 Qi = float4(_Sharpness / ( piX2 * _Amplitude.x ),
                                    _Sharpness / ( piX2 * _Amplitude.y ),
                                    _Sharpness / ( piX2 * _Amplitude.z ),
                                    _Sharpness / ( piX2 * _Amplitude.w ));
                float4 offset;
                offset.w = 0;
                offset.x = Qi * _Amplitude * _WaveX * float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w));
                offset.z = Qi * _Amplitude * _WaveZ , float4(cos(wave.x),cos(wave.y),cos(wave.z),cos(wave.w));
                offset.y = dot(_Amplitude,float4(sin(wave.x),sin(wave.y),sin(wave.z),sin(wave.z))) - v.vertex.y;
                o.vertex = UnityObjectToClipPos(v.vertex + offset);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
