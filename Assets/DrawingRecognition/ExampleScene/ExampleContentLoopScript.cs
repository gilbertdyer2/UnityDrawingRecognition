using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleContentLoopScript : MonoBehaviour
{
    [SerializeField] private DrawingRecognition drawingRecognition;

    void Start()
    {

    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.C)) {
            drawingRecognition.ClearDrawing();
            Debug.Log("Cleared Canvas");
        }
        if(Input.GetKeyDown(KeyCode.F)) {
            drawingRecognition.GetMatch(); // Prints to debug log automatically
        }
    }
}

