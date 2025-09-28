using System.Collections.Generic;
using UnityEngine;

public class PortalRay : MonoBehaviour
{

    public Transform finalDestination;

    public int dotsCount;

    public float dispersion;
    public float frecuency;

    private LineRenderer line;
    private float time = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = dotsCount;
    }


    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;

        if (time > frecuency)
        {
            RefreshDots(this.line);
            time = 0f;
        }
    }

    private void RefreshDots(LineRenderer line)
    {
        List<Vector3> points = InterpolationDots(Vector3.zero, finalDestination.localPosition, dotsCount);
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }

    private List<Vector3> InterpolationDots(Vector3 start, Vector3 end, int totalPoints)
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < totalPoints; i++)
        {
            points.Add(Vector3.Lerp(start, end, (float)i / totalPoints) + RandomDisplacement());
        }

        return points;
    }

    private Vector3 RandomDisplacement()
    {
        return Random.insideUnitSphere.normalized * Random.Range(0f, dispersion);
    }
}
