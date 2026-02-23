using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

public class IntersectionNode : MonoBehaviour
{

    [SerializeField] List<SplineContainer> outgoingSplines;
    [SerializeField] List<SplineContainer> incomingSplines;
    [SerializeField] List<CarPathFollower> carsOnNode;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
