using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using MixedReality.Toolkit.SpatialManipulation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class OfflneSpawner : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("OnPlayerJoined");
        GameObject paintingBoard = GameObject.Find("PaintingBoard(Clone)");
        if (paintingBoard == null)
        {
            var boardPrefab = Resources.Load("Prefabs/PaintingBoard") as GameObject;
            paintingBoard = Instantiate(boardPrefab, new Vector3(0, 0, 1), Quaternion.identity).gameObject;
        }

        ObjectManipulator om = paintingBoard.GetComponent<ObjectManipulator>();
        om.selectEntered.AddListener((SelectEnterEventArgs args) =>
        {
            paintingBoard.GetComponent<NetworkObject>().RequestStateAuthority();
        });

        GameObject configObj = GameObject.FindGameObjectWithTag("Config");
        if (configObj == null)
        {
            var configPrefab = Resources.Load("Prefabs/Config") as GameObject;
            configObj = Instantiate(configPrefab, Vector3.zero, Quaternion.identity).gameObject;
        }
        Config config = configObj.GetComponent<Config>();

        config.paintingBoard = paintingBoard;
        config.cube = paintingBoard.GetNamedChild("Cube");
        config.cube.transform.localScale = new Vector3(config.originalDim.x, config.originalDim.y, config.originalDim.z) / MathF.Max(config.originalDim.x, MathF.Max(config.originalDim.y, config.originalDim.z));

        GameObject Menu = GameObject.Find("HandMenuBase(Clone)");
        if (Menu == null)
        {
            GameObject.Instantiate(Resources.Load("Prefabs/HandMenuBase"), new Vector3(0,0,-0.1f), Quaternion.identity);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
