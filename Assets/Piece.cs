using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Piece : MonoBehaviour
{
    public bool pawnMoved = false;

    public List<Vector2> legalMoves = new List<Vector2>();
    public Vector2 position;

    [SerializeField] Storage storage;

    private void Start()
    {
        storage = GameObject.FindGameObjectWithTag("Storage").GetComponent<Storage>();
    }

    public enum Type
    {
        Pawn, Knight, Bishop, Rook, Queen, King
    }
    public Type type;

    public enum Colour
    {
        White, Black
    }
    public Colour colour;

    public void UpdateLegalMoves()
    {
        legalMoves.Clear();
        
        if (type == Type.Knight)
        {
            Dictionary<string, Vector2> hardcodedKnightMovesBecauseImBad = new Dictionary<string, Vector2> { { "1", new Vector2(position.x + 1, position.y + 2) }, { "2", new Vector2(position.x - 1, position.y + 2) }, { "3", new Vector2(position.x - 2, position.y + 1) }, { "4", new Vector2(position.x + 2, position.y + 1) }, { "5", new Vector2(position.x + 2, position.y - 1) }, { "6", new Vector2(position.x + 1, position.y - 2) }, { "7", new Vector2(position.x - 1, position.y - 2) }, { "8", new Vector2(position.x - 2, position.y - 1) } };

            foreach (var move in hardcodedKnightMovesBecauseImBad.Values)
            {
                if (move.x < 1 || move.y < 1 || move.x > 8 || move.y > 8) continue;

                var pieceOnSquare = storage.game.GetPiece(move);
                if (pieceOnSquare is null) { legalMoves.Add(move); continue; }
                if (pieceOnSquare.colour != colour) { legalMoves.Add(move); continue; }
            }
        }
        else GenerateMoves();
        
    }


    Vector2[] directions = { new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
    

    void GenerateMoves()
    {
        var game = storage.game;

        //Index move direction Visualization, P = Piece
        //
        //   5 4 6
        //   3 P 1
        //   8 2 7
        //
        //

        
        int startIndex;
        int endIndex;
        int maxHorizontalDepth = 7;
        int maxVerticalDepth = 7;
        int maxDiagonalDepth = 7;

        switch (type)
        {
            case Type.Pawn: startIndex = 4; endIndex = 6; maxVerticalDepth = pawnMoved == true ? 1 : 2; maxDiagonalDepth = 1; break;
            case Type.Rook: startIndex = 1; endIndex = 4; break;
            case Type.Bishop: startIndex = 5; endIndex = 8; break;
            case Type.King: startIndex = 1; endIndex = 8; maxHorizontalDepth = 1; maxVerticalDepth = 1; maxDiagonalDepth = 1; break;
            default: startIndex = 1; endIndex = 8; break;
        }


        for (int i = startIndex; i<=endIndex; i++)
        {
            int directionMoveLimit = i == 4 || i == 2 ? maxVerticalDepth : i == 1 || i == 3 ? maxHorizontalDepth : maxDiagonalDepth;

            Vector2 move = position;
            for (int j = 0; j < 8; j++)
            {
                if (directionMoveLimit == j) break;


                move += directions[i - 1];

                if (move.x < 1 || move.y < 1 || move.x > 8 || move.y > 8) break;


                var pieceOnNewSquare = game.GetPiece(move);

                if (pieceOnNewSquare is null)
                {
                    if (type == Type.Pawn && i != 4) break;
                    legalMoves.Add(move); 
                    continue;
                }

                if (pieceOnNewSquare.colour != colour) { legalMoves.Add(move); }
                break;
            }
        }
    }
}
