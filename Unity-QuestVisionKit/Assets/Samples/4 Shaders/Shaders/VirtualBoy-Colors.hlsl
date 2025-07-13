//UNITY_SHADER_NO_UPGRADE
#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED

#define H 255

void VirtualBoy_Color_float(float3 Col, out float3 Out)
{
    if (Col.r > 0.8)
    {
        Col = half3(255. / H, 0. / H, 0. / H);
    }
    else if (Col.r <= 0.80 && Col.r > 0.60)
    {
        Col = half3(183. / H, 0. / H, 13. / H);
    }
    else if (Col.r <= 0.60 && Col.r > 0.40)
    {
        Col = half3(135. / H, 0. / H, 0. / H);
    }
    else if (Col.r <= 0.40 && Col.r > 0.20)
    {
        Col = half3(99. / H, 0. / H, 0. / H);
    }
    else
    {
        Col = half3(48. / H, 0. / H, 0. / H);
    }
    
    Out = Col;
}
#endif //MYHLSLINCLUDE_INCLUDED