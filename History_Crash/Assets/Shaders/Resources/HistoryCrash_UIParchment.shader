// 메인메뉴 도감(스토리 영상·조작법·튜토리얼 바탕)의 어두운 갈색 종이를 밝은 양피지로 보정하는 UI 셰이더.
// 밝기 곡선 1-(1-L)^p: 종이(중간 밝기)는 크게 밝아지고 잉크 글자(어두운 부분)는 덜 밝아져 대비가 커진다.
// _Strength 0 = 원래 그대로, 1 = 보정 끝까지 (영상이 바뀔 때 서서히 올리는 용도)
Shader "HistoryCrash/UIParchment"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Strength ("Strength", Range(0, 1)) = 1
        _Power ("Curve Power", Range(1, 4)) = 2.6
        _Paper ("Paper Tint", Color) = (0.93, 0.85, 0.68, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; float _Strength; float _Power; fixed4 _Paper;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float3 g = c.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                g = LinearToGammaSpace(g);   // 보정은 눈에 보이는 밝기(감마) 기준
                #endif
                float L = dot(g, float3(0.299, 0.587, 0.114));
                float L2 = 1 - pow(saturate(1 - L), _Power);
                float3 o = g * (L > 1e-4 ? L2 / L : 1);
                float l = dot(o, float3(0.299, 0.587, 0.114));
                o = lerp(o, l.xxx, 0.2);                       // 채도를 조금 줄이고
                o = lerp(o, _Paper.rgb * (l / 0.86), 0.3);     // 양피지 색으로 살짝 당김
                o = lerp(g, saturate(o), _Strength);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                o = GammaToLinearSpace(o);
                #endif
                return fixed4(o, c.a) * i.color;
            }
            ENDCG
        }
    }
}
