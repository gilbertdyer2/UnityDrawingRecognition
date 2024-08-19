using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/*
A Bitmap creates & stores map representations of input drawings, used for comparison           
    - Also holds points specifically made for lineRenderer instances that prevent visual artifacts from large jumps between points
    (These points are handled by the MouseTracker script)


There are 4 map representations (Explanation with visuals on Itch.io/Github repo):
"GridMap": A grid that borders the drawing and is divided into equal-area cells. Each cell value = (# of points cell contains / Total # of points)

"CircleMap": Similar to GridMap, but in Circular form. A circle centered around the geometric median of all points, with a radius equal to the distance 
    to the furthest point. The circle is divided into equally spaced rings, which are further divided into quadrants. Each quadrant is treated as a cell,
    and given the same value (# of points cell contains / Total # of points).
        - Indexed as circMap[ring #][quadrant #]
    
"FlatMap" (Horizontal & Vertical): The FlatMap "flattens" 2D points into a 1D array representing slices of the drawing. The drawing is simplified into 
    horizontal or vertical line segments, and each point of the array denotes the density of lines in a slice of the drawing.

****Public Interface

**Constructors:

Bitmap(List<Vector2> points, List<Vector3> worldPoints, int precision)
    - Initializes a bitmap with a CircleMap, GridMap, Horizonal Flatmap, and Vertical FlatMap
    - Precision denotes the width & height of the GridMap, # of rings in the CircleMap, and square root of the # of indices of each FlatMap
        - e.g. precision of 5: 5x5 GridMap, 5 rings in CircleMap, 25 indices in FlatMap
        - Interchangeable with "width" param 

Bitmap(List<Vector2> points, Camera camera, int precision)
    - Overload for initializing without a specific worldPoint list for lineRenderer; Sets worldPoints to points
    
**Methods:

*Comparisons:

(For all methods: A lower returned score means a closer match)
float GridCompareTo(Bitmap bitmapRef)
    - Returns a comparison score of this GridMap compared to the input bitmap's GridMap

float CircCompareTo(Bitmap bitmapRef)
    - Returns a comparison score of this CircleMap compared to the input bitmap's CircleMap, using the geometric median center

float CircCompareTo(Bitmap bitmapRef, bool useMedian) 
    - Returns a comparison score of this CircleMap compared to the input bitmap's CircleMap
    - Based on 'useMedian', uses the geometric median (true) or center of mass (false) as the center

public float FlatMapCompareTo(Bitmap bitmapRef, bool horizontal)
    - Returns a score based on the difference between this bitmap's FlatMap and the input's
    - Based on 'horizontal', compares the horizontal flatmaps (true) or vertical flatmaps (false)


*Parameter settings
void SetPrecision(int precision)
    - Updates the precision of all maps to a specified value


*Static methods
static Vector2 GetCenterOfMass(List<Vector2> input)
    - Returns the center of mass (average of x and y coords) of a list of Vector2 points

static Vector2 GetGeoMedian(List<Vector2> input)
    - Returns an approximation of the geometric median point of a list of Vector2 points
    - Direct implementation of Weiszfeld's algorithm

static float[] getBorders(List<Vector2> input)
    - Returns an array that denotes the borders of a list of Vector2 points: {left, right, top, bottom}

*/

public class Bitmap
{
    // Static params 

    // Minimum width and height of bitmaps (for handling thin vertical and horizontal characters respectively)
    //      - Uses world-based coordinates; If the input coordinate widths are generally less than these set values,
    //        you may need to adjust these. 
    //      - For reference, default values of '50,50,25' are based on input coordinates ranging from x:0-1200, y:0-800
    public readonly float MIN_BITMAP_WIDTH = 50; 
    public readonly float MIN_BITMAP_HEIGHT = 50; 
    public readonly float MIN_BITMAP_RADIUS = 25; // Min Radius for circles, halved from width

    // Non-static params

    public int width; // Dimensions of maps: grid - width*width, circle - width*4 quadrants, flatmap - (width*width) 1D array
    public List<Vector2> points;
    public List<Vector3> worldPoints; // for drawing reconstruction

    // Map Representations
    public float[,] gridMap; 
    public float[][] circMap; // (Center of Mass) 
    public float[][] medCircMap; // (Geometric Median)
    public int[] flatMapHorizontal;
    public int[] flatMapVertical;

    // Border values (leftmost val, rightmost val, etc) 
    public float left; 
    public float right;
    public float top;
    public float bottom;
    

    // // Bitmap constructor: includes a list of worldPoints for line drawing
    // public Bitmap(List<Vector2> points, List<Vector3> worldPoints, float left, float right, float top, float bottom, int curIndex, int curDrawIndex, int width) {
    //     this.left = left;
    //     this.right = right;
    //     this.top = top;
    //     this.bottom = bottom;
    //     this.width = width;
    //     // Bitmap setup
    //     this.points = points;
    //     this.worldPoints = worldPoints;

    //     gridMap = VecToGridMap(points);
    //     // CircleMap setup
    //     circMap = VecToCircMap(points, false);
    //     medCircMap = VecToCircMap(points, true); // median point
        
    //     // Flatmap setup
    //     flatMapHorizontal = VecToFlatMap(points, true);
    //     flatMapVertical = VecToFlatMap(points, false);
    // }

    public Bitmap(List<Vector2> points, List<Vector3> worldPoints, int precision) {
        width = precision;
        this.points = points;
        this.worldPoints = worldPoints;
        // Get the borders of the points, 
        float[] borders = getBorders(points);
        left = borders[0];
        right = borders[1];
        top = borders[2];
        bottom = borders[3];

        // Map representation setup
        gridMap = VecToGridMap(points);
        circMap = VecToCircMap(points, false); // based around center-of-mass 
        medCircMap = VecToCircMap(points, true); // based around geometric median point
        flatMapHorizontal = VecToFlatMap(points, true); // horizontal
        flatMapVertical = VecToFlatMap(points, false); // vertical
    }
    // Overload for no worldPoints (lineRenderer points gathered by MouseTracker); 
    //      - Sets points equal to worldPoints, will result in spaced points always being connected rather than starting a new segment
    public Bitmap(List<Vector2> points, Camera camera, int precision) {
        width = precision;
        this.points = points;

        // Convert points to Vector3 & pass to worldPoints
        List<Vector3> ptsToVec3 = new List<Vector3>();
        foreach (Vector2 vec2 in points) {
            ptsToVec3.Add(camera.ScreenToWorldPoint(vec2)); // Conv from screen -> world
        }
        worldPoints = new List<Vector3>(ptsToVec3);

        // Get the borders of the points, 
        float[] borders = getBorders(points);
        left = borders[0];
        right = borders[1];
        top = borders[2];
        bottom = borders[3];

        // Map representation setup
        gridMap = VecToGridMap(points);
        circMap = VecToCircMap(points, false); // based around center-of-mass 
        medCircMap = VecToCircMap(points, true); // based around geometric median point
        flatMapHorizontal = VecToFlatMap(points, true); // horizontal
        flatMapVertical = VecToFlatMap(points, false); // vertical
    }

    
    // Returns float of match likelihood, based on mean squared error.  
    public float GridCompareTo(Bitmap bitmapRef) {
        if (bitmapRef == null || this.width != bitmapRef.width) 
            return 100; // Return arbitrality large val

        // Tracks total difference between each point
        float totalSqError = 0; 
        for (int i = 0; i < width; i++) {
            for (int k = 0; k < width; k++) {
                float diff = Math.Abs(gridMap[i,k] - bitmapRef.gridMap[i,k]);
                totalSqError += diff * diff;
            }
        }
        float meanSqError = totalSqError / (width * width);
        
        return meanSqError;
    }

    // Returns a score based on comparison of 2 bitmaps' circleMaps,
    //      - Includes all 4 quadrants; CircComparison supports comparison of ranges of quadrants
    //      - By default uses the circleMap based around the geometric median. 
    //      Use overload (funct. below) to choose between center of mass and median
    public float CircCompareTo(Bitmap bitmapRef) {
        if (bitmapRef == null || this.width != bitmapRef.width)
            return 100; // Return arbitrality large val if invalid

        return CircComparison(bitmapRef, 1, 4, true);
    }
    // Overload for choosing between center of mass and median based circleMaps
    public float CircCompareTo(Bitmap bitmapRef, bool useMedian) {
        if (bitmapRef == null || this.width != bitmapRef.width)
            return 100; // Return arbitrality large val if invalid

        return CircComparison(bitmapRef, 1, 4, useMedian);
    }
    // Does a CircComparison on a range of quadrants, inclusive, to support directional comparison
    //  - Non zero-based quadrants! e.g. Quadrant 1 (+,+) = 1, Quadrant 3 (-,-) = 3
    private float CircComparison(Bitmap bitmapRef, int startQuadrant, int endQuadrant, bool useMedian) {
        // Tracks total difference between each point
        float totalSqError = 0; 
        float[][] map, mapRef;
        
        if (useMedian) {
            map = medCircMap;
            mapRef = bitmapRef.medCircMap;
        }
        else {
            map = circMap;
            mapRef = bitmapRef.circMap;
        }

        for (int i = 0; i < width; i++) {
            for (int k = startQuadrant - 1; k < endQuadrant; k++) {             
                float diff = Math.Abs(map[i][k] - mapRef[i][k]);
                totalSqError += diff * diff;
            }
        }
        float meanSqError = totalSqError / (width * (endQuadrant - startQuadrant + 1)); // Total cells is now # of rings (width) * # quadrants (4)
        
        return meanSqError;
    }

    #region FlatMapComparison
    // Returns a score based on differences between 2 Bitmaps' FlatMap values, lower score means more similar
    public float FlatMapCompareTo(Bitmap bitmapRef, bool horizontal) {
        if (bitmapRef == null)
            return  100; // Return arbitrality large val

        if (this.width != bitmapRef.width) {
            Debug.Log("Error During FlatMap Comparison!: this.width (" + this.width + ") did not match bitmapRef.width (" + bitmapRef.width + ")" );
            return 100;
        }
        // Setup Horizontal / Vertical switch
        int[] flatMapRef;
        int[] flatMap;
        if (horizontal) {
            flatMapRef = bitmapRef.flatMapHorizontal;
            flatMap = flatMapHorizontal;
        }
        else {
            flatMapRef = bitmapRef.flatMapVertical;
            flatMap = flatMapVertical;
        }
        // Compares central values (not including direct edges)
        // Takes best score out of multiple shifts
        float diff = FlatMapCenterComparison(flatMap, flatMapRef, width);

        //If most frequent values are not equal, increase difference (Unused, try uncommenting if FlatMap comparisons are egregiously inaccurate)
        // int mostFreqVal = FindMostFrequent(flatMap);
        // int mostFreqValRef = FindMostFrequent(flatMapRef);
        // if (mostFreqVal != mostFreqValRef) {
        //     diff += 0.5f;
        // }

        return diff;
    }
    // Compares 2 FlatMap arrays & calculates a difference score, accounting for possible shifts
    //      - Only inner indeces are compared; values on edges (idx 0, length - 1) are slightly unreliable 
    //      - Width input is how many shifts left and right are checked
    private float FlatMapCenterComparison(int[] flatMap, int[] flatMapRef, int radius) {
        float lowestDiff = 0;
        float diff = 0; // diff tracks the percent difference between maps; incremented if 2 compared indeces aren't equal, 

        // central comparison
        for (int i = 1; i < flatMap.Length - 1; i++) {
            if (flatMap[i] != flatMapRef[i]) {
                diff += 1 / ((float)flatMap.Length); // Fraction of central points
            }
        }
        lowestDiff = diff;
        // Left comparisons
        for (int n = 1; n <= radius; n++) {
            diff = 0;
            for (int i = n; i < flatMap.Length; i++) {
                int totalCompares = flatMap.Length - n;

                if (flatMap[i] != flatMapRef[i - n]) {
                    diff += 1 / ((float)totalCompares); // Fraction of central points
                }
            }
            if (diff < lowestDiff) {
                lowestDiff = diff;
            }
        }
        // Right comparisons
        for (int n = 1; n <= radius; n++) {
            diff = 0;
            for (int i = 0; i < flatMap.Length - n; i++) {
                int totalCompares = flatMap.Length - n;
                if (flatMap[i] != flatMapRef[i + n]) {
                    diff += 1 / ((float)totalCompares); // Fraction of central points
                }
            }
            if (diff < lowestDiff) {
                lowestDiff = diff;
            }
        }

        return lowestDiff;
    }

    // Helper method to find the most frequent element in a flatMap array, uses HashMap frequency
    private int FindMostFrequent(int[] map) {
        Dictionary<int,int> freq = new Dictionary<int,int>();
        // Hash all values to a frequency map 
        foreach (int i in map) {
            if (freq.ContainsKey(i)) {
                freq[i]++;  
            }
            else {
                freq.Add(i, 1);
            }
        }
        // Search KVPs to find most frequent element
        int maxCt = 0;
        int result = -1;
        foreach (KeyValuePair<int,int> kvp in freq) {
            if (maxCt < kvp.Value) {
                maxCt = kvp.Value;
                result = kvp.Key;
            }
        }
        return result;
    }

    #endregion FlatMapComparison

    #region GridBitMap
    // Converts a list of Vector2 points to a GridMap
    private float[,] VecToGridMap(List<Vector2> input) {
        if (input == null || input.Count == 0) {
            return new float[width, width]; // Return empty
        }
            
        int mapHeight = (int)top - (int)bottom;
        int mapWidth = (int)right - (int)left;
        // Round up to minimum height and width if needed
        if (mapHeight < MIN_BITMAP_HEIGHT) {
            mapHeight = (int)MIN_BITMAP_HEIGHT;
        }
        if (mapWidth < MIN_BITMAP_WIDTH) {
            mapWidth = (int)MIN_BITMAP_WIDTH;
            float newCenter = (left + right) / 2;
            left = newCenter - (MIN_BITMAP_WIDTH / 2);
            right = newCenter + (MIN_BITMAP_WIDTH / 2);
        }

        float[,] bitmap = new float[width, width];

        // For each point, get its row and column, then increment bitmap[row,col] by 1 to store the number of points in each cell
        for (int i = 0; i < input.Count; i++) {
            Vector2 vect = input[i];
            
            int row = (int) ((vect.y - bottom) / ((float) mapHeight / width)); // row to be placed on, i.e. true bottom / total row count
            int col = (int) ((vect.x - left) / ((float) mapWidth / width));
            // Catch edge cases
            if (row == width)
                row = width - 1;
            if (col == width)
                col = width - 1; 
            if (row < 0) // Edge case for 1-point lists
                row = width / 2; // Place in middle
            if (col < 0)
                col = width / 2;
            
            bitmap[row,col] += 1;
        }
        // Normalize the map by dividing each cell by the total number of points
        //      - E.g. a ratio of (# of points in this cell / total # of points)
        for (int i = 0; i < width; i++) {
            for (int k = 0; k < width; k++) {
                bitmap[i,k] = bitmap[i,k] / input.Count; 
            }
        }

        return bitmap;
    }
    

    #endregion GridBitMap

    #region CircleBitMap
    // Creates a circle bitmap representation of a Vector2 array, 
    //      - Inspired by Visual Model of: https://www.youtube.com/watch?v=cAkklvGE5io  @ 1:10, along with other radial-based image processing methods
    private float[][] VecToCircMap(List<Vector2> input, bool useMedian) {
        float[][] bitmap = new float[width][]; // bitmap array, for return
        for(int i = 0; i < width; i++) {
            bitmap[i] = new float[4]; // Initializes quadrants of rings
        }
        // Return empty array if input is empty or invalid
        if (input == null || input.Count == 0) {
            return bitmap; 
        }
            
        // Get central point; Either the Center of Mass, or Geometric Median based on input 'useMedian'
        Vector2 center;
        if (useMedian) {
            center = GetGeoMedian(input);
        }
        else {
            center = GetCenterOfMass(input);
        }
        
        // Get the furthest point from the center, to use as radius of circle
        Vector2 furthestPt;
        float maxDist = 0;
        for (int i = 0; i < input.Count; i++) {
            // Track furthest point & maximum distance found
            Vector2 vect = input[i];
            float dist = Vector2.Distance(vect, center);

            if (Vector2.Distance(vect, center) > maxDist) {
                furthestPt = vect;
                maxDist = Vector2.Distance(vect, center);
            }
        }
        // If max distance below min radius, set to min 
        if (maxDist < MIN_BITMAP_RADIUS) {
            maxDist = MIN_BITMAP_RADIUS;
        }
        // The length of each circular ring section. 
        //      - Other approaches use a logarithmic approach rather than equal segments, untested in this model
        float innerRadiusSeparation = maxDist / width; 

        // Now, get the number of points in each cell of the circle map
        //      - Structure is a 2D array { ring1[], ring2[], ring3[], ring4[] }
        //      - Rings are separated into the 4 quadrants around CoM, where ringN[] = {1st quadrant, 2nd quad, 3rd quad, 4th quad}
        for (int i = 0; i < input.Count; i++) {
            Vector2 vect = input[i];
            float dist = Vector2.Distance(vect, center);
            int ringIndex = (int)(dist / innerRadiusSeparation); 
            int quadrantIndex = GetQuadrant(vect, center);
            // If ring index is too large (point on edge), decrease by 1
            if (ringIndex == width)
                ringIndex -= 1;

            bitmap[ringIndex][quadrantIndex]++;
        }
        // Normalize the map by dividing each cell by the total number of points
        for (int i = 0; i < width; i++) {
            for (int k = 0; k < 4; k++) {
                bitmap[i][k] = bitmap[i][k] / input.Count;
            }
        }

        return bitmap;
    }
    // Attached helper method: Gets quadrant of a Vector2 'point', about a given centerpoint
    // Quadrants staggered by 1 (e.g. quadrant 1 = 0, quadrant 2 = 1) for array indexing 
    private int GetQuadrant(Vector2 point, Vector2 center) {
        Vector2 relativeDist = point - center;
        bool xPositive = relativeDist.x >= 0;
        bool yPositive = relativeDist.y >= 0;
    
        switch(xPositive, yPositive) {
            case(true, true): 
                return 0;
            case(true, false): 
                return 1;
            case(false, false): 
                return 2;
            case(false, true):
                return 3;
        }
    }

    public static Vector2 GetCenterOfMass(List<Vector2> input) {
        Vector2 com = Vector2.zero;
        // Center of mass calculation: Take the average x and y value of all points
        for (int i = 0; i < input.Count; i++) {
            Vector2 vect = input[i];
            
            com += vect;
        }
        com.x = com.x / input.Count;
        com.y = com.y / input.Count;

        return com;
    }

    // Implementation of Weiszfeld's algorithm for approximating Geometric Median
    //      - See: https://en.wikipedia.org/wiki/Geometric_median
    public static Vector2 GetGeoMedian(List<Vector2> input) {
        if (input.Count < 2) {
            return GetCenterOfMass(input); // If drawing is not made up of enough points, use center instead
        }
        float tolerance = 0.001f; // convergence tolerance
        int maxIterations = 500;

        // start guess from center of mass
        Vector2 curEstimate = GetCenterOfMass(input);

        for (int i = 0; i < maxIterations; i++) {
            float xNumerator = 0;
            float yNumerator = 0;
            float denominator = 0;
            // Iterate through each point, then update estimate
            for (int k = 0; k < input.Count; k++) {
                float distance = Vector2.Distance(curEstimate, input[k]);

                if (distance != 0) { // Avoid division by 0, just in case
                    xNumerator += input[k].x / distance;
                    yNumerator += input[k].y / distance;
                    denominator += 1f / distance; // In Weiszfeld's algorithm, used as a weighing metric to value closer points more
                } 
            }
            Vector2 nextEstimate = Vector2.zero;
            if (denominator != 0) {
                nextEstimate.x = xNumerator / denominator;
                nextEstimate.y = yNumerator / denominator;
            }
            // Stop if below tolerance (difference between current estimate and next reaches negligible value; marginal gains from more iterations)
            if (Vector2.Distance(curEstimate, nextEstimate) < tolerance) {
                return nextEstimate;
            }
            // If not below tolerance, set curEstimate to next & continue iteration
            curEstimate = nextEstimate;
        }
        // Return current estimate as the final estimate if max # of iterations is reached
        // Debug.Log($"Ran: {maxIterations} times, found: ({curEstimate.x}, {curEstimate.y}) vs CoM: ({GetCenterOfMass(input).x}, {GetCenterOfMass(input).y})");
        return curEstimate;
    }


    #endregion CircleBitMap

    #region FlatMap
    // Returns array of horizontal Line Segments, for use w/ horizontal FlatMap
    //  - Whenever a point changes horizontally over some threshold, creates a new line segment at prev point
    //  - LineMap prevents horizontal density from double counting points that are part of the same segment
    
    // Returns 1d Array, denotes horizontal/vertical density 
    private int[] VecToFlatMap(List<Vector2> input, bool horizontal) {
        if (input == null || input.Count < 2)
            return new int[width*width]; // Return empty
            
        // Returning map as the flatMap, Length = width*width 
        int[] map = new int[width * width];

        float mapWidth; // Width of horizontal/vertical space in standard coordinate units
        float[][] lineMap;
        int floor; // floor of drawing for indexing during iteration
        // mapWidth changes values for horizontal vs vertical
        if (horizontal) {
            mapWidth = right - left;
            lineMap = VecToLineHorizontal(input);
            floor = (int)left;
        }
        else {
            mapWidth = top - bottom;
            lineMap = VecToLineVertical(input);
            floor = (int)bottom;
        }
        
        // Iterate through line segments at each index, incrementing by 1 if within bounds
        for (int i = 0; i < map.Length; i++) {
            int idxStart = i * ((int) mapWidth / map.Length) + floor;
            int idxEnd = (i + 1) * ((int) mapWidth / map.Length) + floor;

            foreach (float[] line in lineMap) {
                float lineStart = line[0]; // LineStart
                float lineEnd = line[1]; // LineEnd
                // Check if line falls within the current column
                if ((lineStart >= idxStart && lineStart <= idxEnd) || // Start is within cols
                    (lineEnd >= idxStart && lineEnd <= idxEnd) ||     // End is within cols
                    (lineStart <= idxStart && lineEnd >= idxEnd) ||   // Segment spans length of col
                    (lineEnd <= idxStart && lineStart >= idxEnd)) {   // Segment reverse-spans length of col
                    // If so, increment
                    map[i]++;
                }
            }
        }

        return map;
    }

    private float[][] VecToLineHorizontal(List<Vector2> input) {
        // Edge case: empty/less than 3 long
        List<float[]> segments = new List<float[]>();
        Vector2 lineStart = input[0];
        bool leadingRight = false; // Denotes which direction current line is leading to, True = right (positive x)
        // Edge case: input with < 3 points
        if (input.Count <= 3) {
            segments.Add(CreateLineSegment(lineStart.x, input[input.Count - 1].x));
            return segments.ToArray();
        }
        // Look ahead 2 points to determine initial direction 
        if (input[2].x > lineStart.x) {
            leadingRight = true;
        }
        // Loop through, adding a line segment whenever the line changes x direction
        for (int i = 0; i < input.Count - 2; i++) {
            // If next 2 points go in opposite x direction, create new linesegment and add it
            if (leadingRight) {
                // Case: when current trend is leading right - check if next 2 points are less than than current
                if ((input[i + 1].x < input[i].x && input[i + 2].x < input[i].x) ||
                    (Vector2.Distance(input[i], input[i+1]) > (top - bottom) / 6)) { // If large gap btw. points (> 1/3 of height), create new
                    // Add lineSegment, reset start, and flip leadingRight direction 
                    segments.Add(CreateLineSegment(lineStart.x, input[i].x));
                    lineStart = input[i + 1];
                    leadingRight = false;
                }
            }
            else {
                // Case: leading left - next 2 points are greater than current
                if ((input[i + 1].x > input[i].x && input[i + 2].x > input[i].x) ||
                    (Vector2.Distance(input[i], input[i+1]) > (top - bottom) / 6)) { // If large gap btw. points, create new (top - bottom) / 3)

                    segments.Add(CreateLineSegment(lineStart.x, input[i].x));
                    lineStart = input[i + 1];
                    leadingRight = true;
                }
            }
        }
        // Add final segment, for cases where no change in direction
        segments.Add(CreateLineSegment(lineStart.x, input[input.Count - 1].x));
        //Debug.Log("created final lineSeg: (" + lineStart.x + "," + input[lastIdx - 1].x + ")");

        // return segments converted to array
        return segments.ToArray();
    }
    // Returns array of vertical line segments (y coordinates)
    private float[][] VecToLineVertical(List<Vector2> input) {
        // Edge case: empty/less than 3 long
        List<float[]> segments = new List<float[]>();
        Vector2 lineStart = input[0];
        bool leadingUp = false; // Denotes which direction current line is leading to, True = up (positive y)
        // Edge case: input with < 3 points
        if (input.Count <= 3) {
            segments.Add(CreateLineSegment(lineStart.y, input[input.Count - 1].y));
            return segments.ToArray();
        }

        // Look ahead 2 points to determine initial direction 
        if (input[2].y > lineStart.y) {
            leadingUp = true;
        }
        // Loop through, adding a line segment whenever the line changes y direction
        for (int i = 0; i < input.Count - 2; i++) {
            // If next 2 points go in opposite y direction, create new linesegment and add it
            if (leadingUp) {
                // Case: when current trend is leading up - check if next 2 points are less than than current
                if ((input[i + 1].y < input[i].y && input[i + 2].y < input[i].y) ||
                    (Vector2.Distance(input[i], input[i+1]) > (top - bottom) / 6)) { // If large gap btw. points (> 1/3 of height), create new
                    // Add lineSegment, reset start, and flip leadingUp direction 
                    segments.Add(CreateLineSegment(lineStart.y, input[i].y));
                    lineStart = input[i + 1];
                    leadingUp = false;
                }
            }
            else {
                // Case: when leading down - check if next 2 points are greater than current
                if ((input[i + 1].y > input[i].y && input[i + 2].y > input[i].y) ||
                    (Vector2.Distance(input[i], input[i+1]) > (top - bottom) / 6)) { // If large gap btw. points, create new (top - bottom) / 3)

                    segments.Add(CreateLineSegment(lineStart.y, input[i].y));
                    lineStart = input[i + 1];
                    leadingUp = true;
                }
            }
        }
        // Add final segment, for cases where there is no change in direction
        segments.Add(CreateLineSegment(lineStart.y, input[input.Count - 1].y));
        //Debug.Log("created final lineSeg: (" + lineStart.x + "," + input[lastIdx - 1].x + ")");

        // return segments converted to array
        return segments.ToArray();
    }
    // Attached helper: Creates a line segment given a start & end
    private float[] CreateLineSegment(float start, float end) {
        float[] line = new float[2];
        line[0] = start;
        line[1] = end;

        return line;
    }


    #endregion FlatMap
    
    // Changes the precision (equivalent to width) of the bitmap, updating all maps
    public void SetPrecision(int precision) {
        // Set width to input, then refresh map and ratiomap
        width = precision;
        
        gridMap = VecToGridMap(points);
        circMap = VecToCircMap(points, false);
        medCircMap = VecToCircMap(points, true);
        flatMapHorizontal = VecToFlatMap(points, true);
        flatMapVertical = VecToFlatMap(points, false);
    }

    #region Helpers

    // Gathers the borders, or extrema, of an array of Vector2 points
    //      - {leftBorder, rightBorder, topBorder, bottomBorder}
    public static float[] getBorders(List<Vector2> input) {
        // Edge case: no points
        if (input.Count == 0) {
            return new float[4];
        }
        float leftBorder = input[0].x;
        float rightBorder = input[0].x;
        float topBorder = input[0].y;
        float bottomBorder = input[0].y;
        
        foreach (Vector2 vec in input) {
            if (vec.x < leftBorder) {
                leftBorder = vec.x;
            }
            if (vec.x > rightBorder) {
                rightBorder = vec.x;
            }
            if (vec.y > topBorder) {
                topBorder = vec.y;
            }
            if (vec.y < bottomBorder) {
                bottomBorder = vec.y;
            }
        }
        float[] res = {leftBorder, rightBorder, topBorder, bottomBorder};
        return res;
    }


    #endregion Helpers
}
