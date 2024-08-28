using NUnit.Framework.Constraints;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class Game : MonoBehaviour
{

    public Dictionary<Vector2, Piece> Board = new Dictionary<Vector2, Piece>();
    public Dictionary<Vector2, Piece> enPassantGhostPawns = new Dictionary<Vector2, Piece>();
    public Dictionary<string, Dictionary<string, Piece>> pieces = new Dictionary<string, Dictionary<string, Piece>>();

    [SerializeField] private Storage storage;

    public Piece.Colour turn = Piece.Colour.White;

    int move = 1;
    
    bool gameOver = false;

    public Dictionary<int, String[]> Moves = new Dictionary<int, String[]>();

    Piece _pieceSelected;
    Piece pieceSelected { get { return _pieceSelected; } set { _pieceSelected = value; _drawMoves(); } }
    

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !gameOver)
        {
            Vector2 pos = storage.camera.ScreenToWorldPoint(Input.mousePosition);

            for (int i = 0; i < 2; i++) { pos[i] = Mathf.Round(pos[i]); }

            if (pos.x <= 8 && pos.x >= 1 && pos.y <= 8  && pos.y >= 1) _MousePressedBoard(pos);
        }
    }

    public Piece GetPiece(Vector2 pos)
    {
        Piece piece;
        return Board.TryGetValue(pos, out piece) == true ? piece : null;
    }
    public Piece GetGhostPawn(Vector2 pos)
    {
        Piece piece;
        return enPassantGhostPawns.TryGetValue(pos, out piece) == true ? piece : null;
    }

    public bool ValidateMove(Piece piece, Vector2 position)
    {
        return piece.legalMoves.Contains(position);
    }


    void Start()
    {
        int pieceIndex = 10;
        foreach (var Object in GameObject.FindGameObjectsWithTag("Piece"))
        {
            Vector2 pos = Object.transform.position;
            var piece = Object.GetComponent<Piece>();

            pieces.TryAdd($"{piece.type}", new Dictionary<string, Piece>());
            pieces[$"{piece.type}"][$"{pieceIndex}{(piece.colour == Piece.Colour.White ? "W" : "B")}"] = piece;

            Board[pos] = piece;
            Board[pos].position = pos;
            Board[pos].UpdateLegalMoves();

            pieceIndex++;
        }
    }
    public string GetFormattedColumn(Vector2 position)
    {
        return position.x switch { 1 => "a", 2 => "b", 3 => "c", 4 => "d", 5 => "e", 6 => "f", 7 => "g", 8 => "h", _ => "uh this isn't supposed to happen" };
    }

    public string GenerateMoveString(Piece piece, Vector2 StartSquare, Vector2 MoveSquare, bool isCapture, bool isCheckmate, bool isCheck, bool isCastle)
    {
        return $"{ (isCastle ? MoveSquare.x == 7 ? "O-O" : "O-O-O" : ((piece.type == Piece.Type.Pawn && isCapture ? GetFormattedColumn(StartSquare) : piece.GetFormattedType()) + (isCapture ? "x" : "") + GetFormattedColumn(MoveSquare) + MoveSquare.y)) }{ (isCheckmate ? "#" : isCheck ? "+" : "") }";
    }

    public void Move(Piece pieceToMove, Vector2 position, bool quickMove)
    {
        bool isCastle = pieceToMove.type == Piece.Type.King && Mathf.Abs(pieceToMove.position.x - position.x) > 1;

        Piece pieceOnSquare;
        pieceOnSquare = GetPiece(position);

        //en passant stuff
        if (pieceOnSquare is null && pieceToMove.type == Piece.Type.Pawn) pieceOnSquare = GetGhostPawn(position);
        enPassantGhostPawns.Clear();
        if (pieceToMove.type == Piece.Type.Pawn && Mathf.Abs(position.y - pieceToMove.position.y) == 2) enPassantGhostPawns[pieceToMove.position + new Vector2(0, pieceToMove.colour == Piece.Colour.White ? 1 : -1)] = pieceToMove;
        //

        pieceOnSquare?.Capture();

        if (!quickMove)
        {
            string moveString = GenerateMoveString(pieceToMove, pieceToMove.position, position, pieceOnSquare ? true : false, pieceOnSquare?.type == Piece.Type.King, false, isCastle);
            storage.movestext.text += moveString;

            Moves.TryAdd(move, new string[2]);
            Moves[move][turn == Piece.Colour.White ? 0 : 1] = moveString;
        }

        Board[pieceToMove.position] = null;
        pieceToMove.position = position;
        Board[position] = pieceToMove;

        pieceToMove.hasMoved = true;

        //castling stuff
        foreach (var rook in pieces[$"{Piece.Type.Rook}"].Values)
        {
            if (isCastle && rook.colour == pieceToMove.colour && rook.hasMoved == false) { 
                rook.hasMoved = true; 
                switch (position.x, rook.position.x)
                {
                    case (7, 8): Move(rook, new Vector2(rook.position.x - 2, rook.position.y), true); break;
                    case (3, 1): Move(rook, new Vector2(rook.position.x + 3, rook.position.y), true); break;
                    default: break;
                }
            }

            if (!rook.hasMoved) rook.UpdateCastle();
        }

        pieceToMove.UpdateLegalMoves();
        pieceToMove.transform.position = new Vector3(position.x, position.y, -1);

        if (!quickMove)
        {
            pieceSelected = null;
            EndTurn();
        }
    }

    void _MousePressedBoard(Vector2 squarePressed)
    {
        //Debug.Log($"{squarePressed.ToString()} pressed");

        Piece piece;

        bool isAPieceOnClickedSquare = Board.TryGetValue(squarePressed, out piece);

        if (pieceSelected && piece?.colour != turn)
        {
            if ( ValidateMove(pieceSelected, squarePressed) ) Move(pieceSelected, squarePressed, false);
            return;
        } 
        else if (isAPieceOnClickedSquare && !pieceSelected && piece?.colour == turn)
        {
            piece.UpdateLegalMoves();
            pieceSelected = piece;
            return;
        }
        pieceSelected = null;
    }

    void _drawMoves()
    {
        if (pieceSelected)
        {
            storage.board.squares[pieceSelected.position].material.color = storage.board.selectColor;

            var tiles = storage.board.squares;

            foreach(var pos in pieceSelected.legalMoves)
            {
                tiles[pos].material.color = storage.board.legalMovesColor;
            }
        }
        else
        {
            storage.board.ColorRefresh();
        }
        
    }

    public void Checkmate()
    {
        Debug.Log($"{turn} Won!");
        gameOver = true;
    }

    void EndTurn()
    {

        if (turn == Piece.Colour.White) { turn = Piece.Colour.Black; storage.movestext.text += ", "; }
        else { turn = Piece.Colour.White; move += 1; storage.movestext.text += " | " + $"Move {move}: "; }
    }
}
