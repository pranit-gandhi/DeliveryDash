Shader "DeliveryDash/Season sky" {
 Properties {
 _Top("Upper sky",Color)=(.3,.6,.8,1)
 _Horizon("Horizon",Color)=(.9,.8,.6,1)
 _SunColor("Sun",Color)=(1,.9,.6,1)
 _SunDirection("Sun direction",Vector)=(0,.3,1,0)
 }
 SubShader {
 Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
 Cull Off ZWrite Off
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct input { float4 vertex:POSITION; };
 struct output { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
 float4 _Top,_Horizon,_SunColor,_SunDirection;
 output vert(input v){output o;o.position=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
 fixed4 frag(output i):SV_Target {
 float3 d=normalize(i.direction);
 float3 color=lerp(_Horizon.rgb,_Top.rgb,smoothstep(-.08,.42,d.y));
 float sun=dot(d,normalize(_SunDirection.xyz));
 color+=_SunColor.rgb*pow(saturate(sun),90)*.11;
 color=lerp(color,_SunColor.rgb,smoothstep(.9979,.9995,sun));
 return fixed4(color,1);
 }
 ENDCG
 }
 }
}
