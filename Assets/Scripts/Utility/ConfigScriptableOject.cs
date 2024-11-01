using CommandStructure;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/ConfigScriptableOject", order = 1)]
public class ConfigScriptableOject : ScriptableObject
{
    public string path;
    public string savePath;
    public bool needImport;
    public bool scale = true;
    public bool gaussianSmoothing = true;
    public bool forceRootCenter = false;
    public Texture3D volume;
    public Vector3Int scaledDim;
    public int somaRadius;
    public int bkgThresh;
    public int blockSize;
    public int viewThresh;
    public bool useBatch = true;
    public bool useKeyBoard = true;
    public int customThresh = 30;

    public Material fixedBkgThresholdMaterial;

    public int thresholdOffset = 0;
    public float viewRadius = 5.0f;

    public bool volumeRenderingWithChebyshev = false;
}
