#define PI 3.1415926
static float RCP_PI = rcp(PI);

float GetSH00(float theta, float phi) {
    return 0.5 * sqrt(RCP_PI);
}

float GetSH10(float theta, float phi) {
    return 0.5 * sqrt(3 * RCP_PI) * cos(theta);
}

float GetSH1p1(float theta, float phi) {
    return 0.5 * sqrt(3 * RCP_PI) * sin(theta) * cos(phi);
}

float GetSH1n1(float theta, float phi) {
    return 0.5 * sqrt(3 * RCP_PI) * sin(theta) * sin(phi);
}

float GetSH20(float theta, float phi) {
    float c = cos(theta);
    return 0.25 * sqrt(5 * RCP_PI) * (3 * c * c - 1);
}

float GetSH2p1(float theta, float phi) {
    return 0.5 * sqrt(15 * RCP_PI) * sin(theta) * cos(theta) * cos(phi);
}

float GetSH2n1(float theta, float phi) {
    return 0.5 * sqrt(15 * RCP_PI) * sin(theta) * cos(theta) * sin(phi);
}

float GetSH2p2(float theta, float phi) {
    float s = sin(theta);
    return 0.25 * sqrt(15 * RCP_PI) * s * s * cos(2 * phi);
}

float GetSH2n2(float theta, float phi) {
    float s = sin(theta);
    return 0.25 * sqrt(15 * RCP_PI) * s * s * sin(2 * phi);
}

float3 UnitDirFromThetaPhi(float theta, float phi) {
    float3 result;
    float s_theta, c_theta, s_phi, c_phi;
    sincos(theta, s_theta, c_theta);
    sincos(phi, s_phi, c_phi);
    result.y = c_theta;
    result.x = s_theta * c_phi;
    result.z = s_theta * s_phi;
    return result;
}

//==============直角坐标系下的3阶球谐函数============//

//l = 0,m = 0
float GetY00(float3 xyz) {
    return 0.5 * sqrt(RCP_PI);
}

//l = 1,m = 0
float GetY10(float3 p) {
    return 0.5 * sqrt(3 * RCP_PI) * p.z;
}

//l = 1,m = 1
float GetY1p1(float3 p) {
    return 0.5 * sqrt(3 * RCP_PI) * p.x;
}

//l = 1,m = -1
float GetY1n1(float3 p) {
    return 0.5 * sqrt(3 * RCP_PI) * p.y;
}

//l = 2, m = 0
float GetY20(float3 p) {
    return 0.25 * sqrt(5 * RCP_PI) * (2 * p.z * p.z - p.x * p.x - p.y * p.y);
}

//l = 2, m = 1
float GetY2p1(float3 p) {
    return 0.5 * sqrt(15 * RCP_PI) * p.z * p.x;
}

//l = 2, m = -1
float GetY2n1(float3 p) {
    return 0.5 * sqrt(15 * RCP_PI) * p.z * p.y;
}

//l = 2, m = 2
float GetY2p2(float3 p) {
    return 0.25 * sqrt(15 * RCP_PI) * (p.x * p.x - p.y * p.y);
}

//l = 2, m = -2
float GetY2n2(float3 p) {
    return 0.5 * sqrt(15 * RCP_PI) * p.x * p.y;
}

float GetY30(float3 p) {
    return 0.25 * sqrt(7 * RCP_PI) * p.z * (2 * p.z * p.z - 3 * p.x * p.x - 3 * p.y * p.y);
}
float GetY3n1(float3 p) {
    return 0.25 * sqrt(21 * 0.5 * RCP_PI) * p.y * (4 * p.z * p.z - p.x * p.x - p.y * p.y);
}
float GetY3p1(float3 p) {
    return 0.25 * sqrt(21 * 0.5 * RCP_PI) * p.x * (4 * p.z * p.z - p.x * p.x - p.y * p.y);
}
float GetY3n2(float3 p) {
    return 0.5 * sqrt(105 * RCP_PI) * p.x * p.y * p.z;
}
float GetY3p2(float3 p) {
    return 0.25 * sqrt(105 * RCP_PI) * p.z * (p.x * p.x - p.y * p.y);
}
float GetY3n3(float3 p) {
    return 0.25 * sqrt(35 * 0.5 * RCP_PI) * p.y * (3 * p.x * p.x - p.y * p.y);
}
float GetY3p3(float3 p) {
    return 0.25 * sqrt(35 * 0.5 * RCP_PI) * p.y * (p.x * p.x - 3 * p.y * p.y);
}