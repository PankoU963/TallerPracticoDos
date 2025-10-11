using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class OutlinePostProcess : MonoBehaviour
{
    public Shader outlineShader;
    private Material outlineMaterial;

    [ColorUsage(false, true)]
    public Color edgeColor = Color.black;
    [Range(0,1)]
    public float normalThreshold = 0.2f;
    [Range(0,0.1f)]
    public float depthThreshold = 0.005f;
    [Range(1,8)]
    public int edgeThickness = 1;

    void OnEnable()
    {
        if (outlineShader == null)
            outlineShader = Shader.Find("Hidden/OutlinePost");
        if (outlineShader != null)
            outlineMaterial = new Material(outlineShader);

        Camera cam = GetComponent<Camera>();
        cam.depthTextureMode |= DepthTextureMode.DepthNormals;
    }

    void OnDisable()
    {
        if (outlineMaterial != null)
            DestroyImmediate(outlineMaterial);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (outlineMaterial == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        outlineMaterial.SetColor("_EdgeColor", edgeColor);
        outlineMaterial.SetFloat("_ThresholdNormal", normalThreshold);
        outlineMaterial.SetFloat("_ThresholdDepth", depthThreshold);
        outlineMaterial.SetFloat("_EdgeThickness", edgeThickness);

        Graphics.Blit(src, dest, outlineMaterial);
    }
}
