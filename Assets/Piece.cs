using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class Piece : MonoBehaviour
{
    public bool hasMoved = false;
    public bool canCastle = false;

    public HashSet<Piece> piecesThatAttack = new HashSet<Piece>(8);

    public Vector2 position;
    public int id;

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
        Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6
    }
    public Type type;

    public enum Direction
    {
        topLeft=5,    up=4,   topRight=6,

        left = 3,             right = 1,

        bottomLeft=8, down=2, bottomRight=7,                                                                                                                                 ////what are you doin over 'ere? get back in line
                                                                                                                                                                                                    none = 0,
    } 

    public enum Colour
    {
        White = 0, Black = 1, None = 2,
    }
    public Colour colour;

    public void Attacked()
    {

        ////Debug.Log($"PIECEATTACKED, {colour}{type} id:{id} atPos: {position}");
        UpdateChecks();
        UpdatePins();
        UpdateLegalMoves();
    }

    public bool dead = false;

    public void Capture()
    {
        if (type == Type.King) { storage.game.Checkmate(); }
        dead = true;
        game.pinLines[this].Clear();

        game.RemoveControlledSquares(this);

        UpdateAttackingPieces(DoRemove: true);

        Destroy(gameObject);
    }
    public void Moved()
    {
        UpdateAttackingPieces(DoRemove: true);
        foreach(var piece in game.GetPiecesOf(game.GetEnemyColour(colour))) { piece.RemoveAttackingPiece(this); }

        if (type == Type.King) UpdatePinsSurroundingPieces();
        game.RemoveControlledSquares(this);
        UpdatePins();
        UpdateLegalMoves();
        game.RefreshDebugSquares();
    }


    public void UpdateLegalMoves()
    {
        if (dead) return;

        game.pinLines[this].Clear();

        game.legalMoves[this].Clear();

        game.controlledSquares[colour][id].Clear();
        UpdatePins();
        
        _GenerateMoves();
    }

    public void UpdateCastle()
    {
        if (dead) return;

        var king = game.GetPiecesOf(Type.King, colour)[0];
        if (hasMoved || king.hasMoved) { canCastle = false; return; }

        if (game.GetLine(position, king.position).firstPiece == king) canCastle = true;
    }

    void UpdateChecks()
    {
        if (type == Type.King && game.GetControlledEnemySquares(colour).Contains(position))
        {
            Debug.Log($"{colour}{type} Checked");
            game.checkKing(this);
            
            foreach (var piece in game.GetPiecesOf(colour))
            {
                if (piece.id == id) continue;
                UpdatePins();
                piece.UpdateLegalMoves();

                game.GUIDrawCheckLines(piece.colour);
            }
        }
    }

    public bool RemoveAttackingPiece(Piece attackingPiece)
    {
        return piecesThatAttack.Remove(attackingPiece);
    }
    public void UpdateAttackingPieces(bool DoRemove)
    {
        foreach (var piece in new HashSet<Piece>(piecesThatAttack)) { if (DoRemove) piece.RemoveAttackingPiece(this); piece.UpdateChecks(); piece.UpdatePins(); piece.UpdateLegalMoves(); }
    }
    public void UpdatePins()
    {
        if (dead) return;

        game.pinLines[this].Clear();
        int count = 0;

        foreach (var attackingPiece in piecesThatAttack)
        {
            count++;
            if (attackingPiece.type == Type.Knight || attackingPiece.type == Type.King || attackingPiece.type == Type.Pawn || attackingPiece.colour == colour) continue;

            var line = game.GetLine(from: attackingPiece.position, inDirectionOf: position, phaseThroughPiece: this);

            if (line.firstPiece is not null && line.firstPiece?.colour == colour && line.firstPiece?.type != Type.Pawn) {

                if (type != Type.Pawn) Debug.Log($"{colour}{type}, pos:{position} HAS BEEN PINNED | to: {line.firstPiece.colour}{line.firstPiece.type}, pos:{line.firstPiece.position} | by: {attackingPiece.colour}{attackingPiece.type} pos:{attackingPiece.position}");
                game.pinLines[this].TryAdd(line.firstPiece, line.lineVectors);
            }
        }
    }

    void UpdatePinsSurroundingPieces()
    {
        foreach (var direction in Piece.directions)
        {
            var pieceOnDirection = game.GetPieceAtPosition(position + direction);
            if (pieceOnDirection) 
            {
                pieceOnDirection.UpdatePins();
                pieceOnDirection.UpdateLegalMoves();
            }
        }
    }

    public bool IsMoveACheckBlock(Vector2 move)
    {
        Piece king = game.GetPiecesOf(Type.King, colour)[0];

        bool isKing = type == Type.King;
        HashSet<Vector2> enemyControlledSquares = game.GetControlledEnemySquares(colour);

        if (!enemyControlledSquares.Contains(move) && isKing) return true;

        bool isPinnedToKing = game.pinLines[this].ContainsKey(king);

        int checkBlockCounter = 0; int linecount = 0;
        //check if move blocks every king check
        foreach (var checkingPiece in king.piecesThatAttack)
        {
            if (checkingPiece.colour == colour) continue;

            if (checkingPiece.type == Type.Knight) {
                HashSet<Vector2> knightMoves = game.GetControlledSquares(checkingPiece);

                if ((type == Type.King && knightMoves.Contains(move)) || (type != Type.King && move != checkingPiece.position)) return false;
                else return true;
            }

            var line = game.GetLine(checkingPiece.position, king.position).lineVectors;
            if (line is null) continue;

            ////Debug.Log($"KingCheckLineCalc. Piece:{colour}{type} MOVE:{move} checkLine:{line.ToSeparatedString(",")} LINECONTAINSMOVE:{line.Contains(move)} isPinnedToKing{isPinnedToKing} |  id:{id} pos:{position}  |  checkingPiece:{checkingPiece.colour}{checkingPiece.type}");

            linecount++;
            if (line.Contains(move)) checkBlockCounter++;

            if ((line.Contains(move) && isKing) || (checkBlockCounter != linecount)) return false;
        }

        //allows move if it blocks all checks; and if it is pinned to the king- doesn't reveal another check by moving off of the file/rank that pins it to the king
        if (!isPinnedToKing || (game.pinLines[this].TryGetValue(king, out var kingPinLine) && kingPinLine.Contains(move))) { Debug.Log($"LEGALCHECKBLOCKINGMOVEFOUND, {colour}{type} pos:{position} move:{move}"); return true; }

        return false;
    }




    public static Vector2[] directions = { new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
    void _GenerateMoves()
    {
        //Index move direction Visualization, P = Piece
        //   5 4 6
        //   3 P 1
        //   8 2 7
        //

        if (type == Type.King && !hasMoved) {
            foreach (var rook in game.pieces[Type.Rook].Values) {

                if (rook.canCastle && rook.colour == colour) game.legalMoves[this].Add( new Vector2( x: (rook.position.x == 1 ? position.x - 2 : position.x + 2), position.y ) );
            }
        }

        int startIndex = 1;
        int endIndex = 8;
        int maxHorizontalDepth = 7;
        int maxVerticalDepth = 7;
        int maxDiagonalDepth = 7;

        switch (type)
        {
            case Type.Pawn: startIndex = 4; endIndex = 6; maxVerticalDepth = hasMoved == true ? 1 : 2; maxDiagonalDepth = 1; break;
            case Type.Rook: startIndex = 1; endIndex = 4; break;
            case Type.Bishop: startIndex = 5; break;
            case Type.Knight or Type.King: maxHorizontalDepth = 1; maxVerticalDepth = 1; maxDiagonalDepth = 1; break;
            default: break;
        }

        //nothin' to see here keep scrolling
        Vector2[] directions = type != Type.Knight ? Piece.directions : new Vector2[]{ new Vector2(-2, 1), new Vector2(-1, 2), new Vector2(1, 2), new Vector2(2, -1), new Vector2(-2, -1), new Vector2(-1, -2), new Vector2(1, -2), new Vector2(2, 1) };
        

        for (Direction direction = (Direction)startIndex; direction <= (Direction)endIndex; direction++)
        {
           
            int directionDepthLimit = direction switch { Direction.down or Direction.up => maxVerticalDepth, Direction.left or Direction.right => maxHorizontalDepth, _ => maxDiagonalDepth };

            ////Debug.Log($"piece:{colour}{type} directionDepthLimit:{directionDepthLimit} direction:{direction}");

            int pawnReversingVariable = 0;
            if (type == Type.Pawn && colour == Colour.Black) { pawnReversingVariable = direction == Direction.up ? -2 : 2; }



            Vector2 move = position;

            for (int depth = 0; depth < directionDepthLimit; depth++)
            {
                if (depth >= directionDepthLimit) break;

                
                move += directions[(int)(direction - 1 + pawnReversingVariable)];


                if (move.x < 1 || move.y < 1 || move.x > 8 || move.y > 8) break;
                if (type == Type.King && game.GetControlledEnemySquares(colour).Contains(move)) break;

                Piece pieceOnNewSquare = game.GetPieceAtPosition(move);


                ////Debug.Log($"check:{game.isInCheck} CalledFrom:{colour} ");

                if (game.isInCheck == true && game.turnColour == colour)
                {
                    if (IsMoveACheckBlock(move) == false) {
                        
                        //if invalid check-blocking move would end up capturing a piece skip to next direction as it blocks any deeper moves
                        if (pieceOnNewSquare is not null) { break; } else { continue; }
                    }
                }


                bool isMoveBlockedByKingPin = PinBlocksMove(move);
                ////if (isMoveBlockedByKingPin) { Debug.Log($"pinTestFailed, PiecePinnedToKing:{colour}{type}, atPos:{position}, move:{move}"); }


                //pawn edge-cases and special behavior
                bool controlSquareSkip = false;

                if (type == Type.Pawn) 
                {
                    if (pieceOnNewSquare is null)
                    {
                        if (direction == Direction.up) { controlSquareSkip = true; }
                        //if no pieces are on the pawn diagonal attack squares and no en passant is available add them to controlledsquares but not as legal moves
                        else if (!game.GetGhostPawnAtPosition(move)) { game.AddControlledSquare(this, move); break; }
                    }
                    else if (pieceOnNewSquare is not null)
                    {
                        if (direction == Direction.up) break;
                    }
                }
                //


                if (!isMoveBlockedByKingPin) {
                    if (!controlSquareSkip) game.AddControlledSquare(this, move);

                    if (pieceOnNewSquare is null)
                    {
                        game.legalMoves[this].Add(move); 
                        continue; 
                    }
                    else if (pieceOnNewSquare is not null)
                    {
                        bool pieceIsEnemy = pieceOnNewSquare.colour != colour;
                        bool addedAttacker = pieceOnNewSquare.piecesThatAttack.Add(this);

                        if (addedAttacker) pieceOnNewSquare.Attacked();

                        if (pieceIsEnemy == true)
                        {
                            ////if (pieceOnNewSquare.type != Type.Pawn) Debug.Log($"PIECEATTACKFOUND, PieceThatAttacked: {colour}{type}{id} CurrentPosition: {position}, |  AttackedPiece: {pieceOnNewSquare.type.HumanName()}{pieceOnNewSquare.colour} atPos: {move}");

                            game.legalMoves[this].Add(move);
                        }
                        break;
                    }
                    

                }
                break;

            }

        }
        game.GUIUpdatePiece(this);
    }


    bool PinBlocksMove(Vector2 move)
    {
        Piece king = game.GetPiecesOf(Type.King, colour)[0];
        bool? moveCausesCheckMate = game.pinLines[this].TryGetValue(king, out var kingPinLine) && !kingPinLine.Contains(move);

        ////Debug.Log($"piece:{colour}{type} PinTest, move:{move} causesCheckMate?:{moveCausesCheckMate} pinLines:{game.pinLines[this].ToSeparatedString(", ")}");

        return (bool)moveCausesCheckMate;
    }
}
