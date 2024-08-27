using NUnit.Framework.Constraints;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class Game : MonoBehaviour
{

    public Dictionary<Vector2, Piece> Board = new Dictionary<Vector2, Piece>();
    [SerializeField] private Storage storage;

    public int move = 0;
    

    Piece _pieceSelected;
    Piece pieceSelected { get { return _pieceSelected; } set { _pieceSelected = value; drawMoves(); } }
    

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pos = storage.camera.ScreenToWorldPoint(Input.mousePosition);

            for (int i = 0; i < 2; i++) { pos[i] = Mathf.Round(pos[i]); }

            if (pos.x <= 8 && pos.x >= 1 && pos.y <= 8  && pos.y >= 1) MousePressedBoard(pos);
        }
    }

    public Piece GetPiece(Vector2 pos)
    {
        Piece piece;
        var squareHasPiece = Board.TryGetValue(pos, out piece);

        return squareHasPiece == true ? piece : null;
    }
    public bool ValidateMove(Piece piece, Vector2 position)
    {
        return piece.legalMoves.Contains(position);
    }


    private void Start()
    {
        foreach(var piece in GameObject.FindGameObjectsWithTag("Piece"))
        {
            Vector2 pos = piece.transform.position;

            Board[pos] = piece.GetComponent<Piece>();
            Board[pos].position = pos;
            Board[pos].UpdateLegalMoves();
        }
    }

    public void Move(Piece piece, Vector2 position)
    {
        piece.transform.position = new Vector3(position.x, position.y, -1);
        Board[pieceSelected.position] = null;
        pieceSelected.position = position;

        Board[position] = pieceSelected;
        pieceSelected.UpdateLegalMoves();

        if (pieceSelected.type == global::Piece.Type.Pawn) pieceSelected.pawnMoved = true;

        pieceSelected = null;
        EndTurn();
    }

    void MousePressedBoard(Vector2 squarePressed)
    {
        //Debug.Log($"{squarePressed.ToString()} pressed");

        Piece piece;

        bool isAPieceOnClickedSquare = Board.TryGetValue(squarePressed, out piece);

        if (pieceSelected && piece?.colour != Piece.Colour.White)
        {
            if ( ValidateMove(pieceSelected, squarePressed) ) Move(pieceSelected, squarePressed);
            return;
        } 
        else if (isAPieceOnClickedSquare && !pieceSelected && piece?.colour == Piece.Colour.White)
        {
            piece.UpdateLegalMoves();
            pieceSelected = piece;
            return;
        }
        pieceSelected = null;
    }

    void drawMoves()
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

    void EndTurn()
    {

    }
}
