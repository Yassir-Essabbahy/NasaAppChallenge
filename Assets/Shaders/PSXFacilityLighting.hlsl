#ifndef PSX_FACILITY_LIGHTING_INCLUDED
#define PSX_FACILITY_LIGHTING_INCLUDED

float _PSX_FacilityDarkness;
float4 _PSX_FacilityLightPos[16];
float4 _PSX_FacilityLightColor[16];

float ComputeBoxMask(float3 p, float3 bMin, float3 bMax)
{
    const float edge = 0.045;
    float3 low  = smoothstep(bMin - edge, bMin + edge, p);
    float3 high = 1.0 - smoothstep(bMax - edge, bMax + edge, p);
    return low.x * low.y * low.z * high.x * high.y * high.z;
}

float ComputeFacilityIndoorMask(float3 p)
{
    if (_PSX_FacilityDarkness < 0.5)
        return 0.0;

    float m = 0.0;
    // Command Module interior (keeps PF_Mars_Exterior at Z > 3 and outer wall faces sunlit)
    m = max(m, ComputeBoxMask(p, float3(-3.04, -0.25, -2.20), float3(3.04, 3.04, 2.83)));
    // Medbay + Mod7 connecting corridor
    m = max(m, ComputeBoxMask(p, float3(-10.72, -0.25, -0.32), float3(-3.02, 3.42, 2.84)));
    // Hub Corridor north-south spine + airlock bridge (tight X/Y so outer shell stays at outdoor ambient)
    m = max(m, ComputeBoxMask(p, float3(-1.86, -0.25, -14.22), float3(1.86, 3.42, -2.10)));
    // Hub Corridor west alcove (Mod2)
    m = max(m, ComputeBoxMask(p, float3(-5.92, -0.25, -6.10), float3(-1.75, 3.45, -2.20)));
    // Greenhouse wing (Mod4 + Room_Greenhouse)
    m = max(m, ComputeBoxMask(p, float3(-10.78, -0.25, -10.39), float3(-1.75, 3.45, -5.88)));
    // Storage wing (Mod5 + Room_Storage)
    m = max(m, ComputeBoxMask(p, float3(1.75, -0.25, -10.39), float3(10.78, 3.45, -5.82)));
    // Airlock chamber (outer hatch & exterior wall at Z <= -17.34 remain sunlit)
    m = max(m, ComputeBoxMask(p, float3(-1.53, -0.25, -17.34), float3(1.53, 3.04, -13.95)));
    return saturate(m);
}

half3 EvaluateFacilitySurfaceLit(float3 posWS, float3 normalWS, half3 tintColor)
{
    float indoor = ComputeFacilityIndoorMask(posWS);

    half3 outdoorAmbient = max(half3(unity_AmbientSky.rgb), half3(0.15h, 0.13h, 0.14h));
    half3 indoorAmbient  = half3(0.015h, 0.017h, 0.023h);
    half3 lit = lerp(outdoorAmbient, indoorAmbient, half(indoor));

    Light mainLight = GetMainLight();
    half rawNdotL = dot(normalWS, mainLight.direction);
    half mainNdotL = saturate(rawNdotL);
    half wrapFill = saturate(rawNdotL * 0.5h + 0.5h);
    lit += half3(mainLight.color) * (mainNdotL * 0.50h + wrapFill * 0.12h) * (1.0h - half(indoor));

    [unroll]
    for (int i = 0; i < 16; i++)
    {
        float4 lp = _PSX_FacilityLightPos[i];
        float4 lc = _PSX_FacilityLightColor[i];
        float activeMask = step(0.00001, lp.w);
        float3 toL = lp.xyz - posWS;
        float distSq = dot(toL, toL);
        float rangeAtt = saturate(1.0 - distSq * lp.w);
        float atten = ((rangeAtt * rangeAtt) / (1.0 + distSq * 0.58)) * activeMask;
        float3 lDir = normalize(toL + float3(0.0, 0.0001, 0.0));
        half ndl = saturate(dot(normalWS, lDir)) * 0.88h + 0.12h;
        lit += half3(lc.rgb) * (ndl * half(atten) * half(lc.a));
    }

    return lit * tintColor;
}

#endif
