using UnityEngine;

public class SculptureDisolve : MonoBehaviour
{
    [Header("Player detection")]
    public Transform player;
    public string playerTag = "Player";
    public float activationDistance = 3f;

    [Header("Target child")]
    [Tooltip("Name of the child to target. If empty, the first child with a Renderer will be used.")]
    public string childName;
    public Renderer targetRenderer;

    [Header("Dissolve material / property")]
    [Tooltip("If set, this material will be instanced and used to animate the dissolve property.")]
    public Material dissolveMaterial;
    [Tooltip("Name of the float property in the shader that controls dissolve (example: _Dissolve, _Cutoff, _DissolveAmount)")]
    public string dissolveProperty = "_Dissolve";
    public float dissolveSpeed = 1f;

    // runtime
    private Material originalMaterial;
    private Material runtimeMaterial;
    private float currentAmount = 0f;

    void Start()
    {
        // find player if not set
        if (player == null)
        {
            var go = GameObject.FindWithTag(playerTag);
            if (go != null) player = go.transform;
        }

        // find renderer if not assigned
        if (targetRenderer == null)
        {
            if (!string.IsNullOrEmpty(childName))
            {
                var child = FindDeepChild(transform, childName);
                if (child != null) targetRenderer = child.GetComponent<Renderer>();
            }

            if (targetRenderer == null)
            {
                // fallback: first renderer in children
                targetRenderer = GetComponentInChildren<Renderer>();
            }
        }

        if (targetRenderer != null)
        {
            originalMaterial = targetRenderer.sharedMaterial;

            // choose base for runtime material: explicit dissolveMaterial if provided, otherwise clone original
            if (dissolveMaterial != null)
            {
                runtimeMaterial = new Material(dissolveMaterial);
            }
            else if (originalMaterial != null)
            {
                runtimeMaterial = new Material(originalMaterial);
            }

            if (runtimeMaterial == null)
            {
                Debug.LogWarning("SculptureDisolve: No material available to animate dissolve.");
            }
            else
            {
                // ensure renderer uses the instance so we can animate it safely
                targetRenderer.material = runtimeMaterial;
                // initialize property if exists
                if (runtimeMaterial.HasProperty(dissolveProperty))
                {
                    runtimeMaterial.SetFloat(dissolveProperty, currentAmount);
                }
                else
                {
                    // property not found, warn so user can set correct property name
                    Debug.LogWarning($"SculptureDisolve: runtime material does not have a float property named '{dissolveProperty}'.");
                }
            }
        }
        else
        {
            Debug.LogWarning("SculptureDisolve: No target Renderer found on child. Please assign or set childName.");
        }
    }

    void Update()
    {
        if (player == null || runtimeMaterial == null || targetRenderer == null) return;

        float dist = Vector3.Distance(player.position, transform.position);
        float target = dist <= activationDistance ? 1f : 0f;

        // animate value
        currentAmount = Mathf.MoveTowards(currentAmount, target, dissolveSpeed * Time.deltaTime);

        if (runtimeMaterial.HasProperty(dissolveProperty))
        {
            runtimeMaterial.SetFloat(dissolveProperty, currentAmount);
        }
    }

    void OnDisable()
    {
        // restore original material to shared material to avoid leaving instantiated material in editor
        if (targetRenderer != null && originalMaterial != null)
        {
            targetRenderer.sharedMaterial = originalMaterial;
        }
    }

    // utility: find child by name recursively
    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
