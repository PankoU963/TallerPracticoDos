using UnityEngine;
using UnityEditor;

public class CreateOutlineDuplicate
{
    [MenuItem("GameObject/3D Object/Create Outline Duplicate", false, 10)]
    static void CreateOutline()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Select a GameObject with a MeshFilter/MeshRenderer to create an outline duplicate.");
            return;
        }

        GameObject src = Selection.activeGameObject;
        MeshFilter mf = src.GetComponent<MeshFilter>();
        MeshRenderer mr = src.GetComponent<MeshRenderer>();
        if (mf == null || mr == null)
        {
            Debug.LogWarning("Selected GameObject does not have MeshFilter/MeshRenderer.");
            return;
        }

        // Ensure materials folder exists
        string matFolder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string matPath = matFolder + "/OutlineSimple.mat";
        Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (outlineMat == null)
        {
            Shader outlineShader = Shader.Find("Hidden/OutlineSimple");
            if (outlineShader == null)
            {
                Debug.LogError("Outline shader not found. Make sure Assets/Shaders/OutlineSimple.shader exists.");
                return;
            }
            outlineMat = new Material(outlineShader);
            AssetDatabase.CreateAsset(outlineMat, matPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Duplicate object
        GameObject dup = Object.Instantiate(src, src.transform.position, src.transform.rotation, src.transform);
        dup.name = src.name + "_Outline";

        // Remove unnecessary components
        foreach (var comp in dup.GetComponents<MonoBehaviour>())
        {
            Object.DestroyImmediate(comp);
        }

        // Assign outline material
        var dupMr = dup.GetComponent<MeshRenderer>();
        if (dupMr != null)
        {
            dupMr.sharedMaterial = outlineMat;
            // Ensure it renders before the original (so outline appears behind)
            dupMr.sortingOrder = mr.sortingOrder - 1;
        }

        // Slightly scale up to avoid z-fighting (alternatively shader extrusion handles it)
        dup.transform.localScale = src.transform.localScale * 1.001f;

        Selection.activeGameObject = dup;
        Debug.Log("Created outline duplicate: " + dup.name);
    }
}
