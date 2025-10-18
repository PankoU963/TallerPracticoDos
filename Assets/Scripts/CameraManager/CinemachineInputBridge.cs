using UnityEngine;
using UnityEngine.InputSystem;
#if CINEMACHINE_INSTALLED
using Cinemachine;
#endif

[DisallowMultipleComponent]
public class CinemachineInputBridge : MonoBehaviour
{
    [Tooltip("Virtual Camera to control. If empty, the component's CinemachineVirtualCamera on the same GameObject will be used.")]
    public UnityEngine.Object vcamObject; // stored as Object to avoid hard compile dependency if Cinemachine isn't installed

    [Header("Input")]
    public float sensitivityX = 2f;
    public float sensitivityY = 2f;
    public bool invertY = false;

    PlayerMovement inputActions;

    // runtime references (set when Cinemachine is present)
    private object pov = null;

    void Awake()
    {
        inputActions = new PlayerMovement();

        // try to find CinemachinePOV if Cinemachine is available
        TryCachePOV();
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
        inputActions.Dispose();
    }

    void Update()
    {
        // read mouse/look action
        Vector2 delta = inputActions.Actions.Mouse.ReadValue<Vector2>();

        // If Cinemachine POV is available, write the values directly to its axes
        if (pov != null)
        {
            WritePOV(pov, delta);
        }
        else
        {
            // Nothing else to do here. If you want non-Cinemachine fallback, implement it elsewhere.
        }
    }

    void TryCachePOV()
    {
        if (vcamObject == null) return;
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        System.Type povType = null;
        foreach (var asm in assemblies)
        {
            povType = asm.GetType("Cinemachine.CinemachinePOV");
            if (povType != null) break;
        }
        if (povType == null) return;

        var go = vcamObject as GameObject;
        if (go == null && vcamObject is Component comp) go = comp.gameObject;
        if (go == null) return;

        var compPOV = go.GetComponent(povType);
        if (compPOV == null) return;
        pov = compPOV;
    }

    /// <summary>
    /// Initialize the POV axes and the vcam transform to match a given camera transform (world yaw/pitch).
    /// Call this before enabling the vcam to avoid jumps when switching.
    /// </summary>
    public void InitializeFromCamera(Transform camT)
    {
        if (camT == null) return;
        TryCachePOV();
        try
        {
            // set the vcam/gameobject transform to camera transform for smooth starting pose
            var go = pov as UnityEngine.Object;
            // pov is a Component; get its gameObject
            var povComp = pov as UnityEngine.Component;
            if (povComp != null && povComp.gameObject != null)
            {
                povComp.gameObject.transform.position = camT.position;
                povComp.gameObject.transform.rotation = camT.rotation;
            }

            if (pov == null) return;
            var povType = pov.GetType();
            var hField = povType.GetField("m_HorizontalAxis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var vField = povType.GetField("m_VerticalAxis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hField == null || vField == null) return;

            var hAxis = hField.GetValue(pov);
            var vAxis = vField.GetValue(pov);
            if (hAxis == null || vAxis == null) return;

            var axisType = hAxis.GetType();
            var valField = axisType.GetField("Value") ?? axisType.GetField("m_Value");
            if (valField == null) return;

            // extract yaw and pitch from cam rotation
            float yaw = camT.eulerAngles.y;
            float pitch = camT.eulerAngles.x;

            valField.SetValue(hAxis, yaw);
            valField.SetValue(vAxis, pitch);
        }
        catch { /* best-effort */ }
    }

    void WritePOV(object povInstance, Vector2 delta)
    {
        try
        {
            var povType = povInstance.GetType();
            var hField = povType.GetField("m_HorizontalAxis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var vField = povType.GetField("m_VerticalAxis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hField == null || vField == null) return;

            var hAxis = hField.GetValue(povInstance);
            var vAxis = vField.GetValue(povInstance);
            if (hAxis == null || vAxis == null) return;

            var axisType = hAxis.GetType();
            var valField = axisType.GetField("Value") ?? axisType.GetField("m_Value");
            if (valField == null) return;

            float h = (float)valField.GetValue(hAxis);
            float v = (float)valField.GetValue(vAxis);

            h += delta.x * sensitivityX;
            v += (invertY ? delta.y : -delta.y) * sensitivityY;

            valField.SetValue(hAxis, h);
            valField.SetValue(vAxis, v);
        }
        catch { /* best-effort only */ }
    }

    // Expose current yaw/pitch for external use (if pov cached)
    public float CurrentYaw
    {
        get
        {
            if (pov == null) return 0f;
            try
            {
                var povType = pov.GetType();
                var hField = povType.GetField("m_HorizontalAxis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hField == null) return 0f;
                var hAxis = hField.GetValue(pov);
                var axisType = hAxis.GetType();
                var valField = axisType.GetField("Value") ?? axisType.GetField("m_Value");
                if (valField == null) return 0f;
                return (float)valField.GetValue(hAxis);
            }
            catch { return 0f; }
        }
    }

    public float CurrentPitch
    {
        get
        {
            if (pov == null) return 0f;
            try
            {
                var povType = pov.GetType();
                var vField = povType.GetField("m_VerticalAxis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (vField == null) return 0f;
                var vAxis = vField.GetValue(pov);
                var axisType = vAxis.GetType();
                var valField = axisType.GetField("Value") ?? axisType.GetField("m_Value");
                if (valField == null) return 0f;
                return (float)valField.GetValue(vAxis);
            }
            catch { return 0f; }
        }
    }
}
