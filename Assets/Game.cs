using NUnit.Framework.Constraints;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static Piece;
using static UnityEditor.PlayerSettings;
using System.Runtime.Serialization.Json;
using UnityEngine.Video;
using UnityEngine.Assertions.Must;
using System.Threading.Tasks;


public class Game : MonoBehaviour
{

    /// <summary>
    /// TODO: FIX HOW CHECK LINE IS NOT CREATED IN BETWEEN KING AND CHECKING PIECE, ONLY POSITION OF KING IS ADDED
    /// </summary>



    //Board Data
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public Dictionary<Vector2, Piece> Board = new Dictionary<Vector2, Piece>();
    public (Vector2? position, Piece? linkedPawn) enPassantGhostPawn;


    public Dictionary<Colour, Dictionary<int, //pieceId
        HashSet<Vector2>>> controlledSquares = new Dictionary<Colour, Dictionary<int, HashSet<Vector2>>>();

    public Dictionary<Piece.Type, Dictionary<int, Piece>> pieces = new Dictionary<Piece.Type, Dictionary<int, Piece>>();
    public Dictionary<Piece, HashSet<Vector2>> legalMoves = new Dictionary<Piece, HashSet<Vector2>>();
    public Dictionary<Piece,//pinnedPiece
        Dictionary<Piece, //pieceItIsPinnedTo
            HashSet<Vector2>>> pinLines = new Dictionary<Piece, Dictionary<Piece, HashSet<Vector2>>>(48);

    public Colour turnColour = Colour.White;
    public bool isInCheck = false;

    int move = 1;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



    //GUI Stuff
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Debug-String stuff, disable printDebugString in settings if to not be used
        HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)> checkLines = new HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)>();
        HashSet<(HashSet<Vector2> pinLine, Piece pinningPiece, Piece pinnedPiece, Piece pieceAtPinEnd)> pins = new HashSet<(HashSet<Vector2> pinLine, Piece pinningPiece, Piece pinnedPiece, Piece pieceAtPinEnd)>();
    //

    KeyCode debugKey = KeyCode.None;
    Piece pieceSelected;

    public Dictionary<int, string[]> moveNotations = new Dictionary<int, string[]>();

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



    //Board state dictionary, used for being able to reverse moves in engine look aheads and the gui
    Dictionary<
        int, //move 
        Dictionary<
            Colour, //turnColourA
            (
                Dictionary<Piece, HashSet<Vector2>> legalMoves,
                Dictionary<Vector2, Piece> board,
                Dictionary<Colour, Dictionary<int, HashSet<Vector2>>> controlledSquares,
                (Vector2? position, Piece? linkedPawn) enPassantGhostPawn,
                bool isInCheck
            )
        >
    > boardState = new Dictionary<int, Dictionary<Colour, (Dictionary<Piece, HashSet<Vector2>> legalMoves, Dictionary<Vector2, Piece> board, Dictionary<Colour, Dictionary<int, HashSet<Vector2>>> controlledSquares, (Vector2?, Piece?) enPassantGhostPawn, bool isInCheck)>>();
    //

    [SerializeField] private Storage storage;


    //TODO: make this a struct
    public static class Settings
    {
        public static class Debug {
            public static bool drawWhiteControlledSquares = true;
            public static bool drawWhiteSelectedPieceLegalSquares = true;

            public static bool drawBlackControlledSquares = true;
            public static bool drawBlackSelectedPieceLegalSquares = true;

            public static bool printDebugString = true;
        }
    }

    

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pos = storage.camera.ScreenToWorldPoint(Input.mousePosition);

            for (int i = 0; i < 2; i++) { pos[i] = Mathf.Round(pos[i]); }

            if (pos.x <= 8 && pos.x >= 1 && pos.y <= 8  && pos.y >= 1) _MousePressedBoard(pos);
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            debugKey = KeyCode.W;
            RefreshDebugSquares();
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            debugKey = KeyCode.B;
            RefreshDebugSquares();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            storage.board.RefreshSquares();
            debugKey = KeyCode.None;
        }
    }

    public void RefreshDebugSquares()
    {
        switch (debugKey)
        {
            case KeyCode.W: _DebugRefresh(Colour.White);  break;
            case KeyCode.B: _DebugRefresh(Colour.Black);  break;
            default:        break;
        }
    }

    void _DebugRefresh(Colour colour)
    {
        GUIDrawAllControlledSquares(colour);
        foreach (var piece in GetPiecesOf(colour))
        {
            RemoveControlledSquares(piece);
            piece.UpdatePins();
            piece.UpdateLegalMoves();
        }
        GUIDrawCheckLines(colour);
    }

    public void GenerateDebugString(Colour colour, Piece movingPiece, Vector2 movePosition)
    {
        if (Settings.Debug.printDebugString == false) return;

        string debugString = "";

        var controlledSquares = colour == Colour.White ? GetControlledSquares(Colour.White) : GetControlledSquares(Colour.Black);
        var checkLines = GetCheckLines(colour);
        //...
        

    }



    HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)> GetCheckLines(Colour colour)
    {
        if (this.checkLines.Count != 0) return this.checkLines;

        Piece king = GetPiecesOf(Piece.Type.King, colour)[0];

        HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)> tempCheckLines = new HashSet<(HashSet<Vector2> lineVectors, Piece checkingPiece)>();

        foreach (var checkingPiece in king.piecesThatAttack)
        {
            if (checkingPiece.colour == colour) continue;

            var checkLine = GetLine(from: checkingPiece.position, inDirectionOf: king.position).lineVectors;

            tempCheckLines.Add((lineVectors: checkLine, checkingPiece));
        }
        checkLines = tempCheckLines;
        return tempCheckLines;
    }

    void clearCaches()
    {
        checkLines.Clear();

    }

    public Piece GetPieceAtPosition(Vector2 pos)
    {
        Piece piece;
        return Board.TryGetValue(pos, out piece) == true ? piece : null;
    }

    public void AddControlledSquare(Piece piece, Vector2 pos)
    {
        controlledSquares[piece.colour][piece.id].Add(pos);
    }

    public (HashSet<Vector2>? lineVectors, Piece? firstPiece) GetLine(Vector2 from, Vector2 inDirectionOf)
    {
        Vector2 pos = from;
        Vector2 direction = DirectionFromTwoPoints(from, inDirectionOf);

        Debug.Log(direction);

        HashSet<Vector2> line = new HashSet<Vector2>();
        Piece firstPieceOnLine = null;

        while (pos.x >= 1 && pos.y >= 1 && pos.x <= 8 && pos.y <= 8)
        {
            line.Add(pos);
            pos += direction;
            var pieceOnSquare = GetPieceAtPosition(pos);

            ////Debug.Log($"fromPiece:{pieceOnSquare}, fromPiecePos:{pos}, inDirection:{direction}");
            if (pieceOnSquare?.position == from) continue;

            if (pieceOnSquare is not null) { firstPieceOnLine = pieceOnSquare; line.Add(pos); break; }
        }
        if (firstPieceOnLine is not null) { return (line, firstPieceOnLine); }
        else return (null, null);
    }

    public Vector2 DirectionFromTwoPoints(Vector2 point1, Vector2 point2)
    {
        var calculation = (point2 - point1);
        if (calculation.x > 1) calculation.x = 1;
        else if (calculation.x < -1) calculation.x = -1;

        if (calculation.y > 1) calculation.y = 1;
        else if (calculation.y < -1) calculation.y = -1;

        Debug.Log(calculation);
        return calculation;
    }

    public (HashSet<Vector2> lineVectors, Piece firstPiece) GetLine(Vector2 from, Vector2 inDirectionOf, Piece phaseThroughPiece)
    {
        Vector2 pos = from;

        HashSet<Vector2> line = new HashSet<Vector2>();

        Vector2 direction = DirectionFromTwoPoints(from, inDirectionOf);
        if (direction.x == 0 && direction.y == 0) { Debug.Log("GetLine Directions are both 0"); return (null, null); }

        Piece pieceBehind = null;

        while (pos.x >= 1 && pos.y >= 1 && pos.x <= 8 && pos.y <= 8)
        {
            line.Add(pos);
            pos += direction;
            var pieceOnSquare = GetPieceAtPosition(pos);
            if (pieceOnSquare?.position == from || pieceOnSquare == phaseThroughPiece) continue;


            if (pieceOnSquare is not null) { pieceBehind = pieceOnSquare; line.Add(pos); break; }
        }
        if (pieceBehind is not null) return (line, pieceBehind);
        else return (null, null);
    }

    public void ClearPinLines(Piece piece)
    {
        pinLines[piece].Clear();
    }

    public void RemoveControlledSquares(Piece piece)
    {
        if (guiControlledSquaresCache.ContainsKey(piece.id)) guiControlledSquaresCache[piece.id].Clear();
        controlledSquares[piece.colour][piece.id].Clear();
    }

    public HashSet<Vector2> GetControlledSquares(Piece piece)
    {
        controlledSquares[piece.colour].TryGetValue(piece.id, out var squares);
        return squares;
    }
    public HashSet<Vector2> GetControlledEnemySquares(Colour friendlyColour)
    {
        var squares = controlledSquares[GetEnemyColour(friendlyColour)].SelectMany(piece => piece.Value);
        return Enumerable.ToHashSet(squares);
    }
    public HashSet<Vector2> GetControlledSquares(Colour colour)
    {
        var squares = controlledSquares[colour].SelectMany(piece => piece.Value);
        return Enumerable.ToHashSet(squares);
    }

    public Piece[] GetPiecesOf(Piece.Type type, Colour colour)
    {        
        return pieces[type].Values.Where(x => x.colour == colour).ToArray();
    }
    public Piece[] GetPiecesOf(Piece.Type type)
    {
        return pieces[type].Values.ToArray();
    }
    public Piece[] GetPiecesOf(Colour colour)
    {
        return pieces.SelectMany(typeDict => typeDict.Value.Select(pieceDict => pieceDict.Value).Where(piece => piece.colour == colour)).ToArray();
    }

    public Piece GetGhostPawnAtPosition(Vector2 pos)
    {
        return enPassantGhostPawn.position == pos ? enPassantGhostPawn.linkedPawn : null;
    }

    public bool IsLegalMove(Piece piece, Vector2 position)
    {
        return legalMoves[piece].Contains(position);
    }
    public string GenerateMoveString(Piece piece, Vector2 StartSquare, Vector2 MoveSquare, bool isCapture, bool isCheckmate, bool isCheck, bool isCastle)
    {
        return $"{(isCastle ? MoveSquare.x == 7 ? "O-O" : "O-O-O" : ((piece.type == Piece.Type.Pawn && isCapture ? storage.board.GetFormattedColumn(StartSquare) : storage.board.GetFormattedType(piece.type)) + (isCapture ? "x" : "") + storage.board.GetFormattedColumn(MoveSquare) + MoveSquare.y))}{(isCheckmate ? "#" : isCheck ? "+" : "")}";
    }
    public void checkKing(Piece king)
    {
        Debug.Log($"KINGGOTCHECKED, piece:{king.colour}{king.type} id:{king.id} pos:{king.position}");
        isInCheck = true;
    }
    public void Checkmate()
    {
        Debug.Log($"{turnColour} Won!");
        //...
    }

    public Colour GetEnemyColour()
    {
        return turnColour == Colour.White ? Colour.Black : Colour.White;
    }
    public Colour GetEnemyColour(Colour colour)
    {
        return colour == Colour.White ? Colour.Black : Colour.White;
    }

    void _EndTurn()
    {
        if (checkLines.Count > 0) Debug.Log($"checkLinesCount:{checkLines.Count}");

        if (turnColour == Colour.White) { turnColour = Colour.Black; storage.movestext.text += ", "; }
        else { turnColour = Colour.White; move += 1; storage.movestext.text += " | " + $"Move {move}: "; }
        RefreshDebugSquares();
    }





    void Start()
    {

        int pieceId = 1;
        foreach (var Object in GameObject.FindGameObjectsWithTag("Piece"))
        {
            Vector2 pos = Object.transform.position;
            var piece = Object.GetComponent<Piece>();
            piece.id = pieceId;

            controlledSquares.TryAdd(piece.colour, new Dictionary<int, HashSet<Vector2>>(16));
            controlledSquares[piece.colour].Add(pieceId, new HashSet<Vector2>());

            legalMoves.TryAdd(piece, new HashSet<Vector2>());

            pinLines.TryAdd(piece, new Dictionary<Piece, HashSet<Vector2>>());

            pieces.TryAdd(piece.type, new Dictionary<int, Piece>());
            pieces[piece.type][pieceId] = piece;

            Board[pos] = piece;
            Board[pos].position = pos;


            pieceId += 1;
        }
        foreach (var piece in Board) { Debug.Log($"PIECEINITIALIZING, piece:{piece.Value.colour}{piece.Value.type} id:{piece.Value.id} pos:{piece.Value.position}"); piece.Value.UpdateLegalMoves(); };
    }
    void _MousePressedBoard(Vector2 squarePressed)
    {
        //Debug.Log($"{squarePressed.ToString()} pressed");

        Piece piece;

        bool isAPieceOnClickedSquare = Board.TryGetValue(squarePressed, out piece);

        Color32 guiClickColour = new Color32(0,0,0,0);
        float guiLerpSeconds = 1f;
        float guiHoldSeconds = 0.25f;
        int guiPulseRunTimes = 1;
        bool guiResetCache = false;
        
        //a piece is selected and player clicks on the selected piece | action: deselect piece
        if (pieceSelected && piece == pieceSelected) {
            storage.board.RefreshSquare(squarePressed);
            guiClickColour = new Color32(100, 90, 230, 255); guiHoldSeconds = 0f; guiPulseRunTimes = 1; guiResetCache = true;

            pieceSelected = null;
        }

        //a piece is selected and player clicks on an enemy piece | action: check if it is a legal move; and if so- make move
        else if (pieceSelected && piece?.colour != turnColour) {

            //reset current selected piece's square's colour smoothly
            GUIPulseSquare(pieceSelected.position, storage.board.GetSquareColor(pieceSelected.position), guiHoldSeconds, 0.5f, guiPulseRunTimes, true, false);

            if (IsLegalMove(pieceSelected, squarePressed)) { guiClickColour = new Color32(150, 200, 10, 255); guiPulseRunTimes = 1; guiLerpSeconds = 3f; MakeMove(pieceSelected, squarePressed, false, false); }
            else { guiClickColour = new Color32(210, 20, 0, 255); guiPulseRunTimes = 3; guiLerpSeconds = 0.1f; guiHoldSeconds = 0.1f; }
            pieceSelected = null; 
        }

        //a piece is clicked and it is a friendly piece | action: select piece
        else if (isAPieceOnClickedSquare && piece?.colour == turnColour) {

            if (pieceSelected) GUIPulseSquare(pieceSelected.position, storage.board.GetSquareColor(pieceSelected.position), guiHoldSeconds, 0.5f, guiPulseRunTimes, true, false);

            guiClickColour = new Color32(25, 90, 230, 255); guiLerpSeconds = 1f; guiPulseRunTimes = 1;

            pieceSelected = piece;
            piece.UpdateLegalMoves();
        }

        //none of the above are true ie: clicking on an enemy piece without a piece selected or clicking on an empty square without a piece selected | action: deselect piece
        else {

            if (pieceSelected) GUIPulseSquare(pieceSelected.position, storage.board.GetSquareColor(pieceSelected.position), guiHoldSeconds, 0.5f, guiPulseRunTimes, true, false);

            guiClickColour = new Color32(230, 0, 0, 255); guiPulseRunTimes = 3; guiLerpSeconds = 0.3f; guiHoldSeconds = 0f;
            pieceSelected = null;
        }

        GUIDrawLegalSquares();

        if (guiClickColour.a != 0) GUIPulseSquare(squarePressed, guiClickColour, guiHoldSeconds, guiLerpSeconds, guiPulseRunTimes, false, guiResetCache);
    }

    void MoveNotation(string moveString)
    {
        storage.movestext.text += moveString;

        moveNotations.TryAdd(move, new string[2]);
        moveNotations[move][turnColour == Colour.White ? 0 : 1] = moveString;
    }



    void _CastlingHandler(Vector2 KingCastlePosition)
    {
        foreach (var rook in GetPiecesOf(Piece.Type.Rook, turnColour))
        {
            if (!rook.canCastle) continue;

            switch (KingCastlePosition.x, rook.position.x)
            {
                case (7, 8): MakeMove(rook, new Vector2(rook.position.x - 2, rook.position.y), true, true); rook.hasMoved = true; break;
                case (3, 1): MakeMove(rook, new Vector2(rook.position.x + 3, rook.position.y), true, true); rook.hasMoved = true; break;
                default: break;
            }
        }
    }
    void _CastlingHandler()
    {
        if (GetPiecesOf(Piece.Type.King, turnColour)[0].hasMoved) return;

        foreach (var rook in GetPiecesOf(Piece.Type.Rook, turnColour))
        {
            if (rook.hasMoved) continue;

            rook.UpdateCastle();
        }
    }

    public void MakeMove(Piece pieceToMove, Vector2 position, bool bypassMoveNotation, bool bypassEndTurn)
    {
        //Debug.Log($"{pieceToMove.type.HumanName()}, {position}");

        
        isInCheck = false;
        clearCaches();


        Piece pieceOnSquare = GetPieceAtPosition(position);

        //en passant stuff
        var enPassantPawn = GetGhostPawnAtPosition(position);
        if (pieceOnSquare is null && pieceToMove.type == Piece.Type.Pawn && enPassantPawn is not null) pieceOnSquare = enPassantPawn;

        enPassantGhostPawn = (null, null);

        bool isPawnMoving2SquaresForward = pieceToMove.type == Piece.Type.Pawn && Mathf.Abs(position.y - pieceToMove.position.y) == 2;
        if (isPawnMoving2SquaresForward) { enPassantGhostPawn.position = pieceToMove.position + new Vector2(0, pieceToMove.colour == Colour.White ? 1 : -1); enPassantGhostPawn.linkedPawn = pieceToMove; }
        //

        bool isCastle = pieceToMove.type == Piece.Type.King && Mathf.Abs(position.x - pieceToMove.position.x) > 1;

        Board[pieceToMove.position] = null;
        Board[position] = null;

        pieceToMove.position = position;
        Board[position] = pieceToMove;

        if (isCastle) { _CastlingHandler(position); } else { _CastlingHandler(); }

        pieceToMove.hasMoved = true;

        pieceOnSquare?.Capture();
        pieceToMove.Moved();

        bool isCheck = isInCheck == true;
        bool isCheckMate = false;

        if (!bypassMoveNotation) MoveNotation( GenerateMoveString(pieceToMove, pieceToMove.position, position, pieceOnSquare ? true : false, pieceOnSquare?.type == Piece.Type.King, isCheck, isCastle) );

        if (!bypassEndTurn) { pieceSelected = null; _EndTurn(); }

        GUIDrawCheckLines(turnColour);
        pieceToMove.transform.position = new Vector3(position.x, position.y, -1);
    }






    Dictionary<Vector2, Color> guiSelectedSquaresCache = new Dictionary<Vector2, Color>(64);

    Dictionary<int, HashSet<Vector2>> guiControlledSquaresCache = new Dictionary<int, HashSet<Vector2>>(64);
    Piece pieceSelectedCache = null;

    public void GUIDrawLegalSquares()
    {
        if ((pieceSelected?.colour == Colour.White && !Settings.Debug.drawWhiteSelectedPieceLegalSquares) || (pieceSelected?.colour == Colour.Black && !Settings.Debug.drawBlackControlledSquares)) return;


        //Debug.Log($"selectedPiece:{pieceSelected?.id}, {cachedMove}, cachedSelectedPiece:{pieceSelectedCache?.id}");


        GUILoadCache();


        if (pieceSelectedCache != pieceSelected)
        {
            foreach(var square in guiSelectedSquaresCache)
            {
                storage.board.RefreshSquare(square.Key);
                storage.board.squares[square.Key].material.color = square.Value;
            }

            guiSelectedSquaresCache.Clear();
            pieceSelectedCache = pieceSelected;

            if (pieceSelected)
            {
                storage.board.squares[pieceSelected.position].material.color = storage.board.selectColor;
                pieceSelectedCache = pieceSelected;


                foreach (var pos in legalMoves[pieceSelected])
                {

                    Color cachedColor = storage.board.squares[pos].material.color;

                    storage.board.squares[pos].material.color = storage.board.legalMovesColor / 1f;
                    guiSelectedSquaresCache.TryAdd(pos, cachedColor);

                }
            }
        }
    }

    void GUILoadCache()
    {

        foreach (var Tile in guiSelectedSquaresCache)
        {
            storage.board.squares[Tile.Key].material.color = Tile.Value;
        }
    }



    public void GUIUpdatePiece(Piece piece)
    {
        
    }



    public void GUIDrawPieceControlledSquares(Piece piece)
    {
        if ((piece.colour == Colour.White && !Settings.Debug.drawWhiteControlledSquares) || (piece.colour == Colour.Black && !Settings.Debug.drawBlackControlledSquares)) return;

        if (!guiControlledSquaresCache.ContainsKey(piece.id)) { guiControlledSquaresCache.TryAdd(piece.id, new HashSet<Vector2>(96)); }

        
        Color blackColor = new Color(0.07f, 0.03f, 0.02f);
        Color whiteColor = new Color(0.01f, 0.02f, 0.13f);

        var controlledSquares = GetControlledSquares(piece);

        foreach (var position in guiControlledSquaresCache[piece.id])
        {
            storage.board.RefreshSquare(position);
        }
        guiControlledSquaresCache[piece.id].Clear();

        var squares = storage.board.squares;

        foreach (var square in controlledSquares) {
            squares[square].material.color += piece.colour == Colour.Black ? blackColor : whiteColor;
            guiControlledSquaresCache[piece.id].Add(square);
        }
    }

    public void GUIDrawAllControlledSquares(Colour colour)
    {
        Color blackColor = new Color(0.15f, 0f, 0f);
        Color whiteColor = new Color(0f, 0f, 0.15f);
        storage.board.RefreshSquares();
        foreach(var square in GetControlledSquares(colour))
        {
            storage.board.squares[square].material.color += colour == Colour.Black ? whiteColor : blackColor;
        }
    }

    public void GUIDrawLine(HashSet<Vector2> line, Color colour)
    {
        if (line is null) { Debug.Log("GUIDrawLine ERROR 1, line is null"); return; }
        var boardSquares = storage.board.squares;
        foreach(var position in line)
        {
            boardSquares[position].material.color = colour;
        }
    }


    public class Pulse
    {
        public Vector2 position;
        public Color32 colour;
        public float holdSeconds;
        public float secondsToReturn;
        public int howManyTimes;
        public bool doInReverse;

        public Color32 cachedColour;

        public Pulse(Vector2 _position, Color32 _colour, float _secondsBeforeReturning, float _secondsToReturn, int _howManyTimes, bool _doInReverse)
        {
            position = _position; colour = _colour; holdSeconds = _secondsBeforeReturning; secondsToReturn = _secondsToReturn; howManyTimes = _howManyTimes; doInReverse = _doInReverse;
        }
    }




    public Dictionary<Vector2, Queue<Pulse>> pulseQueue = new Dictionary<Vector2, Queue<Pulse>>(16);

    public void GUIPulseSquare(Vector2 position, Color32 colour, float secondsBeforeReturning, float secondsToReturn, int howManyTimes, bool doInReverse, bool resetCache)
    {
        Pulse pulse = new Pulse(position, colour, secondsBeforeReturning, secondsToReturn, howManyTimes, doInReverse);

        _PulseInstanceHandler(pulse, resetCache);
    }

    async void _PulseInstanceHandler(Pulse pulse, bool resetCache)
    {
        if (!pulseQueue.ContainsKey(pulse.position)) pulseQueue[pulse.position] = new Queue<Pulse>();

        Queue<Pulse> queue = pulseQueue[pulse.position];

        //if there is a pulse still running on this square, add self to queue causing running queue to break itself, once it is it's turn in line run.
        if (queue.Count != 0)
        {
            queue.Enqueue(pulse);

            pulse.cachedColour = resetCache ? storage.board.squares[pulse.position].material.color : queue.Peek().cachedColour;

            while (!queue.Peek().Equals(pulse)) { await Task.Delay(50); }

            Pulse lastElement = queue.ToArray()[queue.Count - 1];

            if (!lastElement.Equals(pulse)) { queue.Dequeue(); return; }
            PulseSquare(pulse, queue);
        }
        else
        {
            pulse.cachedColour = storage.board.squares[pulse.position].material.color;
            queue.Enqueue(pulse);
            PulseSquare(pulse, queue);
        }
    }


    int ticksPerSecond = 50;
    async void PulseSquare(Pulse pulse, Queue<Pulse> queue)
    {
        for (int pulseIndex = 0; pulseIndex < pulse.howManyTimes; pulseIndex++)
        {
            var square = storage.board.squares[pulse.position].material;

            square.color = pulse.doInReverse ? square.color : pulse.colour;

            await Task.Delay((int)(pulse.holdSeconds * 1000));

            for (int tick = 0; tick < (ticksPerSecond * pulse.secondsToReturn); tick++)
            {
                Pulse lastElementInQueue = queue.ToArray()[queue.Count - 1];

                //break early if another pulse has been started on this square
                if (!lastElementInQueue.Equals(pulse))
                {
                    storage.board.squares[pulse.position].material.color = pulse.cachedColour;

                    queue.Dequeue();

                    return;
                }

                square.color = pulse.doInReverse ? Color32.Lerp(pulse.cachedColour, pulse.colour, tick / (ticksPerSecond * pulse.secondsToReturn)) : Color32.Lerp(pulse.colour, pulse.cachedColour, tick / (ticksPerSecond * pulse.secondsToReturn));

                await Task.Delay((int)((1 / (ticksPerSecond * (Time.deltaTime * 1000))) * 1000));
            }
        }

        //runs after all pulses have completed and were not interrupted
        storage.board.squares[pulse.position].material.color = pulse.doInReverse == true ? pulse.colour : pulse.cachedColour;

        queue.Dequeue();
        return;
    }





    public void GUIDrawCheckLines(Colour pieceColour)
    {

        Color checkLineColor = new Color(-0.2f, 0.5f, 0.9f);

        var checkLines = GetCheckLines(pieceColour);

        foreach (var checkLine in checkLines)
        {
            GUIDrawLine(checkLine.lineVectors, checkLineColor);
        }
    }
    
}
