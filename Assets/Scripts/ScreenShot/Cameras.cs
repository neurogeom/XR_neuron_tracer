using System.Collections.Generic;
using UnityEngine;

class CamerasPara
{
    public string SceneName;
    public float ViewportWidth;
    public float ViewportHeight;
    public float CameraDistance;
    public float NearClip;
    public float FarClip;
    public int PixelWidth;
    public int PixelHeight;
    public int CameraRows;
    public int CameraColumns;
    public float HorizontalFieldOfView;
    public float VerticalFieldOfView;
    public CameraArray[] Cameras;
}
class CameraArray
{
    public CameraPara parameters;
    public int row;
    public int column;
    public string key;
}

class CameraPara 
{
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Matrix modelViewMatrix;
    public Matrix projectionMatrix;
}

class Matrix
{
    public float e00;
    public float e01;
    public float e02;
    public float e03;
    public float e10;
    public float e11;
    public float e12;
    public float e13;
    public float e20;
    public float e21;
    public float e22;
    public float e23;
    public float e30;
    public float e31;
    public float e32;
    public float e33;
}