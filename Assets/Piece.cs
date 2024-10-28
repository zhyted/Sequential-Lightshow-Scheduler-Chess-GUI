using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
public class Piece : MonoBehaviour
{
    //flags
    public bool hasMoved = false;
    public bool canCastle = false;

    //guiflags
    public bool _guiPinLineShown = false;
    public bool _guiCheckLineShown = false;
    public bool _guiChangedMovesShown = false;

    public HashSet<Piece> piecesThatAttack = new HashSet<Piece>(8);

    public Vector2 position;
    public int id;

    public Storage storage;
    public Game game;

    

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
        GUI.DebugInfo.pieceCalculationsRunning += 1;
        ////Debug.Log($"PIECEATTACKED, {colour}{type} id:{id} atPos: {position}");

        UpdateChecks();
        UpdatePins();
        UpdateLegalMoves();
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }

    public void _resetGuiFlags()
    {
        _guiChangedMovesShown = false;
        _guiCheckLineShown = false;
        _guiPinLineShown = false;
        _guiChangedMovesShown = false;
    }

    public bool dead = false;

    public void Capture()
    {
        GUI.DebugInfo.pieceCalculationsRunning += 1;
        dead = true;
        //foreach (var piece in piecesThatAttack) { RemoveAttackingPiece(piece}; }
        //foreach (var piece in game.GetPiecesOf(game.GetEnemyColour(colour))) { piece.RemoveAttackingPiece(this); }

        UpdateAttackingPieces(DoRemove: true);
        game.legalMoves[this].Clear();

        game.RemoveControlledSquares(this);

        game.pinLines[this].Clear();
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
        Destroy(gameObject);
    }

    void PromotePawn()
    {
        GUI.DebugInfo.pieceCalculationsRunning += 1;
        storage.game.pieces[type].Remove(id);

        gameObject.GetComponent<SpriteRenderer>().sprite = (Sprite)Resources.Load($"Assets/Sprites/{colour}queen.png", typeof(Sprite));
        //TODO: add option to choose to promote to knight, bishop or rook
        type = Type.Queen;

        storage.game.pieces[type].Add(id, this);
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }

    public void Moved()
    {
        GUI.DebugInfo.pieceCalculationsRunning += 1;
        //Pawn Promotion
        if ( type == Type.Pawn && (position.y == 8 || position.y == 1) )
        {
            PromotePawn();
        }
        UpdateAttackingPieces(DoRemove: true);

        //TODO: update to only run on influenced pieces
        foreach (var piece in game.GetPiecesOf(game.GetEnemyColour(colour))) { piece.RemoveAttackingPiece(this); piece.UpdateChecks(); piece.UpdatePins(); piece.UpdateLegalMoves(); piece.UpdateAttackingPieces(true); }

        if (type == Type.King) UpdatePinsSurroundingPieces();

        UpdatePins();
        UpdateLegalMoves();
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }


    public void UpdateLegalMoves()
    {
        if (dead) return;
        GUI.DebugInfo.pieceCalculationsRunning += 1;

        //game.legalMoves[this].Clear();

        //game.controlledSquares[colour][id].Clear();
        UpdatePins();
        game.RemoveControlledSquares(this);
        game.legalMoves[this].Clear();

        _GenerateMoves();
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }

    public void UpdateCastle()
    {
        if (dead) return;
        GUI.DebugInfo.pieceCalculationsRunning += 1;

        var king = game.GetPiecesOf(Type.King, colour)[0];
        if (hasMoved || king.hasMoved) { canCastle = false; return; }

        if (game.GetLine(position, king.position).firstPiece == king && position.y == 8 || position.y == 1) canCastle = true;
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }

    public void UpdateChecks()
    {
        GUI.DebugInfo.pieceCalculationsRunning += 1;
        if (type == Type.King && game.GetControlledSquares(game.GetEnemyColour(colour)).Contains(position))
        {
            Debug.Log($"{colour}{type} Checked");
            game.checkKing(this);

            foreach (var attackingPiece in piecesThatAttack)
            {
                if (attackingPiece.colour == colour) continue;

                var line = game.GetLine(from: attackingPiece.position, inDirectionOf: position);
                if (line.lineVectors is null) continue;
                game.pinLines.TryAdd(this, new Dictionary<Piece, HashSet<Vector2>>()); game.pinLines[this].TryAdd(attackingPiece, new HashSet<Vector2>(line.lineVectors));
            }

            game.pinLines.TryAdd(this, new Dictionary<Piece, HashSet<Vector2>>());
            
            foreach (var piece in game.GetPiecesOf(colour))
            {
                //if (piece.id == id) continue;
                piece.UpdatePins();
                piece.UpdateLegalMoves();
            }

        }
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }

    public bool RemoveAttackingPiece(Piece attackingPiece)
    {
        return piecesThatAttack.Remove(attackingPiece);
    }
    public void UpdateAttackingPieces(bool DoRemove)
    {
        foreach (var piece in new HashSet<Piece>(piecesThatAttack)) { if (DoRemove) { piece.RemoveAttackingPiece(this); } piece.UpdatePins(); piece.UpdateLegalMoves(); }
    }
    public void UpdatePins()
    {
        game.pinLines[this].Clear();
        if (dead) return;

        GUI.DebugInfo.pieceCalculationsRunning += 1;

        int count = 0;

        foreach (var attackingPiece in piecesThatAttack)
        {
            count++;
            if (attackingPiece.type == Type.Knight || attackingPiece.type == Type.King || attackingPiece.type == Type.Pawn || attackingPiece.colour == colour) continue;

            var line = game.GetLine(from: attackingPiece.position, inDirectionOf: position, phaseThroughPiece: this);

            if (line.firstPiece is not null && line.firstPiece?.colour == colour) {

                //Debug.Log($"{colour}{type}, pos:{position} HAS BEEN PINNED | to: {line.firstPiece.colour}{line.firstPiece.type}, pos:{line.firstPiece.position} | by: {attackingPiece.colour}{attackingPiece.type} pos:{attackingPiece.position}");
                game.pinLines[this].TryAdd(line.firstPiece, line.lineVectors);
            }
        }
        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }

    void UpdatePinsSurroundingPieces()
    {
        foreach (var direction in directions)
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
        if (king.piecesThatAttack.Count == 0) return true;

        bool isKing = type == Type.King;
        HashSet<Vector2> enemyControlledSquares = game.GetControlledSquares(game.GetEnemyColour(colour));

        if (enemyControlledSquares.Contains(move) && isKing) return false;

        bool isPinnedToKing = game.pinLines[this].ContainsKey(king);

        int checkBlockCounter = 0; int linecount = 0;
        //check if move blocks every king check
        foreach (var checkingPiece in king.piecesThatAttack)
        {
            if (checkingPiece.colour == colour) continue;

            if (checkingPiece.type == Type.Knight) {
                HashSet<Vector2> knightMoves = game.GetControlledSquares(checkingPiece);

                if ((isKing && knightMoves.Contains(move)) || (!isKing && move != checkingPiece.position)) return false;
                else return true;
            }

            var line = game.GetLine(checkingPiece.position, king.position);

            linecount++;

            if (isKing) {
                if (line.combinedLine.Contains(move) && move != checkingPiece.position) return false;

                if (move == checkingPiece.position || !line.combinedLine.Contains(move)) { checkBlockCounter++; }
                else { return false; }

                continue;
            }

            if (line.lineVectors is null) continue;
            //Debug.Log(line.combinedLine.Serialize());

            ////Debug.Log($"KingCheckLineCalc. Piece:{colour}{type} MOVE:{move} checkLine:{line.ToSeparatedString(",")} LINECONTAINSMOVE:{line.Contains(move)} isPinnedToKing{isPinnedToKing} |  id:{id} pos:{position}  |  checkingPiece:{checkingPiece.colour}{checkingPiece.type}");

            //Debug.Log(checkingPiece.type);
            if (line.lineVectors.Contains(move)) checkBlockCounter++;

            if (checkBlockCounter != linecount) return false;
        }

        //allows move if it blocks all checks; and if it is pinned to the king- doesn't reveal another check by moving off of the file/rank that pins it to the king
        if (!isPinnedToKing || (game.pinLines[this].TryGetValue(king, out var kingPinLine) && kingPinLine.Contains(move))) {
            //Debug.Log($"LEGALCHECKBLOCKINGMOVEFOUND, {colour}{type} pos:{position} move:{move}"); 
            return true; }

        return false;
    }

    public Dictionary<int, List<Vector2>> controlledSquaresChangeCache = new Dictionary<int, List<Vector2>>(8);

    Queue<Piece> attackedPieceUpdateQueue = new Queue<Piece>();


    public static Vector2[] directions = { new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
    void _GenerateMoves()
    {
        GUI.DebugInfo.pieceCalculationsRunning += 1;
        //Index move direction Visualization, P = Piece
        //   5 4 6
        //   3 P 1
        //   8 2 7
        //
        controlledSquaresChangeCache.Clear();

        if (type == Type.King && !hasMoved && position.y == 1 || position.y == 8) {
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

            //controlled squares cache in each direction, adds it for a cool gui effect if the direction contains new moves
            Dictionary<int, List<Vector2>> csDirCache = new Dictionary<int, List<Vector2>>(9);

            bool newMoveInDirectionFound = false;

            Vector2 move = position;

            for (int depth = 0; depth < directionDepthLimit; depth++)
            {
                csDirCache.TryAdd(depth+1, new List<Vector2>(8));
                if (depth >= directionDepthLimit) break;

                
                move += directions[(int)(direction - 1 + pawnReversingVariable)];


                if (move.x < 1 || move.y < 1 || move.x > 8 || move.y > 8) break;
                if (type == Type.King && game.GetControlledSquares(game.GetEnemyColour(colour)).Contains(move)) break;

                Piece pieceOnNewSquare = game.GetPieceAtPosition(move);


                ////Debug.Log($"check:{game.isInCheck} CalledFrom:{colour} ");

                if (game.checkedColour == colour)
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
                        else if (!game.GetGhostPawnAtPosition(move)) { 
                            game.AddControlledSquare(this, move); 

                            csDirCache[depth+1].Add(move); 
                            if (!GUI.controlledSquaresCache[id].Contains(move) && !controlSquareSkip) { newMoveInDirectionFound = true; } 
                            break; 
                        }
                    }
                    else if (pieceOnNewSquare is not null)
                    {
                        if (direction == Direction.up) break;
                    }
                }
                //


                if (!isMoveBlockedByKingPin) {
                    if (!controlSquareSkip) { game.AddControlledSquare(this, move); csDirCache[depth+1].Add(move); }

                    if (!GUI.controlledSquaresCache[id].Contains(move) && !controlSquareSkip) { newMoveInDirectionFound = true; }

                    if (pieceOnNewSquare is null)
                    {
                        game.legalMoves[this].Add(move); 
                        continue; 
                    }
                    else if (pieceOnNewSquare is not null)
                    {
                        bool pieceIsEnemy = pieceOnNewSquare.colour != colour;
                        bool alreadyAddedAttackingPiece = pieceOnNewSquare.piecesThatAttack.Contains(this);
                        bool added = pieceOnNewSquare.piecesThatAttack.Add(this);

                        if (!alreadyAddedAttackingPiece && added) attackedPieceUpdateQueue.Enqueue(pieceOnNewSquare);

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
            if(newMoveInDirectionFound)
            {
                foreach (var depth in csDirCache)
                {
                    bool added = controlledSquaresChangeCache.TryAdd(depth.Key, new List<Vector2>(24));

                    depth.Value.ForEach(p => controlledSquaresChangeCache[depth.Key].Add(p));
                }
            }

        }
        //Debug.Log($"{id} contrlcache: " + game.controlledSquaresCache[id].Count); Debug.Log($"{id} changecache: " + controlledSquaresChangeCache.Count);

        //if (attackedPieceUpdateQueue.Count > 0) Debug.Log($"{colour.HumanName()} {type.HumanName()} X{position.x} Y{position.y}  {attackedPieceUpdateQueue.Serialize()}");

        while (attackedPieceUpdateQueue.Count > 0)
        {
            attackedPieceUpdateQueue.Dequeue().Attacked();
        }

        GUI.DebugInfo.pieceCalculationsRunning -= 1;
    }


    bool PinBlocksMove(Vector2 move)
    {
        Piece king = game.GetPiecesOf(Type.King, colour)[0];
        bool? moveCausesCheckMate = game.pinLines[this].TryGetValue(king, out var kingPinLine) && !kingPinLine.Contains(move);

        ////Debug.Log($"piece:{colour}{type} PinTest, move:{move} causesCheckMate?:{moveCausesCheckMate} pinLines:{game.pinLines[this].ToSeparatedString(", ")}");

        return (bool)moveCausesCheckMate;
    }

    public void GUIPulseChangedControlledSquares()
    {
        if ((GUI.Settings.Debug.debugKey == KeyCode.W && colour == Colour.White) || (GUI.Settings.Debug.debugKey == KeyCode.B && colour == Colour.Black) || GUI.Settings.Debug.debugKey == KeyCode.A)
        {
            GUI.changedPiecesMoves.TryAdd(id, new Dictionary<int, List<Vector2>>());
            GUI.changedPiecesMoves[id].Clear();
            bool runThisShit = false;

            if (controlledSquaresChangeCache.Count > 0)
            {
                bool Added = GUI.changedPiecesMoves[id].TryAdd(0, new List<Vector2>());
                if (Added) GUI.changedPiecesMoves[id][0].Add(position);
            }

            foreach (var controlledSquareInCache in GUI.controlledSquaresCache[id])
            {
                foreach (var direction in controlledSquaresChangeCache)
                {
                    if (!direction.Value.Contains(controlledSquareInCache))
                    {
                        bool added = GUI.changedPiecesMoves[id].TryAdd(direction.Key, new List<Vector2>(24));
                        if (added)
                        {
                            GUI.changedPiecesMoves[id][direction.Key] = direction.Value;
                            runThisShit = true;
                        }
                    }
                }
            };
            controlledSquaresChangeCache.Clear();
            if (runThisShit)
            {
                GUI.GUIDrawPieceControlledSquares(this);
            }
            _guiChangedMovesShown = true;
        }
    }
}
