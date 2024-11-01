using Fusion;
using MixedReality.Toolkit;
using System.Collections.Generic;
using System.Linq;
using IntraXR;
using MixedReality.Toolkit.SpatialManipulation;
using Unity.XR.CoreUtils;
using UnityEngine;

public class Primitive : NetworkBehaviour
{
    static Material[] materials = new Material[10];

    private static readonly int BufferLength = Shader.PropertyToID("buffer_length");

    private static readonly int NodeBuffer = Shader.PropertyToID("node_buffer");
    //static Color[] colors = { Color.red, Color.red, Color.red , Color.red};

    public static GameObject MyCylinder(float radiusA, float radiusB, float height)
    {
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();
        const int cnt = 100;
        float deltaRad = Mathf.PI * 2 / cnt;
        for (int i = 0; i < cnt; i++)
        {
            float rad = i * deltaRad;
            float x = radiusA * Mathf.Sin(rad) / 2;
            float z = radiusA * Mathf.Cos(rad) / 2;
            float y = radiusB * Mathf.Sin(rad) / 2;
            float w = radiusB * Mathf.Cos(rad) / 2;

            verts.Add(new Vector3(x, height / 2, z));
            verts.Add(new Vector3(y, -height / 2, w));
        }

        mesh.SetVertices(verts);
        List<int> indexList = new List<int>();
        for (int i = 0; i < cnt; i++)
        {
            if (i == cnt - 1)
            {
                // 
                indexList.Add(2 * i);
                indexList.Add(2 * i + 1);
                indexList.Add(0);

                indexList.Add(0);
                indexList.Add(2 * i + 1);
                indexList.Add(1);
            }
            else
            {
                // 
                indexList.Add(2 * i);
                indexList.Add(2 * i + 1);
                indexList.Add(2 * (i + 1));

                indexList.Add(2 * (i + 1));
                indexList.Add(2 * i + 1);
                indexList.Add(2 * i + 3);
            }
        }

        mesh.SetIndices(indexList.ToArray(), MeshTopology.Triangles, 0);
        mesh.RecalculateNormals();

        GameObject cylinder = new GameObject("cylinder");
        cylinder.AddComponent<MeshFilter>().mesh = mesh;
        cylinder.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Textures/Default");
        //cylinder.GetComponent<MeshRenderer>().material.enableInstancing = true;

        return cylinder;
    }

    public static GameObject CreateCylinder(Marker marker, Vector3Int dim, Transform parentTransform,
        float radiusBias = 0)
    {
        if (marker.parent == null) return null;
        var positionA = marker.position.Div(dim) - 0.5f * Vector3.one;
        float radiusA = (marker.radius + radiusBias) * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z)) *
                        parentTransform.parent.localScale.x;

        var parent = marker.parent;
        var positionB = marker.parent.position.Div(dim) - 0.5f * Vector3.one;
        float radiusB = (parent.radius + radiusBias) * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z)) *
                        parentTransform.parent.localScale.x;
        //radiusA = Mathf.Min(parentTransform.localScale.x);

        positionA = parentTransform.TransformPoint(positionA);
        positionB = parentTransform.TransformPoint(positionB);


        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        float length = Vector3.Distance(positionA, positionB);
        GameObject myCylinder = MyCylinder(radiusA, radiusB, length);
        myCylinder.transform.position = (positionA + positionB) / 2;
        myCylinder.transform.up = (positionA - positionB).normalized;
        myCylinder.transform.SetParent(reconstruction, true);
        //myCylinder.transform.SetParent(parentTransform);

        myCylinder.GetComponent<MeshRenderer>().material = materials[marker.type];
        return myCylinder;
    }

    [Rpc]
    public static void RpcCreateCylinder(NetworkRunner runner, Vector3 positionA, Vector3 positionB, float radiusA,
        float radiusB, int type, RpcInfo info = default)
    {
        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        float length = Vector3.Distance(positionA, positionB);
        GameObject myCylinder = MyCylinder(radiusA, radiusB, length);
        myCylinder.transform.position = (positionA + positionB) / 2;
        myCylinder.transform.up = (positionA - positionB).normalized;
        myCylinder.transform.SetParent(reconstruction, true);
        //myCylinder.transform.SetParent(parentTransform);  

        myCylinder.GetComponent<MeshRenderer>().material = Resources.Load<Material>($"Textures/{type}");
        ;
    }

    public static GameObject CreateCylinderTemp(Marker marker, Vector3Int dim, Transform parentTransform,
        Transform reconstruction, int colortype, float radiusBias = 0)
    {
        if (marker.parent == null) return null;
        var positionA = marker.position.Div(dim) - 0.5f * Vector3.one;
        float radiusA = (marker.radius + radiusBias) * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z)) *
                        parentTransform.parent.localScale.x;

        var parent = marker.parent;
        var positionB = marker.parent.position.Div(dim) - 0.5f * Vector3.one;
        float radiusB = (parent.radius + radiusBias) * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z)) *
                        parentTransform.parent.localScale.x;
        //radiusA = Mathf.Min(parentTransform.localScale.x);

        positionA = parentTransform.TransformPoint(positionA);
        positionB = parentTransform.TransformPoint(positionB);


        float length = Vector3.Distance(positionA, positionB);
        GameObject myCylinder = MyCylinder(radiusA, radiusB, length);
        myCylinder.transform.SetParent(reconstruction, false);
        myCylinder.transform.position = (positionA + positionB) / 2;
        myCylinder.transform.up = (positionA - positionB).normalized;
        //myCylinder.transform.SetParent(parentTransform);

        myCylinder.GetComponent<MeshRenderer>().material = materials[colortype];
        return myCylinder;
    }

    public static GameObject CreateCylinder(Marker marker, Vector3Int dim, Transform parentTransform,
        float colorIntensity, float radiusBias = 0)
    {
        if (marker.parent == null) return null;
        var positionA = marker.position.Div(dim) - 0.5f * Vector3.one;
        float radiusA = (marker.radius + radiusBias) * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z)) *
                        parentTransform.parent.localScale.x;

        var parent = marker.parent;
        var positionB = marker.parent.position.Div(dim) - 0.5f * Vector3.one;
        float radiusB = (parent.radius + radiusBias) * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z)) *
                        parentTransform.parent.localScale.x;
        //radiusA = Mathf.Min(parentTransform.localScale.x);

        positionA = parentTransform.TransformPoint(positionA);
        positionB = parentTransform.TransformPoint(positionB);


        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        float length = Vector3.Distance(positionA, positionB);
        GameObject myCylinder = MyCylinder(radiusA, radiusB, length);
        myCylinder.transform.position = (positionA + positionB) / 2;
        myCylinder.transform.up = (positionA - positionB).normalized;
        myCylinder.transform.SetParent(reconstruction, true);

        //myCylinder.GetComponent<MeshRenderer>().material.color = Color.Lerp(Color.red, Color.green, colorIntensity);
        // myCylinder.GetComponent<MeshRenderer>().material.color = Color.Lerp(new Color(158, 1, 66), new Color(78, 98, 171), colorIntensity);
        myCylinder.GetComponent<MeshRenderer>().material.shader = Shader.Find("Unlit/Color");
        myCylinder.GetComponent<MeshRenderer>().material.shader = Shader.Find("Standard");
        myCylinder.GetComponent<MeshRenderer>().material.color = GetColor(colorIntensity);

        var info = myCylinder.AddComponent<NodeInformation>();
        info.imgIndex = (int)marker.img_index(1024, 1024 * 1024);
        info.type = marker.type;
        info.radius = marker.radius;
        info.position = marker.position;
        info.batch = marker.batch;
        return myCylinder;
    }

    public static GameObject CreateSphere(Marker marker, Vector3Int dim, Transform parentTransform,
        float radiusBias = 0)
    {
        var position = marker.position.Div(dim) - 0.5f * Vector3.one;
        var radius = (marker.radius + radiusBias) * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z));
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (marker.type > 4 || marker.type<0) marker.type = 0;
        sphere.GetComponent<MeshRenderer>().material = materials[marker.type];

        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        sphere.transform.position = parentTransform.TransformPoint(position);
        sphere.transform.SetParent(reconstruction, true);
        sphere.transform.localScale = new Vector3(1, 1, 1) * radius;

        if (marker.isLeaf)
        {
            //sphere.GetComponent<MeshRenderer>().material = materials[4];
            //sphere.transform.localScale = sphere.transform.localScale * 2;
            var statefulInteractable = sphere.AddComponent<StatefulInteractable>();
            statefulInteractable.ToggleMode = StatefulInteractable.ToggleType.OneWayToggle;
            var pipeCasing = sphere.AddComponent<PipeCasing>();
            pipeCasing.Initial(marker, dim, parentTransform);
            statefulInteractable.OnClicked.AddListener(() =>
            {
                if (statefulInteractable.IsToggled)
                {
                    pipeCasing.AddLength();
                    pipeCasing.Activate();
                }
                else
                {
                    pipeCasing.ClearPipes();
                }
            });
        }

        var info = sphere.AddComponent<NodeInformation>();
        info.imgIndex = (int)marker.img_index(1024, 1024 * 1024);
        info.type = marker.type;
        info.radius = marker.radius;
        info.position = marker.position;
        info.batch = marker.batch;
        return sphere;
    }

    [Rpc]
    public static void RpcClearTree(NetworkRunner runner)
    {
        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        for (int i = 0; i < reconstruction.childCount; i++)
        {
            GameObject.Destroy(reconstruction.transform.GetChild(i).gameObject);
        }
        
        Transform sdf = GameObject.Find("SDF").transform;
        for (int i = 0; i < sdf.childCount; i++)
        {
            GameObject.Destroy(sdf.transform.GetChild(i).gameObject);
        }
    }
    
    public static void ClearTree( )
    {
        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        for (int i = 0; i < reconstruction.childCount; i++)
        {
            GameObject.Destroy(reconstruction.transform.GetChild(i).gameObject);
        }
        
        Transform sdf = GameObject.Find("SDF").transform;
        for (int i = 0; i < sdf.childCount; i++)
        {
            GameObject.Destroy(sdf.transform.GetChild(i).gameObject);
        }
    }

    [Rpc]
    public static void RpcCreateSphere(NetworkRunner runner, Vector3 position, float radius, int type,
        RpcInfo info = default)
    {
        if (info.IsInvokeLocal) return;
        Debug.Log(info);
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.GetComponent<MeshRenderer>().material = Resources.Load<Material>($"Textures/{type}");
        ;

        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        sphere.transform.position = position;
        sphere.transform.SetParent(reconstruction, true);
        sphere.transform.localScale = new Vector3(1, 1, 1) * radius;
    }

    public static GameObject CreateSphere(Vector3 position, float radius, int type)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.GetComponent<MeshRenderer>().material = Resources.Load<Material>($"Textures/{type}");
        ;

        Transform reconstruction = GameObject.Find("Reconstruction").transform;
        sphere.transform.position = position;
        sphere.transform.localScale = new Vector3(1, 1, 1) * radius;
        sphere.transform.SetParent(reconstruction, true);
        return sphere;
    }

    public static GameObject CreateSphereTemp(Marker marker, Vector3Int dim, Transform parentTransform,
        Transform reconstruction, int colorType)
    {
        var position = marker.position.Div(dim) - 0.5f * Vector3.one;
        var radius = marker.radius * Mathf.Min(1.0f / dim.x, Mathf.Min(1.0f / dim.y, 1.0f / dim.z));
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (marker.type > 4) marker.type = 0;
        sphere.GetComponent<MeshRenderer>().material = materials[colorType];

        //Transform reconstruction = GameObject.Find("Reconstruction").transform;
        sphere.transform.position = parentTransform.TransformPoint(position);
        sphere.transform.SetParent(reconstruction, true);
        sphere.transform.localScale = new Vector3(1, 1, 1) * radius;

        if (marker.isLeaf)
        {
            //sphere.GetComponent<MeshRenderer>().material = materials[4];
            //sphere.transform.localScale = sphere.transform.localScale * 2;
            var statefulInteractable = sphere.AddComponent<StatefulInteractable>();
            statefulInteractable.ToggleMode = StatefulInteractable.ToggleType.OneWayToggle;
            var pipeCasing = sphere.AddComponent<PipeCasing>();
            pipeCasing.Initial(marker, dim, parentTransform);
            statefulInteractable.OnClicked.AddListener(() =>
            {
                if (statefulInteractable.IsToggled)
                {
                    pipeCasing.AddLength();
                    pipeCasing.Activate();
                }
                else
                {
                    pipeCasing.ClearPipes();
                }
            });
        }

        var info = sphere.AddComponent<NodeInformation>();
        info.imgIndex = (int)marker.img_index(1024, 1024 * 1024);
        info.type = marker.type;
        info.radius = marker.radius;
        info.position = marker.position;
        info.batch = marker.batch;
        return sphere;
    }
    
    private static Color GetColor(float colorIntensity)
    {
        Color[] colors = new Color[11]
        {
            new Color(108 / 255.0f, 001 / 255.0f, 33 / 255.0f), new Color(158 / 255.0f, 001 / 255.0f, 66 / 255.0f),
            new Color(214 / 255.0f, 64 / 255.0f, 78 / 255.0f),
            new Color(245 / 255.0f, 117 / 255.0f, 71 / 255.0f), new Color(253 / 255.0f, 185 / 255.0f, 106 / 255.0f),
            new Color(254 / 255.0f, 232 / 255.0f, 154 / 255.0f),
            new Color(245 / 255.0f, 251 / 255.0f, 177 / 255.0f), new Color(203 / 255.0f, 233 / 255.0f, 157 / 255.0f),
            new Color(135 / 255.0f, 207 / 255.0f, 164 / 255.0f),
            new Color(100 / 255.0f, 175 / 255.0f, 170 / 255.0f), new Color(70 / 255.0f, 158 / 255.0f, 180 / 255.0f)
        };
        float[] ranges = new float[11] { 0.0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f };
        for (int i = 0; i < ranges.Length - 1; i++)
        {
            if (colorIntensity >= ranges[i] && colorIntensity < ranges[i + 1])
                return Color.Lerp(colors[i], colors[i + 1], (colorIntensity - ranges[i]) / (ranges[i + 1] - ranges[i]));
        }

        return colors[^1];
    }

    public static void RpcCreateTree(NetworkRunner runner, List<Marker> tree, Transform cubeTransform, Vector3Int dim)
    {
        RpcClearTree(runner);
        foreach (var marker in tree)
        {
            Vector3 positionA = marker.position.Div(dim) - 0.5f * Vector3.one;
            positionA = cubeTransform.TransformPoint(positionA);
            float radiusA = marker.radius * Vector3.one.Divide(dim).MinComponent() *
                            cubeTransform.parent.transform.localScale.x * Config.Instance.radiusScale;

            RpcCreateSphere(runner, positionA, radiusA, marker.type);
            var sphere = CreateSphere(positionA, radiusA, marker.type);
            //if (marker.isLeaf)
            //{
            //    var si = sphere.AddComponent<StatefulInteractable>();
            //    si.ToggleMode = StatefulInteractable.ToggleType.Toggle;
            //    var pipeCasing = sphere.AddComponent<PipeCasing>();
            //    pipeCasing.Initial(marker, dim, parentTransform);
            //    si.IsToggled.OnEntered.AddListener((args) =>
            //    {
            //        if (Config.Instance.isIsolating) return;
            //        Config.Instance.isIsolating = true;
            //        GameObject menu = GameObject.Find("IsolateMenu(Clone)") ?? GameObject.Instantiate(Resources.Load("Prefabs/IsolateMenu")) as GameObject;
            //        SolverHandler sh = menu.GetComponent<SolverHandler>();
            //        sh.TrackedTargetType = TrackedObjectType.CustomOverride;
            //        sh.TransformOverride = sphere.transform;
            //        Orbital orbital = menu.GetComponent<Orbital>();
            //        Vector3 offset = -0.1f * (marker.position - Config.Instance._rootPos).normalized;
            //        orbital.LocalOffset = new Vector3(offset.x, offset.y, 0);
            //        menu.SetActive(true);
            //        menu.GetComponent<IsolateMenu>().pipeCasing = pipeCasing;
            //        pipeCasing.ClearPipes();
            //        Config.Instance.gazeController.interactionType = GazeController.EyeInteractionType.None;
            //        Config.Instance.paintingBoard.GetComponent<ObjectManipulator>().AllowedInteractionTypes = InteractionFlags.None;
            //    });
            //}

            if (marker.parent != null)
            {
                var positionB = marker.parent.position.Div(dim) - 0.5f * Vector3.one;
                float radiusB = marker.parent.radius * Vector3.one.Divide(dim).MinComponent() *
                                cubeTransform.parent.transform.localScale.x * Config.Instance.radiusScale;
                positionB = cubeTransform.TransformPoint(positionB);

                RpcCreateCylinder(runner, positionA, positionB, radiusA, radiusB, marker.type);
            }
        }
    }
    
    public static void RpcCreateTreeSDF(NetworkRunner runner, List<Marker> tree, Transform cubeTransform, Vector3Int dim)
    {
        RpcClearTree(runner);
        Dictionary<Marker, int> indexes = new(tree.Count);
        List<RayMarchedNode> rayMarchedNodes = new(tree.Count);
        float dimMax = ((Vector3)dim).MaxComponent();
        Vector3 halfVector = 0.5f * Vector3.one;
        Material material_color = Resources.Load<Material>("Textures/White");
        for (int i = 0; i < tree.Count; i++)
        {
            Marker marker = tree[i];
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //GameObject node = new GameObject("node");
            RayMarchedNode rayMarchedNode = node.AddComponent<RayMarchedNode>();
            Destroy(node.GetComponent<SphereCollider>());
            node.GetComponent<MeshRenderer>().material = material_color;
            node.name = i.ToString();
            node.transform.localScale = new Vector3(1.0f/dimMax, 1.0f/dimMax, 1.0f/dimMax);
            node.transform.SetParent(cubeTransform.Find("SDF"));
            node.transform.localPosition = marker.position.Div(dim) - halfVector;

            node.GetComponent<MeshRenderer>().enabled = false;
            
            // rayMarchedNode.radius = marker.radius / dimMax;
            rayMarchedNode.radius = marker.radius/2;
            rayMarchedNode.Type = marker.type;
            rayMarchedNode.index = i;
            if (marker.isLeaf) rayMarchedNode.Type = -rayMarchedNode.Type;
            
            rayMarchedNodes.Add(rayMarchedNode);
            indexes[marker] = i;
        }

        for (int i = 0; i < tree.Count; i++)
        {
            Marker marker = tree[i];
            if (marker.parent != null && !indexes.ContainsKey(marker.parent))
            {
                Debug.Log(marker.position);
                Debug.Log(marker.parent.position);
                BoardManager.Instance.CreatePoint(marker.position, dim, Color.green); 
                BoardManager.Instance.CreatePoint(marker.parent.position, dim, Color.green); 
            }
        }
        for (int i = 0; i < tree.Count; i++)
        {
            Marker marker = tree[i];
            RayMarchedNode rayMarchedNode = rayMarchedNodes[i];
 
            rayMarchedNode.parent = marker.parent == null ? rayMarchedNode : rayMarchedNodes[indexes[marker.parent]];
        }
        
        for (int i = 0; i < tree.Count; i++)
        {
            RayMarchedNode rayMarchedNode = rayMarchedNodes[i];
            rayMarchedNode.CreateProxy();
        }
    }
    public static void CreateTreeSDF(List<Marker> tree, Transform cubeTransform, Vector3Int dim, bool generateControlPoint=false)
    {
        ClearTree();
        Dictionary<Marker, int> indexes = new(tree.Count);
        List<RayMarchedNode> rayMarchedNodes = new(tree.Count);
        float dimMax = ((Vector3)dim).MaxComponent();
        Vector3 halfVector = 0.5f * Vector3.one;
        Material material_color = Resources.Load<Material>("Textures/White");
        for (int i = 0; i < tree.Count; i++)
        {
            Marker marker = tree[i];
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            RayMarchedNode rayMarchedNode = node.AddComponent<RayMarchedNode>();
            Destroy(node.GetComponent<SphereCollider>());
            node.GetComponent<MeshRenderer>().material = material_color;
            node.name = i.ToString();
            node.transform.localScale = new Vector3(1.0f/dimMax, 1.0f/dimMax, 1.0f/dimMax);
            node.transform.SetParent(cubeTransform.Find("SDF"));
            node.transform.localPosition = marker.position.Div(dim) - halfVector;

            node.GetComponent<MeshRenderer>().enabled = generateControlPoint;
            
            if (marker.isLeaf)
            {
                node.AddComponent<SphereCollider>();
                var si = node.AddComponent<StatefulInteractable>();
                si.ToggleMode = StatefulInteractable.ToggleType.Toggle;
                var pipeCasing = node.AddComponent<PipeCasing>();
                pipeCasing.Initial(marker, dim, cubeTransform);
                si.IsToggled.OnEntered.AddListener((args) =>
                {
                    if (Config.Instance.isIsolating) return;
                    Config.Instance.isIsolating = true;
                    GameObject menu = GameObject.Find("IsolateMenu(Clone)") ?? GameObject.Instantiate(Resources.Load("Prefabs/IsolateMenu")) as GameObject;
                    SolverHandler sh = menu.GetComponent<SolverHandler>();
                    sh.TrackedTargetType = TrackedObjectType.CustomOverride;
                    sh.TransformOverride = node.transform;
                    Orbital orbital = menu.GetComponent<Orbital>();
                    Vector3 offset = -0.1f * (marker.position - Config.Instance._rootPos).normalized;
                    orbital.LocalOffset = new Vector3(offset.x, offset.y, 0);
                    menu.SetActive(true);
                    menu.GetComponent<IsolateMenu>().pipeCasing = pipeCasing;
                    pipeCasing.ClearPipes();
                    Config.Instance.gazeController.interactionType = GazeController.EyeInteractionType.None;
                    Config.Instance.paintingBoard.GetComponent<ObjectManipulator>().AllowedInteractionTypes = InteractionFlags.None;
                });
            }
            
            // rayMarchedNode.radius = marker.radius / dimMax;
            rayMarchedNode.radius = marker.radius/2;
            rayMarchedNode.Type = marker.type;
            rayMarchedNode.index = i;
            if (marker.isLeaf) rayMarchedNode.Type = -rayMarchedNode.Type;
            
            rayMarchedNodes.Add(rayMarchedNode);
            indexes[marker] = i;

            if (marker.parent == null)
            {
               var transferSomaInfo = node.AddComponent<TransferSomaInfo>();
               transferSomaInfo.renderer = GameObject.Find("SDF").GetComponent<MeshRenderer>();
               transferSomaInfo.radius = marker.radius * 1.3f / 2.0f;
            }
        }

        for (int i = 0; i < tree.Count; i++)
        {
            Marker marker = tree[i];
            if (marker.parent != null && !indexes.ContainsKey(marker.parent))
            {
                Debug.Log(marker.position);
                Debug.Log(marker.parent.position);
                BoardManager.Instance.CreatePoint(marker.position, dim, Color.green); 
                BoardManager.Instance.CreatePoint(marker.parent.position, dim, Color.green); 
            }
        }
        for (int i = 0; i < tree.Count; i++)
        {
            Marker marker = tree[i];
            RayMarchedNode rayMarchedNode = rayMarchedNodes[i];
 
            rayMarchedNode.parent = marker.parent == null ? rayMarchedNode : rayMarchedNodes[indexes[marker.parent]];
        }
        
        for (int i = 0; i < tree.Count; i++)
        {
            RayMarchedNode rayMarchedNode = rayMarchedNodes[i];
            rayMarchedNode.CreateProxy();
        }
    }
    public static void CreateTree(List<Marker> tree, Transform parentTransform, Vector3Int dim)
    {
        materials[0] = Resources.Load<Material>("Textures/0");
        materials[1] = Resources.Load<Material>("Textures/1");
        materials[2] = Resources.Load<Material>("Textures/2");
        materials[3] = Resources.Load<Material>("Textures/3");
        materials[4] = Resources.Load<Material>("Textures/4");
        materials[5] = Resources.Load<Material>("Textures/5");
        materials[6] = Resources.Load<Material>("Textures/6");
        materials[7] = Resources.Load<Material>("Textures/7");
        materials[8] = Resources.Load<Material>("Textures/8");
        materials[9] = Resources.Load<Material>("Textures/9");

        foreach (var marker in tree)
        {
            GameObject sphere = CreateSphere(marker, dim, parentTransform);

            if (marker.parent != null)
            {
                CreateCylinder(marker, dim, parentTransform);
            }
            else
            {
                sphere.name = "Soma";
            }
        }
    }

    public static void CreateTreeTemp(List<Marker> tree, Transform parentTransform, Transform reconstruction,
        int colorType, Vector3Int dim)
    {
        materials[0] = Resources.Load<Material>("Textures/0");
        materials[1] = Resources.Load<Material>("Textures/1");
        materials[2] = Resources.Load<Material>("Textures/2");
        materials[3] = Resources.Load<Material>("Textures/3");
        materials[4] = Resources.Load<Material>("Textures/4");
        materials[5] = Resources.Load<Material>("Textures/5");
        materials[6] = Resources.Load<Material>("Textures/6");
        materials[7] = Resources.Load<Material>("Textures/7");
        materials[8] = Resources.Load<Material>("Textures/8");
        materials[9] = Resources.Load<Material>("Textures/9");

        //float Scale = 1 / 512.0f;
        foreach (var marker in tree)
        {
            GameObject sphere = Primitive.CreateSphereTemp(marker, dim, parentTransform, reconstruction, colorType);

            if (marker.parent != null)
            {
                var parent = marker.parent;
                GameObject cylinder =
                    Primitive.CreateCylinderTemp(marker, dim, parentTransform, reconstruction, colorType);
                //Chosen c = cylinder.AddComponent<Chosen>();
                //c.nodeA = parent;
                //c.nodeB = node;
                //node.cylinder = cylinder;
            }
            else
            {
                sphere.name = "Soma";
                //Chosen soma = sphere.AddComponent<Chosen>();
                //soma.nodeA = node;
                //soma.nodeB = node;
            }
        }
    }

    public static void CreateTree(List<Marker> tree, Transform parentTransform, Vector3Int dim, List<float> growth)
    {
        materials[0] = Resources.Load<Material>("Textures/0");
        materials[1] = Resources.Load<Material>("Textures/1");
        materials[2] = Resources.Load<Material>("Textures/2");
        materials[3] = Resources.Load<Material>("Textures/3");
        materials[4] = Resources.Load<Material>("Textures/4");
        materials[5] = Resources.Load<Material>("Textures/5");
        materials[6] = Resources.Load<Material>("Textures/6");
        materials[7] = Resources.Load<Material>("Textures/7");
        materials[8] = Resources.Load<Material>("Textures/8");
        materials[9] = Resources.Load<Material>("Textures/9");
        //float Scale = 1 / 512.0f;
        foreach (var marker in tree)
        {
            marker.radius = 1.8f * marker.radius;
        }

        for (int i = 0; i < tree.Count; i++)
        {
            Marker marker = tree[i];
            GameObject sphere = Primitive.CreateSphere(marker, dim, parentTransform, growth[i]);

            if (marker.parent != null)
            {
                var parent = marker.parent;
                GameObject cylinder = Primitive.CreateCylinder(marker, dim, parentTransform, growth[i], 0);
            }
            else
            {
                sphere.name = "Soma";
            }
        }
    }

    public static void CreateBranch(List<Marker> branch, Transform parentTransform, Vector3Int dim)
    {
        foreach (var marker in branch)
        {
            GameObject sphere = Primitive.CreateSphere(marker, dim, parentTransform);

            //Marker parent;

            GameObject cylinder = Primitive.CreateCylinder(marker, dim, parentTransform);
            //Chosen c = cylinder.AddComponent<Chosen>();
            //App2.MarkerMap[marker] = c;
            //c.nodeA = parent;
            //c.nodeB = node;
            //node.cylinder = cylinder;
        }
    }
}
