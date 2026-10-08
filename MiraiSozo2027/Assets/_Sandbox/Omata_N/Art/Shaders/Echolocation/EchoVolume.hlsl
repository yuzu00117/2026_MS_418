// 元の球・扇形の伝播に、発生地点からの影判定を組み合わせる。
#ifndef ECHO_VOLUME_INCLUDED
#define ECHO_VOLUME_INCLUDED
int _EchoPulseCount;
float4 _EchoOrigins[64];
float4 _EchoForwards[64];
float4 _EchoVolumes[64]; // 外側半径、消去半径、消去中、展開中
float4 _EchoTint;
float _EchoOcclusion;
int _EchoShadowsEnabled;
float _EchoShadowBias;
float4 _EchoShadowSlots[64];
float4x4 _EchoShadowMatrices[384];
Texture2DArray<float> _EchoShadowMaps;

bool EchoSourceVisible(float3 p,int wave)
{
    if(_EchoShadowsEnabled==0)return true;
    float3 direction=p-_EchoOrigins[wave].xyz;float d=length(direction);
    if(d<=_EchoShadowBias)return true;
    float3 a=abs(direction);int face;
    if(a.x>=a.y&&a.x>=a.z)face=direction.x>=0?0:1;
    else if(a.y>=a.z)face=direction.y>=0?2:3;
    else face=direction.z>=0?4:5;
    int slice=(int)_EchoShadowSlots[wave].x*6+face;
    float4 clipPosition=mul(_EchoShadowMatrices[slice],float4(p,1));
    float2 uv=clipPosition.xy/clipPosition.w*.5+.5;
    #if UNITY_UV_STARTS_AT_TOP
    uv.y=1-uv.y;
    #endif
    uint width,height,layers;_EchoShadowMaps.GetDimensions(width,height,layers);
    int2 pixel=clamp((int2)(uv*float2(width,height)),int2(0,0),int2(width-1,height-1));
    float nearest=_EchoShadowMaps.Load(int4(pixel,slice,0));
    // 距離マップの画素中心と現在の表面位置は少しずれる。
    // 表面の接平面と「画素中心の光線」の交点で比較し、床を斜めに見た際の縞状の自己遮蔽を防ぐ。
    float2 sampleUV=(float2(pixel)+.5)/float2(width,height);
    #if UNITY_UV_STARTS_AT_TOP
    sampleUV.y=1-sampleUV.y;
    #endif
    float2 xy=sampleUV*2-1;float3 ray;
    if(face==0)ray=float3(1,xy.y,-xy.x);
    else if(face==1)ray=float3(-1,xy.y,xy.x);
    else if(face==2)ray=float3(-xy.x,1,xy.y);
    else if(face==3)ray=float3(-xy.x,-1,-xy.y);
    else if(face==4)ray=float3(xy.x,xy.y,1);
    else ray=float3(-xy.x,xy.y,-1);
    ray=normalize(ray);
    float3 normal=cross(ddx(p),ddy(p));float denominator=dot(normal,ray);
    float expected=d;
    if(abs(denominator)>1e-10)
    {float planeDistance=dot(normal,direction)/denominator;if(planeDistance>0)expected=planeDistance;}
    return expected<=nearest+_EchoShadowBias;
}
bool EchoContainsAt(float3 p,int index)
{
    float4 origin=_EchoOrigins[index],forward=_EchoForwards[index],volume=_EchoVolumes[index];
    float3 v=p-origin.xyz;float d=length(v);
    if(origin.w<=0||d>volume.x||(volume.z>.5&&d<=volume.y))return false;
    if(origin.w<360&&d>.00001&&dot(v/d,forward.xyz)<forward.w)return false;
    return EchoSourceVisible(p,index);
}
float EchoCoverage(float3 p)
{
    // どれかの生存波から届けば表示する。重なった波の色は累積させない。
    [loop]for(int i=0;i<_EchoPulseCount;i++)if(EchoContainsAt(p,i))return 1;
    return 0;
}
#endif
