using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

#nullable enable
public class IntersectionNode : MonoBehaviour
{

    [System.NonSerialized] public SplineContainer splineContainer;

    public IntersectionNode? continueNode;
    public IntersectionNode? leftTurnNode;
    public IntersectionNode? rightTurnNode;
    public IntersectionNode? noTurnNode;


    void Awake()
    {
        splineContainer = GetComponent<SplineContainer>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
