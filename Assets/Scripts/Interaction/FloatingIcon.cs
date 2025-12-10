using UnityEngine;

public class FloatingIcon : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField, Tooltip("Vertical movement amplitude in units.")]
    private float floatAmplitude = 0.25f;

    [SerializeField, Tooltip("Vertical movement frequency (cycles per second).")]
    private float floatFrequency = 1f;

    [Header("Rotation")]
    [SerializeField, Tooltip("Rotation speed in degrees per second around each axis.")]
    private Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);

    [SerializeField, Tooltip("If true uses localPosition for the float, otherwise uses world position.")]
    private bool useLocalPosition = true;

    // internal state
    private Vector3 startPosition;
    private float timeOffset;

    // Cache the start position so the object never drifts away from its original spot
    void Start()
    {
        startPosition = useLocalPosition ? transform.localPosition : transform.position;
        // randomize phase so multiple icons don't all bob in unison
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    // Update is called once per frame
    void Update()
    {
        // compute vertical offset (sin wave). frequency is cycles per second, so multiply by 2*pi
        float y = Mathf.Sin((Time.time + timeOffset) * floatFrequency * Mathf.PI * 2f) * floatAmplitude;

        if (useLocalPosition)
            transform.localPosition = startPosition + Vector3.up * y;
        else
            transform.position = startPosition + Vector3.up * y;

        // rotate around local axes; multiply by deltaTime to make it frame-rate independent
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }
}
