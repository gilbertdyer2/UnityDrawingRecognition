
# Drawing Recognition
[Setup](#setup)
- [Basic Setup](#basic-setup)
- [Creating Custom Libraries and Characters](#creating-custom-libraries-and-characters)

[How it Works](#how-it-works)

[Documentation](#documentation)
- [General Methods](#general-methods)
- [Prefabbed Library Initializers](#prefabbed-character-library-initializers)
- [Debug and Print Methods](#debug-and-print-methods)
- [Overview of Other Classes](#brief-overview-of-other-classes-used-in-drawingrecognition)
## Setup
### Basic Setup
To set up basic recognition functionality with the provided character libraries, you'll need to do the following:
1. Drag your main camera to the 'Camera' field of the MouseTracker script.
2. Set the draw area by dragging a 2D collider to the 'Draw Surface' of the Drawing Recognition script. Alternatively, if this is set to none, drawing will be possible anywhere.
3. If your project has a static 2D backdrop, set the field 'Duck Point Z Value' to a Z value that lies behind the backdrop from the perspective of the camera. This is optional, but gets rid of the connecting lines between separately drawn segments.
4. In a separate script, implement controls to clear the drawing and get matches to it (examples below)

	#### Ex.1 With Hotkeys:
	```c#
	// 'drawingRecognition' - serialized reference to the DrawingRecognition script
	public Character match; // For storing the match
	private void CheckDrawRecControls() {
		if(Input.GetKeyDown(KeyCode.C)) {
			drawingRecognition.ClearDrawing();
		}
		if(Input.GetKeyDown(KeyCode.F)) {
			match = drawingRecognition.GetMatch(); // Stores match & prints its name to debug log 
		}
	}
	```
	#### Ex.2 Clear and GetMatch on left mouse up (simpler control scheme, but supports only single-stroke drawings):
	```c#
	// 'drawingRecognition' - serialized reference to the DrawingRecognition script
	public Character match; // For storing the match
	private void CheckDrawRecControls() {
		if(Input.GetKeyUp(KeyCode.Mouse0)) {
			match = drawingRecognition.GetMatch(); // Stores match & prints name to debug console 
			drawingRecognition.ClearDrawing();
		}
	}
	```
	*(Alternatively, calling GetMatch() on mouse up and implementing ClearDrawing() separately supports multi-stroke drawings and consistently tracks the current match during runtime.)*

After this, you should be able to draw on-screen, clear the drawing, and view what matches are found to your drawings through the debug log's output.

The rest of the setup is mostly dependent on the project you implement this to. From here, you could add your own libraries of custom characters (explained further below), or add support for player-sided custom character creation during runtime through the AddDrawingToLib() method. 


### Creating Custom Libraries and Characters
A large feature this project aims to support is the the ability to store prefabricated libraries of custom drawings/characters that are accessible during runtime. Prefabbed libraries are structured as initializer methods that load their stored characters onto an input character library. You can view the included initializers near the bottom of the 'DrawingRecognition' script for reference.

Before creating a custom library, it is first recommended to set up a hotkey to 'PrintCurCharacter()'. This copies a code snippet to your clipboard that stores the current drawing and initializes it as a character when ran in an initializer. 

Next, you'll need to create an empty initializer method in the DrawingRecognition script to paste this into. (shown below)
```c#
public void InitializerExample(CharacterLibrary charLib) {
	charLib.ClearLibrary(); // Remove if you only want to add characters to the library
	// Paste output of PrintCurCharacter() or PrintCharacterLibrary() below
	
}
```
After pasting your prefabs, you need to create a library setup method and call it in Awake(). This method will handle the initial setup of the character libraries and the adjustable parameters used in comparisons. 

Copy the example below and edit the middle block according to the name of your initializer method and the desired name of your library. 

```c#

// SetupExample() should be called in Awake()
public void SetupExample() {
	libraryList = new List<CharacterLibrary>(); // Initialize libraryList
	
	// Repeat this block for each character library
	CharacterLibrary name = new CharacterLibrary("name"); // Create a new character library
	InitializerExample(name); // Load the prefabbed library onto it
	libraryList.Add(name); // Add the library to the list 
	
	// Set the current library, weight, and precision values (Required)
	currentLib = libraryList[0]; // Set the initial library selected on start
	SetWeights(1.0, 1.0, 1.0, 1.0);
	SetPrecision(3);
}
```
*(InitializerExample() and SetupExample() are both included in the "Default Initializer" region of the DrawingRecognition script to modify, along with a call to SetupExample() in Awake() that has been commented out)*

Lastly, uncheck the script's serialized field 'Use Default Initializer' and call the library setup method in Awake(). After this, your prefabbed library should be initialized and selected on play.

### A Quick Note on Using Drawings as Player Controls or Triggers

If you want drawings to each trigger specific player controls or actions, identifying characters returned by GetMatch() will be important. To identify a character, either access its name with *'</span>[character].name</span>'* or use the reference to the returned character itself, assuming references to it are well maintained in your libraries.

Running this name or reference through a switch case statement will then let you map drawn characters to actions.


## How it Works

When a drawing is converted to a character, it is stored as an instance of the “Bitmap” class. A Bitmap converts the points of a drawing into four different map-like representations: the **GridMap, CircleMap, HorizontalMap, and VerticalMap**. These representations each feature their own setup process and comparison score calculations. When two Bitmaps are compared, all four of these scores are multiplied by a set weight value then summed to even out each map's individual strengths and weaknesses.

### GridMap:

1. Obtain the points of the drawing
2. Find the bounding values of the drawing and take the left and bottom bounds as the new origin for each axis
3. Divide the vertical axis into n slices of equal area
4. Divide the horizontal axis into n slices of equal area

Now, we assign each cell of the created grid a value equal to the percentage of points it contains out of the entire drawing. This results in a grid that represents the distribution of points of the drawing.

To use this for character recognition, we take the mean squared error between two characters' GridMaps as a comparison score. To calculate this, we compare the two GridMaps at each cell, adding the squared difference between each pair of cells to a total error score. Then, we divide the total error by the number of cells (n * n) to obtain a normalized score between the two maps, where a lower score means a more likely match.

Pros:
 - Good for distorted characters that are squished or stretched

### CircleMap:

1. Calculate the geometric median of all points in the drawing, and the furthest point from it
2. Draw a circle centered around the median point, with a radius extending to the furthest point
3. Divide the circle into n rings of equal thickness
4. Divide each ring into quadrants

Like the GridMap, we assign each quadrant a value equal to the percentage of points it contains out of the entire drawing to obtain a representation of the distribution of points. 

We also use the same mean squared error score calculation as the GridMap, however, instead of comparing pairs of cells, we compare pairs of quadrants.

### HorizontalMap

For the HorizontalMap and VerticalMap, we take into account the order points are drawn in
1. Obtain the points of the drawing and their drawn order
2. Starting from the first point, start a line segment, ending it and and beginning a new one whenever the trend of the points in the y direction changes
3. Divide the vertical axis into (n * n) slices of equal area
4. Obtain an array of values representing the number of lines that intersect with each slice


Array representation of this HorizontalMap: {}
In short, this method simplifies the drawing into lines, then flattens it into a 1D representation of the distribution of points along the horizontal axis. This approach is especially robust for characters, as most common characters can be easily simplified down into lines and curves.
To calculate a comparison score, we take 2 characters' respective arrays representing 

### VerticalMap


1. Obtain the points of the drawing and their drawn order
2. Starting from the first point, start a line segment, ending it and and beginning a new one whenever the trend of the points in the y direction changes
3. Divide the vertical axis into (n * n) slices of equal area
4. Obtain an array of values representing the number of lines that intersect with each slice
![enter image description here](https://photos.google.com/share/AF1QipMmVaRLXCh9hFaaJL_gYqJYgma-CxLk-T_wqLVZZSmqiahYFSZEijP1KjZMzfnqVw/photo/AF1QipPIU_mtV9vRs1icaJ5xbF5cFJvJRJ08kqgnQp5k?key=dGhad3VxTy1XSHRTNVItNEtKVVRyYmlEdVh1ZGpB)
*Array representation of this VerticalMap: {1, 1, 1, 1, 2, 2, 2, 2, 2}*

## Documentation


### General Methods:
*Note: The return type 'Character' is not be confused with the primitive 'char' type.*
| Type | Method | Description |
|-----------------|------------------|-----------------|
| void    | SetLibrary    | Sets the current character library to compare from  & modify. By default, set to lowercase alphabets on start.    |
| void     | SetDrawSurface(Collider2D collision2D)    | Sets the area on screen the mouse must be within to draw new points.    |
| void     | ClearDrawing()    | Clears the drawing and its stored points.    |
| void     | ShowDrawing(bool inp)    | Shows (true) or Hides (false) the drawn line (visual change only).     |
|void  	|EnableDrawing(bool  inp)	|Enables (true) or Disables (false) checks for drawing controls within MouseTracker.	|
| void     | SetWeights(double circleMapWeight, double gridMapWeight, double horizontalMapWeight, double verticalMapWeight)    | Sets the weights applied to each bitmap during comparison score calculation. Recommended to keep as (1.0, 1.0, 1.0, 1.0) unless certain maps do not work as well for a use case.   |
| void     | SetPrecision(int input)    | Updates the precision/width of stored bitmaps in current library    |
|void 	|AddDrawingToLib(string charName)	|Creates a character from the current drawing and adds it to the current library	|
|void 	|AddDrawingToLib(string charName, CharacterLibrary charLib)	|Creates a character from the current drawing and adds it to a specified library	|
| void     | AddCharToLib(Character character)    | Adds a character to the current library    |
| void      | AddCharToLib(Character  character, CharacterLibrary  charLib)    | Adds a character to a specified library    |
|void	|RemoveCharacter(Character character)	|Removes a character from the current library, if found.	|
|void		| RemoveCharFromLib(Character character, CharacterLibrary  charLib)	|Removes a character from a specified library, if found.	|
|void		| RemoveCharFromLib(string  charName) 	|Removes a character from the current library by string name lookup, if found.	|
|void		| RemoveCharFromLib(string  charName, CharacterLibrary  charLib)	|Removes a character from a specified library by string name lookup, if found.	|
|List\<CharacterLibrary\>	|GetLibraryList()	|Returns the list of stored libraries	|
|void	|ClearLibraryList()	|Clears the list of stored libraries	|
|List\<Character\>	|GetCharList()	|Returns the list of characters stored in the current library	|
|List\<Character\>	|GetCharList(CharacterLibrary charLib)	|Returns the list of characters stored in a specified library|
|Character 	|GetMatch()	|Returns the closest match to the drawing in the current character library	|
|Character 		|GetMatch(CharacterLibrary charLib)	|Returns the closest match to the drawing in a specified character library	|
|List<KeyValuePair<Character, double>> 	|GetMatchList()	|Returns a list of (Character -> Score) KVPs, representing the drawing's comparison score to each character in the current library.	List is sorted from closest matches (highest % score), to least likely matches (lowest % score).|
|List<KeyValuePair<Character, double>> 	|GetMatchList(CharacterLibrary charLib)	|Returns a sorted list of KVP scores based on a specified library's characters. Same structure as GetMatchList().	|

### Prefabbed Character Library Initializers:
| Type | Method | Description |
|-----------------|------------------|-----------------|
|void  	|InitializeLowercaseAlphas(CharacterLibrary  charLib)	|Initializes a prefabbed character library of lowercase alphabet letters to a character library.	|
|void  	|InitializeSymbols(CharacterLibrary  charLib)	|Initializes a prefabbed character library of symbols to a character library.	|
|void  	|SetupDefaultLibraries()	|Initializes the included libraries. Clears stored libraries, then initializes 5 libraries: The lowercase alphabet, A set of symbols, and 3 empty libraries. Sets precision to 3 and weights to (1.0, 1.0, 1.0, 1.0).|

### Debug and Print Methods:
| Type | Method | Description |
|-----------------|------------------|-----------------|
|string	 |PrintCurCharacter()	 |Returns a code snippet to initialize the current drawing as a prefabbed character. If called in the Unity editor during runtime, copies the string to the clipboard. Paste the returned string inside a character library initializer method in the DrawingRecognition script.	
 | 
|string  	|PrintCharacterLibrary()	|Returns a code snippet to initialize all characters in the current library as prefabbed characters. If called in the Unity editor during runtime, copies the string to the clipboard. Paste the returned string inside a character library initializer method in the DrawingRecognition script.	|
|void	|PrintGridMap(Bitmap  bitmap)	|Prints the 2D GridMap representation of the current drawing. Note that if working correctly, cell values should sum (very close) to 1.	|
|void  	|PrintCircleMap(Bitmap  bitmap)	|Prints the 2D circle map representation stored by a bitmap. Each ring of the map is printed in order from inner to outer ring as 2x2 group of values.	|
|void  	|PrintCoMCircleMap(Bitmap  bitmap)	|Prints the 2D circle map representation stored by a bitmap, using the center of mass as the centerpoint instead of the geometric median used by default. Each ring of the map is printed in order from inner to outer ring as 2x2 group of values. Use this for comparing the effectiveness of the two centerpoints.	|
|void  	|PrintFlatMapHorizontal(Bitmap  bitmap)	|Prints the horizontal FlatMap representation stored by a bitmap. 	|
|void  	|PrintFlatMapVertical(Bitmap  bitmap)	|Prints the vertical FlatMap representation stored by a bitmap.	|

### Brief Overview of Other Classes used in DrawingRecognition 
*(These are model classes you are not required to interact with, but may potentially end up editing in case you want to add to or modify the behavior of a class, or find a bug. Documentation for each of these classes is can be found within their respective scripts)*

**Bitmap:** Stores the points of a drawing and various map representations of it for usage during comparison. Handles calculations of raw comparison scores between Bitmaps.

**Character:** Stores a Bitmap and a name to be associated with it. Does not contain behavior. *(Overall, this class is quite redundant ignoring abstraction & readability)*

**CharacterLibrary:** Stores a list of Characters and handles character recognition. Features methods that returns the closest match to an input Character by calculating an input's raw bitmap comparison score for each Character in the stored list. Holds map weighing and precision values used for comparison, along with callable methods to update these values.

  
  
  
  


