using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Piece : MonoBehaviour
{
    public bool hasMoved = false;
    public bool canCastle = false;

    public List<Vector2> legalMoves = new List<Vector2>();
    public Vector2 position;


    [SerializeField] Storage storage;
    Game game;

    private void Start()
    {
        storage = GameObject.FindGameObjectWithTag("Storage").GetComponent<Storage>();

        //CHANGE THIS AFTER IMPLEMENTING AI
        game = storage.game;
    }

    public enum Type
    {
        Pawn, Knight, Bishop, Rook, Queen, King
    }
    public Type type;
    string formattedType;

    public enum Colour
    {
        White, Black
    }
    public Colour colour;

    public void Capture()
    {
        if (type == Type.King) storage.game.Checkmate();
        Destroy(gameObject);
    }
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
        else _GenerateMoves();
        
    }
    
    public string GetFormattedType()
    {
        if (formattedType is null) formattedType = type switch
        {
            Type.Knight => "N",
            Type.Pawn => "",
            _ => type.ToString().Substring(0, 1)
        };

        return formattedType;
    }

    Vector2[] directions = { new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };

    public void UpdateCastle()
    {
        if (hasMoved) { canCastle = false; return; }

        for (int i = 1; i <= 3; i += 2)
        {
            var move = position;
            for (int j = 0; j < 5; j++)
            {
                move += directions[i - 1];
                if (move.x > 8 || move.x < 1) break;

                var pieceOnNewSquare = game.GetPiece(move);

                if (pieceOnNewSquare is null) { continue; }
                if (pieceOnNewSquare.type != Type.King || pieceOnNewSquare.colour != colour) { canCastle = false; break; }

                canCastle = true;
                return;
            }
        }
    }

    void _GenerateMoves()
    {
        var game = storage.game;

        //Index move direction Visualization, P = Piece
        //
        //   5 4 6
        //   3 P 1
        //   8 2 7
        //
        //

        //Castling stuff
        if (type == Type.King && !hasMoved)
        {
            foreach(var rook in game.pieces[$"{Type.Rook}"].Values)
            {
                if (rook.canCastle && rook.colour == colour) legalMoves.Add(new Vector2(rook.position.x == 1 ? position.x - 2 : position.x + 2, position.y));
            }
        }

        int startIndex;
        int endIndex;
        int maxHorizontalDepth = 7;
        int maxVerticalDepth = 7;
        int maxDiagonalDepth = 7;

        switch (type)
        {
            case Type.Pawn: startIndex = 4; endIndex = 6; maxVerticalDepth = hasMoved == true ? 1 : 2; maxDiagonalDepth = 1; break;
            case Type.Rook: startIndex = 1; endIndex = 4; break;
            case Type.Bishop: startIndex = 5; endIndex = 8; break;
            case Type.King: startIndex = 1; endIndex = 8; maxHorizontalDepth = 1; maxVerticalDepth = 1; maxDiagonalDepth = 1; break;
            default: startIndex = 1; endIndex = 8; break;
        }


        for (int i = startIndex; i <= endIndex; i++)
        {
            int directionMoveLimit = i == 4 || i == 2 ? maxVerticalDepth : i == 1 || i == 3 ? maxHorizontalDepth : maxDiagonalDepth;

            int pawnReversingVariable = 0;
            if (type == Type.Pawn) {
                if (colour == Colour.Black && i <= 6 && i >= 4) { pawnReversingVariable = i == 4 ? -2 : 2; }
            }

            Vector2 move = position;
            for (int j = 0; j < 8; j++)
            {
                if (directionMoveLimit == j) break;


                move += directions[(i-1) + pawnReversingVariable];

                if (move.x < 1 || move.y < 1 || move.x > 8 || move.y > 8) break;


                var pieceOnNewSquare = game.GetPiece(move);

                if (pieceOnNewSquare is null)
                {
                    if (type == Type.Pawn && i != 4 && !game.GetGhostPawn(move)) break;
                    legalMoves.Add(move); 
                    continue;
                }
                if (type == Type.Pawn && i == 4) break;

                if (pieceOnNewSquare.colour != colour) { legalMoves.Add(move); }
                break;
            }
        }
    }
}
