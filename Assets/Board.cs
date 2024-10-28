using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
#endif

public class Board : MonoBehaviour
{
    [SerializeField] private GameObject squarePrefab;
    public Storage storage;


    public Color _lightColor;

    public Color _darkColor;
    public Color lightColor { get { return _lightColor; } set { RefreshSquares(); _lightColor = value; } }
    public Color darkColor { get { return _darkColor; } set { RefreshSquares(); _darkColor = value; } }


    //Great Wall Of Colour
    public Color whiteControlledSquaresColour;
    public Color blackControlledSquaresColour;
    public Color whiteCSFlashingColour;
    public Color blackCSFlashingColour;

    public Color legalMovesColour;
    public Color legalMovesSelectFlashingColour;
    public Color legalMovesUnSelectFlashingColour;

    public Color selectedColour;
    public Color unSelectColour;
    public Color selectColour;

    public Color madeMoveColour;

    public Color pinLineColour;
    public Color checkLineColour;

    public Color errorColour;
    //

    public string GetFormattedColumn(Vector2 position)
    {
        return position.x switch { 1 => "a", 2 => "b", 3 => "c", 4 => "d", 5 => "e", 6 => "f", 7 => "g", 8 => "h", _ => "uh this isn't supposed to happen" };
    }
    public string GetFormattedType(Piece.Type type)
    {
        return type switch
        {
            Piece.Type.Knight => "N",
            Piece.Type.Pawn => "",
            _ => type.ToString().Substring(0, 1)
        };
    }

    public Dictionary<Vector2, Renderer> squares = new Dictionary<Vector2, Renderer>();

    public Color GetSquareColor(Vector2 position)
    {
        return (position.x + position.y) % 2 != 0 ? lightColor : darkColor;
    }

    public Material RefreshSquare(Vector2 position)
    {
        var square = squares[position];
        Color color = GetSquareColor(position);
        square.material.color = color;
        return square.material;
    }

    public void RefreshSquares()
    {
        foreach(var square in squares)
        {
            Color color = (square.Key[0] + square.Key[1]) % 2 != 0 ? lightColor : darkColor;
            square.Value.material.color = color;
        }

        //Debug.Log($"Light Colour: ({lightColor.r*255},{lightColor.g*255},{lightColor.b*255}) Dark Colour: ({darkColor.r * 255},{darkColor.g * 255},{darkColor.b * 255})");
    }

    

    void CreateBoard()
    {
        //generate board squares
        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                Vector2 index = new Vector2(file+1, rank+1);

                if (squares.ContainsKey(index)) Destroy(squares[index]);

                Color color = (file + rank) % 2 != 0 ? lightColor : darkColor;

                squares[index] = DrawSquare(new Vector3(file+1, rank+1, 5), color);
            }
        }
    }
    Renderer DrawSquare(Vector3 position, Color color)
    {
        var Square = Instantiate(squarePrefab, position, Quaternion.identity).GetComponent<Renderer>();

        Square.material.color = color;
        return Square;
    }

    void Start()
    {
        CreateBoard();
    }
}


