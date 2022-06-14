
void InjectSetup_float(float3 A, out float3 Out)
{
    Out = A;
}

float4x4 inverse(float4x4 input)
{
#define minor(a,b,c) determinant(float3x3(input.a, input.b, input.c))
    //determinant(float3x3(input._22_23_23, input._32_33_34, input._42_43_44))

    float4x4 cofactors = float4x4(
        minor(_22_23_24, _32_33_34, _42_43_44),
        -minor(_21_23_24, _31_33_34, _41_43_44),
        minor(_21_22_24, _31_32_34, _41_42_44),
        -minor(_21_22_23, _31_32_33, _41_42_43),

        -minor(_12_13_14, _32_33_34, _42_43_44),
        minor(_11_13_14, _31_33_34, _41_43_44),
        -minor(_11_12_14, _31_32_34, _41_42_44),
        minor(_11_12_13, _31_32_33, _41_42_43),

        minor(_12_13_14, _22_23_24, _42_43_44),
        -minor(_11_13_14, _21_23_24, _41_43_44),
        minor(_11_12_14, _21_22_24, _41_42_44),
        -minor(_11_12_13, _21_22_23, _41_42_43),

        -minor(_12_13_14, _22_23_24, _32_33_34),
        minor(_11_13_14, _21_23_24, _31_33_34),
        -minor(_11_12_14, _21_22_24, _31_32_34),
        minor(_11_12_13, _21_22_23, _31_32_33)
        );
#undef minor
    return transpose(cofactors) / determinant(input);
}

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

//struct IndirectShaderData
//{
//	float4x4 PositionMatrix;
//	float4x4 InversePositionMatrix;
//	float4 ControlData;
//};

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_D3D11) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PSSL) || defined(SHADER_API_XBOXONE)
uniform StructuredBuffer<float3> Particles;
#endif

#endif

void setupVSPro()
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

#ifdef unity_ObjectToWorld
#undef unity_ObjectToWorld
#endif

#ifdef unity_WorldToObject
#undef unity_WorldToObject
#endif
    float4x4 mat = {
                             1.f, 0.f, 0.f, Particles[unity_InstanceID].x,
                             0.f, 1.f, 0.f, Particles[unity_InstanceID].y,
                             0.f, 0.f, 1.f, Particles[unity_InstanceID].z,
                             0.f, 0.f, 1.f, 1.f
    };

    unity_ObjectToWorld = mat;
    unity_WorldToObject = inverse(mat);
#endif
}