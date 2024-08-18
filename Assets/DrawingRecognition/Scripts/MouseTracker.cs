using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/*
DrawingRecognition layers the methods contained here for interfacing functionality. Unless there are specific requirements 
for your game or drawing capture doesn't work/captures incorrect coordinates, it is not necessary to call directly from this 
class or modify it in any way. 
*/

public class MouseTracker : MonoBehaviour
{
    public Camera mainCamera;
    public LineRenderer lineRenderer;
    private float startWidth;
    private float endWidth;

    public List<Vector2> points; // Stores points in drawing
    public List<Vector3> worldPoints; // Stores same points, with extra points for visual line segmentation
    [HideInInspector] public int DuckPointZValue = 0; // Z position of lineRenderer points
    [SerializeField] private int drawDistance; // Min distance from last point in screen pixels before a new point is placed
    [SerializeField] private int overlapDistance; // Radius from any point where no points can be placed, to prevent overlap
    
    private bool mouseLifted; // detects whether mouse0 has been lifted since last point was added (supports line segmentation)
    
    [HideInInspector]public Collider2D drawSurface; // DrawSurface collider; drawing is possible within bounds

    // Determines whether to check for mouse0 down to update line
    // True by default. Toggle to false if using a specified drawing space on-screen with borders
    [HideInInspector]public bool shouldCheckControls; // Use this field to disable drawing based on conditions in other scripts (e.g. typefield is active)

    [HideInInspector]public int precision; // Updated by drawingRecognition when precision is changed. Used to pass bitmap to drawingRecognition
    
    void Awake() {
        // Create empty Vector arrs for drawing
        points = new List<Vector2>();
        worldPoints = new List<Vector3>();
        
        // Set lineRenderer params
        drawDistance = 2;
        overlapDistance = 10;
        lineRenderer.positionCount = 0;
        lineRenderer.SetColors(Color.black, Color.black);
        startWidth = lineRenderer.startWidth;
        endWidth = lineRenderer.endWidth;

        mouseLifted = true;
        shouldCheckControls = true;
    }
    
    void Update() {    
        if (!shouldCheckControls || !WithinDrawArea() ) {
            return;
        }
        // Draw controls
        if(Input.GetKey(KeyCode.Mouse0)) {
            DrawPoint();
        } 
        else if (Input.GetKeyUp(KeyCode.Mouse0)) {
            mouseLifted = true;
        }   
    }   

    #region Drawing Controls

    void DrawPoint() {
        Vector2 mousePos = Input.mousePosition;
        
        // Store point for LineRenderer visual
        Vector3 worldMousePos = mainCamera.ScreenToWorldPoint(mousePos);
        worldMousePos.z = 0;

        // If point isn't close to any previously drawn points (logic in ShouldAddPoint), add new point
        if (ShouldAddPoint(mousePos)) {
            points.Add(mousePos);

            // If mouse has been lifted, a gap may exist between cur mouse position and other points, 
            // so create 2 extra points ducked behind the background to mask gap
            if (mouseLifted && points.Count != 1) {

                Vector3 behindCurPt = worldPoints[worldPoints.Count - 1];
                behindCurPt.z = DuckPointZValue;
                worldPoints.Add(behindCurPt);

                Vector3 behindNextPt = worldMousePos;
                behindNextPt.z = DuckPointZValue;
                worldPoints.Add(behindNextPt);
        
                mouseLifted = false;
            }
            // After adding ducked points, add the original point as normal
            worldPoints.Add(worldMousePos); 
        }
        UpdateLine(); // Pass updates to lineRenderer for display
    }

    #endregion Drawing Controls

    #region Drawing Helpers

    // Sets the draw surface based on a collision2D component
    //      - If null is passed, enables drawing anywhere on-screen (may cause overlap with UI)
    public void SetDrawSurface(Collider2D collision2D) {
        if (collision2D != null) {
            drawSurface = collision2D;
        } 
        else {
            drawSurface = null;
        }
    }

    // Shows line visuals
    public void ShowDrawing() {
        // Set back to stored initial widths
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;

        UpdateLine();
    }
    // Hides line visuals
    public void HideDrawing() {
        // Set widths to 0
        lineRenderer.startWidth = 0f;
        lineRenderer.endWidth = 0f;
    }

    // Update lineRenderer points, if enabled
    private void UpdateLine() {
        if (lineRenderer.enabled) {
            lineRenderer.positionCount = worldPoints.Count;
            lineRenderer.SetPositions(worldPoints.ToArray());
        }
    }

    // Returns whether mouse is within the defined drawing area
    private bool WithinDrawArea() {
        if (drawSurface == null) { 
            return true; // True if drawSurface is not set
        }

        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition); 
        mousePosition.z = drawSurface.bounds.center.z; // Set to same Z value as collider
        if (drawSurface.bounds.Contains(mousePosition)) {
            return true;
        }
        else {
            return false;
        }
    }

    // When called, denotes whether a new point should be added, based on mouse distance
    // Also prevents overlap of previous points
    private bool ShouldAddPoint(Vector2 mousePos) {
        // If first index, add point
        if (points.Count == 0) {
            return true;
        }

        // Initial condition: far enough away from last point to prevent unnessesary search 
        if (Vector2.Distance(mousePos, points[points.Count - 1]) < drawDistance) {
            return false;
        }
        // If far away from last, check previous points to prevent overlap, 
        // not including last drawn (points[curIndex])

        // If mouse has been lifted, lower tolerance needed to create new point
        int overlapTolerance = overlapDistance;
        if (mouseLifted) 
            overlapTolerance = drawDistance;
        
        for (int i = 0; i < points.Count - 1; i++) {
            Vector3 vect = points[i];
            if (Vector3.Distance(mousePos, vect) < overlapTolerance) {
                return false;
            }
        }
        
        // If no previous points are close enough, draw new
        return true;
    }

    // Clears lineRenderer and resets local stored Vector Arrays 
    public void ClearDrawing() {
        points = new List<Vector2>();
        worldPoints = new List<Vector3>();
        mouseLifted = true;

        UpdateLine();
    }

    #endregion Drawing Helpers

    #region Character Display

    // Displays a given character by replacing drawing with its params
    //      - Also clears current data when replacing
    public void DisplayCharacter(Character character) {
        ClearDrawing();
        Bitmap bitmap = character.bitmap;
        // Make copy of character's points and display with lineRenderer
        points = new List<Vector2>(bitmap.points);
        worldPoints = new List<Vector3>(bitmap.worldPoints);

        UpdateLine();
    }


    #endregion Character Display

    // Returns current bitmap representation of drawing, initialized with an input precision/width value
    //      - Recommended to 
    public Bitmap GetBitmap() {
        return new Bitmap(points, worldPoints, precision);
    }
    
}
