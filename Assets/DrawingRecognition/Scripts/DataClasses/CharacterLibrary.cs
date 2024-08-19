using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Security;

/*
A CharacterLibrary stores a list of Characters, and handles comparison calculations of input bitmaps to the bitmaps of its stored characters
    - By default, a character library contains an "Empty" character (added during initialization)
    - Also contains weights for each comparison method, set by drawingRecognition during Awake()/Runtime.
*/

/*
****Public Interface

**Constructors:

CharacterLibrary(string name)
    - Initializes a new CharacterLibrary with a given name. Contains 1 empty character by default.


**Instance Variables:

string name
    - The name of this CharacterLibrary, used for identification.

List<Character> characterList
    - The list of Characters stored in this library.

double horizontalMapWeight, verticalMapWeight, circleMapWeight, gridMapWeight:
    - Weights applied to each map representation score when calculating an overall comparison score.


**Methods:

void AddCharacter(Character character)
    - Adds a character to the character list.
    - If a character with the same name exists in this library, it is replaced with the new character.

void RemoveCharacter(Character character)
    - Removes the specified character from this library, if it exists.

void RemoveCharacter(string charName) 
    - Removes a character from this library by string name lookup, if the name exists.

void ClearLibrary()
    - Clears all characters from this library.


KeyValuePair<Character, double> GetMatch(Bitmap bitmap)
    - Returns the closest character match to a given bitmap and its comparison score.
    
List<KeyValuePair<Character, double>> GetMatchList(Bitmap bitmap)
    - Returns a list of all character comparison scores.
    - List is sorted from lowest scores -> highest, where a lower score means a closer match.


double CompareCharacters(string char1Name, string char2Name)
    - Returns the comparison score of 2 characters within this library.
    - Unused in my current implementation; could be useful for cross-comparison of values to optimize weight values.

void SetWeights(double circleMapWeight, double gridMapWeight, double horizontalMapWeight, double verticalMapWeight)
    - Sets the weight values used by this library in comparisons.
    - Recommended to keep weight vals as {1.0, 1.0, 1.0, 1.0} across all libs, but it is possible to set different weights for each library.


*/
public class CharacterLibrary 
{
    public string name;
    public List<Character> characterList;
    private Dictionary<string, Character> takenTable; // stores name -> character mappings to check if name has already been assigned to a char

    // Weights for score calculation; Explanations of maps in Bitmap class desc.
    public double horizontalMapWeight; // Flattened 1D representation of columns
    public double verticalMapWeight; // Flattened 1D representation of rows
    public double circleMapWeight; // 2d radial array representation
    public double gridMapWeight; // 2d array representation
    
    private static Character Empty; // Definition for an empty character
    private static void InitializeEmptyDef() {
        // Create empty vector list defs
        List<Vector2> emptyVec2 = new List<Vector2>(); 
        List<Vector3> emptyVec3 = new List<Vector3>();

 	 	Empty = new Character(new Bitmap(emptyVec2, emptyVec3, 4 ), "Empty");
    }

    // Constructor
    public CharacterLibrary(string name) {
        this.name = name;
        characterList = new List<Character>();
        takenTable = new Dictionary<string, Character>();

        // Initialized with an Empty character by default
        InitializeEmptyDef(); // Static method, but needs to run at least once
        AddCharacter(Empty);
    }


    #region LibraryInteraction 
    // Adds/Updates a character to the character list
    public void AddCharacter(Character character) {
        if (character == null) {
            return;
        }
            
        // If a mapping exists, replace it
        if (takenTable.ContainsKey(character.name)) {
            takenTable[character.name] = character;

            // Find the first occurance of the name & replace it 
            for(int i = 0; i < characterList.Count; i++) {
                if (characterList[i].name.Equals(character.name)) {
                    characterList[i] = character;
                    return;
                }
            }
            return;
        }
        // If no previous mapping is found, add normally
        takenTable.Add(character.name, character);
        characterList.Add(character);
    }

    // Removes character from this library
    public void RemoveCharacter(Character character) {
        if (character == null)
            return;

        takenTable.Remove(character.name);
        characterList.Remove(character);
    }

    public void RemoveCharacter(string charName) {
        if (!takenTable.ContainsKey(charName))
            return;

        Character c = takenTable[charName];
        takenTable.Remove(charName);
        characterList.Remove(c);
    }

    // Clears all characters from this library
    //      - Keeps an empty character
    public void ClearLibrary() {
        characterList = new List<Character>();
        takenTable = new Dictionary<string, Character>();
        AddCharacter(Empty);
    }

    // Compares 2 characters within this library
    public double CompareCharacters(string char1Name, string char2Name) {
        Character char1 = takenTable[char1Name];
        Character char2 = takenTable[char2Name];

        if (char1 == null || char2 == null) {
            Debug.Log("CharacterLibrary.CompareCharacters Error: name input invalid");
            return 100; // Return arbitrary large val to signal error
        }

        return GetScore(char1.bitmap, char2.bitmap);
    }

    #endregion LibraryInteraction


    #region ScoreCalculation
    // Region for calculating comparison scores

    // Returns the closest match KVP(Character, score) 
    public KeyValuePair<Character, double> GetMatch(Bitmap bitmap) {
        List<KeyValuePair<Character, double>> scoreList = GetMatchList(bitmap);

        return scoreList[0];
    } 

    // Returns the list of comparison scores for each character in this library
    public List<KeyValuePair<Character, double>> GetMatchList(Bitmap bitmap) {
        // Clear scoreList to store values
        List<KeyValuePair<Character, double>> scoreList = new List<KeyValuePair<Character, double>>();

        // Compare all stored characters in this library to the bitmap
        foreach(Character charRef in characterList) {
            // Get score
            Bitmap bitmapRef = charRef.bitmap;
            double score = GetScore(bitmap, bitmapRef); 

            // UNCOMMENT TO VIEW SCORES FOR EACH CHARACTER
            // Debug.Log($"Compared To: '{charRef.name}' Score: {Math.Truncate(score * 1000) / 1000}");

            // Add score to list
            scoreList.Add(new KeyValuePair<Character, double>(charRef, score));
        }

        // Sort stored scores low->high & convert the kvp values to a percentage based on average score
        scoreList = scoreList.OrderBy(kvp => kvp.Value).ToList();
        scoreListToPercent(scoreList); 
        return scoreList; 
    } 

    // Calculates score of weighted comparison between 2 characters 
    private double GetScore(Bitmap bitmap1, Bitmap bitmap2) {

        double gridDiff = 100 * (double) bitmap1.GridCompareTo(bitmap2);
        double circDiff = 100 * (double) bitmap1.CircCompareTo(bitmap2);
        double horizontalDiff = (double) bitmap1.FlatMapCompareTo(bitmap2, true);
        double verticalDiff = (double) bitmap1.FlatMapCompareTo(bitmap2, false);

        // Now, set the higher value between circDiff and gridDiff closer to the lower score
        //      - Ensures that very low scores are valued more & unnaturally varied scores in a comparison are reduced
        // Function: https://www.desmos.com/calculator/sebcpk9ono
        if (circDiff < gridDiff) {
            double bias = 1 / (2 * (1 + gridDiff - circDiff));
            gridDiff = circDiff + ((gridDiff - circDiff) * bias); 
        }
        else if (gridDiff  < circDiff) {
            double bias = 1 / (2 * (1 + circDiff - gridDiff));
            circDiff = gridDiff + ((circDiff - gridDiff) * bias); // Val between grid & circle that stays closer to smaller value at high difference values
        }  
        
        double score = (horizontalDiff * horizontalMapWeight) + (verticalDiff * verticalMapWeight) 
                        + (circDiff * circleMapWeight) + (gridDiff * gridMapWeight);
        
        // UNCOMMENT TO VIEW INDIVIDUAL SCORES FOR EACH CHARACTER.
        // Also uncomment the debug log line in GetMatchList() to see the character name associated with the score
        // Debug.Log($"Grid: {gridDiff.ToString("F4")}, Circ: {circDiff.ToString("F4")}, HzMap: {horizontalDiff.ToString("F4")}, VertMap: {verticalDiff.ToString("F4")}");
        return score;
    }

    // Converts all values of input list of scores to percentages based on the average score 
    private void scoreListToPercent(List<KeyValuePair<Character, double>> scoreList) {
        if (scoreList.Count == 0) 
            return;

        double totScore = 0;
        for (int i = 0; i < scoreList.Count; i++) {
            totScore += scoreList[i].Value;
        }
        double avgScore = totScore / scoreList.Count;

        // Avoid division by zero, just in case
        if (avgScore == 0) {
            avgScore = 1;
        }

        for (int i = 0;i < scoreList.Count; i++) {
            double ratio = scoreList[i].Value / avgScore;
            if (ratio > 1) {
                ratio = 1;
            }
            double percentScore = 100 - (100 * ratio);
            percentScore = Math.Truncate(percentScore * 100) / 100;
            scoreList[i] = new KeyValuePair<Character, double>(scoreList[i].Key, percentScore);
        }
    }

    // Updates the weightings applied to each map
    public void SetWeights(double circleMapWeight, double gridMapWeight, double horizontalMapWeight, double verticalMapWeight) {
        this.circleMapWeight = circleMapWeight;
        this.gridMapWeight = gridMapWeight;
        this.horizontalMapWeight = horizontalMapWeight;
        this.verticalMapWeight = verticalMapWeight;
    }

    #endregion ScoreCalculation
}
