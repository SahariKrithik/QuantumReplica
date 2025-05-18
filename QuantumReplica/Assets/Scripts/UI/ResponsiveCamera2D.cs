using UnityEngine;

public class ResponsiveCamera2D : MonoBehaviour
{
    public float baseOrthographicSize = 5f;
    public float targetAspect = 16f / 9f;

    public void RecalculateCamera()
    {
        Camera cam = GetComponent<Camera>();
        float windowAspect = (float)Screen.width / Screen.height;
        float scale = targetAspect / windowAspect;

        if (scale < 1) scale = 1;
        cam.orthographicSize = baseOrthographicSize * scale;
    }

    void Start()
    {
        RecalculateCamera();
    }
}
