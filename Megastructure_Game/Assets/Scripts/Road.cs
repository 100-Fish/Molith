using System.Collections.Generic;
using UnityEngine;

public class Road
{
    public List<Vector3> splinePoints;
    public GameObject roadMeshObject;
    public Mesh roadMesh;
    public int roadID;

    public Road(int id)
    {
        roadID = id;
        splinePoints = new List<Vector3>();
    }

    public void AddPoint(Vector3 point)
    {
        splinePoints.Add(point);
    }

    public void RemoveOldestPoint()
    {
        if (splinePoints.Count > 0)
            splinePoints.RemoveAt(0);
    }
}
