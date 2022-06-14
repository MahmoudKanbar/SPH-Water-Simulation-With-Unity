Shader "Custom/InstancedIndirectColor" {
    SubShader {
        Tags { "RenderType" = "Opaque" }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
            };

            struct v2f {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
            }; 

            StructuredBuffer<float3> Particles;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4x4 mat = {
                                    40.f, 0.f, 0.f, Particles[instanceID].x,
                                    0.f, 40.f, 0.f, Particles[instanceID].y,
                                    0.f, 0.f, 40.f, Particles[instanceID].z,
                                    0.f, 0.f, 1.f, 1.f
                                };
                // mat._11_22_33_44 = float4(1, 1, 1, 1);
                // mat._14_24_34_44 = float4(_Particles[instanceID].xyz, 1);

                float4 pos = mul(mat, i.vertex);
                o.vertex = UnityObjectToClipPos(pos);
                o.color = float4(0, 1, 1, 1);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                return i.color;
            }

            ENDCG
        }
    }
}