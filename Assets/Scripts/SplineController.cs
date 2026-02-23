using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

public class SplineController : MonoBehaviour
{

    [SerializeField] List<SplineController> outgoingSplines;
    [SerializeField] List<SplineController> incomingSplines;
    [SerializeField] List<CarPathFollower> carsOnNode;
    public SplineContainer splineContainer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public NodeTransferType TransferToNextSpline(CarPathFollower car)
    {
        if (outgoingSplines.Count == 0) return NodeTransferType.End; // No outgoing splines, can't transfer
        SplineController nextSpline = outgoingSplines[Random.Range(0, outgoingSplines.Count)];
        car.splineController = nextSpline; // Move car to start of next spline
        carsOnNode.Remove(car);
        nextSpline.carsOnNode.Add(car);
        return NodeTransferType.LeftTurn; // Placeholder
    }



    public enum NodeTransferType
{
    End,
    LeftTurn,
    Straight
}

}
