using Fusion;
using Fusion.Sockets;
using MixedReality.Toolkit.SpatialManipulation;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log("OnPlayerJoined");
        GameObject paintingBoard = GameObject.Find("PaintingBoard(Clone)");
        if (paintingBoard == null)
        {
            var boardPrefab = Resources.Load("Prefabs/PaintingBoard") as GameObject;
            paintingBoard = _runner.Spawn(boardPrefab, new Vector3(0, 0, 1), Quaternion.identity).gameObject;
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
            configObj = _runner.Spawn(configPrefab, Vector3.zero, Quaternion.identity).gameObject;
        }
        Config config = configObj.GetComponent<Config>();

        config.paintingBoard = paintingBoard;
        config.cube = paintingBoard.GetNamedChild("Cube");
        config.cube.transform.localScale = new Vector3(config.originalDim.x, config.originalDim.y, config.originalDim.z) / MathF.Max(config.originalDim.x, MathF.Max(config.originalDim.y, config.originalDim.z));
        config.runner = _runner;

        GameObject Menu = GameObject.Find("HandMenuBase(Clone)");
        if (Menu == null)
        {
            GameObject.Instantiate(Resources.Load("Prefabs/HandMenuBase"));
        }



        GetComponent<NetworkPhysicsSimulation3D>().enabled = false;
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) 
    {
        Debug.Log("shutdown");
        Debug.Log(shutdownReason.ToString());
        
        GameObject paintingBoard = GameObject.Find("PaintingBoard(Clone)");
        if (paintingBoard == null)
        {
            var boardPrefab = Resources.Load("Prefabs/PaintingBoard") as GameObject;
            paintingBoard = Instantiate(boardPrefab, new Vector3(0, 0, 1), Quaternion.identity).gameObject;
        }

        ObjectManipulator OM = paintingBoard.GetComponent<ObjectManipulator>();
        OM.selectEntered.AddListener((SelectEnterEventArgs args) =>
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
        config.runner = _runner;

        GameObject Menu = GameObject.Find("HandMenuBase(Clone)");
        if (Menu == null)
        {
            GameObject.Instantiate(Resources.Load("Prefabs/HandMenuBase"));
        }

        Camera.main.clearFlags = CameraClearFlags.SolidColor;

    }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
 
    }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    void Update()
    {
    }

    void Start()
    {
        StartGame(GameMode.Shared);
    }

    async void StartGame(GameMode mode)
    {
        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // Start or join (depends on gamemode) a session with a specific name
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = SceneManager.GetActiveScene().buildIndex,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });


    }
}


