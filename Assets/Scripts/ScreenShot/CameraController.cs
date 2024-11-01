using System.Collections;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class CameraController : MonoBehaviour
{
    void Start()
    {
        // ��ȡ����
        string jsonData = ReadData();
        // ��������
        CamerasPara camerasPara = JsonConvert.DeserializeObject<CamerasPara>(jsonData);

        StartCoroutine(MoveCamera(camerasPara));

    }

    IEnumerator MoveCamera(CamerasPara camerasPara)
    {
        // �����������
        Camera cam = Camera.main;
        cam.nearClipPlane = camerasPara.NearClip;
        cam.farClipPlane = camerasPara.FarClip;
        cam.farClipPlane = camerasPara.FarClip;
        cam.fieldOfView = camerasPara.VerticalFieldOfView;
        // "ViewportWidth": 3.2767999172210695, 
        // "ViewportHeight": 1.8431999683380128,

        GameObject cube = GameObject.Find("Cube");
        Material material = cube.GetComponent<MeshRenderer>().material;
        CameraArray[] cameras = camerasPara.Cameras;
        for(int i = 0; i < cameras.Length; i++)
        {
            // �������λ�ú���ת�Ƕ�
            Vector3 cameraPisition = cameras[i].parameters.localPosition;
            Vector3 cameraRotation = cameras[i].parameters.localRotation;
            cam.transform.position = new Vector3(cameraPisition.x, cameraPisition.y, cameraPisition.z);
            cam.transform.rotation = new Quaternion(cameraRotation.x, cameraRotation.y, cameraRotation.z, 1f);

            // �������ͶӰ����
            Matrix cameraPM = cameras[i].parameters.projectionMatrix;
            cam.projectionMatrix = new Matrix4x4(
                new Vector4(cameraPM.e00, cameraPM.e01, cameraPM.e02, cameraPM.e03),
                new Vector4(cameraPM.e10, cameraPM.e11, cameraPM.e12, cameraPM.e13),
                new Vector4(cameraPM.e20, cameraPM.e21, cameraPM.e22, cameraPM.e23),
                new Vector4(cameraPM.e30, cameraPM.e31, cameraPM.e32, cameraPM.e33));

            cam.transform.LookAt(GameObject.Find("Cube").transform);
            material.SetFloat("_IsDepth", 0);
            ScreenCapture.CaptureScreenshot($"ScreenShot/{cameras[i].key}_rgbd.png");
            yield return new WaitForSeconds(0.1f);
            material.SetFloat("_IsDepth", 1);
            ScreenCapture.CaptureScreenshot($"ScreenShot/{cameras[i].key}_norm_depth.png");
            yield return new WaitForSeconds(0.1f);
        }

        /*Vector3 basePos = new(-0.0812f, -0.0812f, -0.667f);
        Camera cam = Camera.main;
        GameObject cube = GameObject.Find("Cube");
        Material material = cube.GetComponent<MeshRenderer>().material;
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
            {
                cam.transform.position = basePos + new Vector3(0.0203f * i, 0.0203f * j, 0);
                cam.transform.LookAt(GameObject.Find("Cube").transform);
                material.SetFloat("_IsDepth", 0);
                ScreenCapture.CaptureScreenshot($"ScreenShot/Color_{i}_{j}.png");
                yield return new WaitForSeconds(0.1f);
                material.SetFloat("_IsDepth", 1);
                ScreenCapture.CaptureScreenshot($"ScreenShot/Depth_{i}_{j}.png");
                yield return new WaitForSeconds(0.1f);
            }*/
    }

    //��ȡ�ļ�
    public string ReadData()
    {
        // string���͵����ݳ���
        string readData;
        // ��ȡ��·��
        string fileUrl = "Assets\\Scripts\\ScreenShot\\cameras.json";
        // ��ȡ�ļ�
        using (StreamReader sr = File.OpenText(fileUrl))
        {
            //���ݱ���
            readData = sr.ReadToEnd();
            sr.Close();
        }
        //��������
        return readData;
    }
}
