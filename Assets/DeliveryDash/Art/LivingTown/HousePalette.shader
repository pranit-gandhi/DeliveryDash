Shader "DeliveryDash/Replaceable house palette"
{
    Properties { _MainTex("Palette",2D)="white"{} _Color("Facade tint",Color)=(1,1,1,1) _RoofColor("Roof accent",Color)=(0.45,0.25,0.18,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex; fixed4 _Color, _RoofColor;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            fixed3 c=tex2D(_MainTex,IN.uv_MainTex).rgb;
            float green=step(c.r*1.12,c.g)*step(c.b*.98,c.g)*step(.15,c.g-c.r);
            o.Albedo=lerp(c*_Color.rgb,_RoofColor.rgb*(.65+c.g*.5),green);
            o.Smoothness=.12;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
