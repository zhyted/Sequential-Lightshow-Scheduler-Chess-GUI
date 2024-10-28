using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Piece;
using System.Threading.Tasks;
using static Game;
using Unity.VisualScripting;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel;


public class Game : MonoBehaviour
{

				/// <summary>
				/// TODO: FIX HOW CHECK LINE IS NOT CREATED IN BETWEEN KING AND CHECKING PIECE, ONLY POSITION OF KING IS ADDED
				/// </summary>



				//Board Data
				//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

				public Dictionary<Vector2, Piece> pieceBoard = new Dictionary<Vector2, Piece>();
				public (Vector2? position, Piece? linkedPawn) enPassantGhostPawn;


				public Dictionary<Colour, //piece Colour
											Dictionary<int, //pieceId
														HashSet<Vector2>>> controlledSquares = new Dictionary<Colour, Dictionary<int, HashSet<Vector2>>>();

				public Dictionary<Piece.Type, Dictionary<int, Piece>> pieces = new Dictionary<Piece.Type, Dictionary<int, Piece>>();

				public Dictionary<Piece, HashSet<Vector2>> legalMoves = new Dictionary<Piece, HashSet<Vector2>>();

				public Dictionary<Piece,//pinnedPiece
											Dictionary<Piece, //pieceItIsPinnedTo
														HashSet<Vector2>>> pinLines = new Dictionary<Piece, Dictionary<Piece, HashSet<Vector2>>>(48);

				HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)> checkLines = new HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)>();

				public Colour turnColour = Colour.White;
				public Colour checkedColour = Colour.None;

				public bool gameOver { get; private set; } = false;

				public int move { get; private set; } = 1;

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

				[SerializeField] public Storage storage;

				public static class Settings {

				}

				void Update()
				{
								
				}



				public HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)> GetCheckLines(Colour colour)
				{
					if (checkLines.Count != 0) return checkLines;

					Piece king = GetPiecesOf(Piece.Type.King, colour)[0];

								HashSet<(HashSet<Vector2>? lineVectors, Piece checkingPiece)> tempCheckLines = new HashSet<(HashSet<Vector2> lineVectors, Piece checkingPiece)>();

								foreach (var checkingPiece in king.piecesThatAttack)
								{
												if (checkingPiece.colour != king.colour)
												{
																var checkLine = GetLine(from: checkingPiece.position, inDirectionOf: king.position);

																if (checkLine.firstPiece == king) { tempCheckLines.Add((lineVectors: checkLine.lineVectors, checkingPiece)); }
												}
								}
								checkLines = tempCheckLines;
								return tempCheckLines;
				}

				public Piece GetPieceAtPosition(Vector2 pos)
				{
								Piece piece;
								return pieceBoard.TryGetValue(pos, out piece) == true ? piece : null;
				}

				public void AddControlledSquare(Piece piece, Vector2 pos)
				{
								controlledSquares[piece.colour][piece.id].Add(pos);
				}

				public (HashSet<Vector2>? lineVectors, HashSet<Vector2> combinedLine, Piece? firstPiece) GetLine(Vector2 from, Vector2 inDirectionOf)
				{
								Vector2 pos = from;
								Vector2 direction = DirectionFromTwoPoints(from, inDirectionOf);

								//Debug.Log(direction);

								HashSet<Vector2> line = null;
								HashSet<Vector2> combinedLine = new HashSet<Vector2>();
								Piece firstPieceOnLine = null;

								combinedLine.Add(pos);

								while (pos.x >= 1 && pos.y >= 1 && pos.x <= 8 && pos.y <= 8)
								{
												pos += direction;
												if (pos.x < 1 || pos.y < 1 || pos.x > 8 || pos.y > 8) break;

												combinedLine.Add(pos);

												var pieceOnSquare = GetPieceAtPosition(pos);

												////Debug.Log($"fromPiece:{pieceOnSquare}, fromPiecePos:{pos}, inDirection:{direction}");
												if (pieceOnSquare?.position == from) continue;

												if (pieceOnSquare is not null && firstPieceOnLine is null) { firstPieceOnLine = pieceOnSquare; line = new HashSet<Vector2>(); foreach (var vec in combinedLine) { line.Add(vec); } }
								}

								return (line, combinedLine, firstPieceOnLine);
				}

				Vector2 DirectionFromTwoPoints(Vector2 point1, Vector2 point2)
				{
								var calculation = (point2 - point1);
								if (calculation.x > 1) calculation.x = 1;
								else if (calculation.x < -1) calculation.x = -1;

								if (calculation.y > 1) calculation.y = 1;
								else if (calculation.y < -1) calculation.y = -1;

								//Debug.Log(calculation);
								return calculation;
				}

				public (HashSet<Vector2>? lineVectors, HashSet<Vector2> combinedLine, Piece? firstPiece) GetLine(Vector2 from, Vector2 inDirectionOf, Piece phaseThroughPiece)
				{
								//if (from is null || inDirectionOf is null || phaseThroughPiece is null) return (null, null, null);

								Vector2 pos = from;

								HashSet<Vector2> line = null;
								HashSet<Vector2> combinedLine = new HashSet<Vector2>();
								Piece firstPieceOnLine = null;

								Vector2 direction = DirectionFromTwoPoints(from, inDirectionOf);
								if (direction.x == 0 && direction.y == 0) { Debug.Log("GetLine Directions are both 0"); return (line, combinedLine, firstPieceOnLine); }

								combinedLine.Add(pos);

								while (pos.x >= 1 && pos.y >= 1 && pos.x <= 8 && pos.y <= 8)
								{
												pos += direction;
												if (pos.x < 1 || pos.y < 1 || pos.x > 8 || pos.y > 8) break;

												combinedLine.Add(pos);

												var pieceOnSquare = GetPieceAtPosition(pos);
												if (pieceOnSquare?.position == from || pieceOnSquare == phaseThroughPiece) continue;


												if (pieceOnSquare is not null && firstPieceOnLine is null) { firstPieceOnLine = pieceOnSquare; line = new HashSet<Vector2>(); foreach (var vec in combinedLine) { line.Add(vec); } }
								}

								return (line, combinedLine, firstPieceOnLine);
				}

				public void ClearPinLines(Piece piece)
				{
								pinLines[piece].Clear();
				}

				public void RemoveControlledSquares(Piece piece)
				{
								//if (guiControlledSquaresCache.ContainsKey(piece.id)) guiControlledSquaresCache[piece.id].Clear();
								controlledSquares[piece.colour][piece.id].Clear();
				}

				public HashSet<Vector2> GetControlledSquares(Piece piece)
				{
								controlledSquares[piece.colour].TryGetValue(piece.id, out var squares);
								return squares;
				}
				//public HashSet<Vector2> GetControlledEnemySquares(Colour friendlyColour)
				//{
				//    var squares = controlledSquares[GetEnemyColour(friendlyColour)].SelectMany(piece => piece.Value);
				//    return Enumerable.ToHashSet(squares);
				//}
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
								checkedColour = king.colour;
				}
				public void Checkmate()
				{
								storage.boardtext.text = $"Checkmate! \n {turnColour} Won In {move} Moves!";
								gameOver = true;
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

				public void _EndTurn()
				{
								checkLines.Clear();
								//if (checkLines.Count > 0) { //Debug.Log($"checkLinesCount:{checkLines.Count}");
								//

								//foreach (var piece in GetPiecesOf(Piece.Colour.Black)) { piece._resetGuiFlags(); }
								//foreach (var piece in GetPiecesOf(Piece.Colour.White)) { piece._resetGuiFlags(); }

								GUI.GUIUPDATE();

								bool checkmateCheck = true;
								foreach (var piece in GetPiecesOf(GetEnemyColour(turnColour))) {
												Debug.Log($"{piece.type.HumanName()}, {legalMoves[piece].Serialize()}");
												if (legalMoves[piece].Count > 0) { checkmateCheck = false; break; }
								}

								if (checkmateCheck) { Checkmate(); }
								else
								{
												if (turnColour == Colour.White) { turnColour = Colour.Black; storage.movestext.text += ", "; }
												else { turnColour = Colour.White; move += 1; storage.movestext.text += " | " + $"Move {move}: "; }
								}
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

												pieceBoard[pos] = piece;
												piece.position = pos;

												GUI.controlledSquaresCache.TryAdd(pieceId, new HashSet<Vector2>());

												pieceId += 1;
								}
								foreach (var piece in pieceBoard) { Debug.Log($"PIECEINITIALIZING, piece:{piece.Value.colour}{piece.Value.type} id:{piece.Value.id} pos:{piece.Value.position}"); piece.Value.UpdateAttackingPieces(true); piece.Value.UpdatePins(); piece.Value.UpdateChecks(); piece.Value.UpdateLegalMoves(); };
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

								//Debug.Log($"top makeMove csquares {controlledSquares[pieceToMove.colour][pieceToMove.id].Count}");
								//Debug.Log($"top makeMove cache {controlledSquaresCache[pieceToMove.id].Count}");

								checkedColour = Colour.None;


								Piece pieceOnSquare = GetPieceAtPosition(position);

								//en passant stuff
								var enPassantPawn = GetGhostPawnAtPosition(position);
								if (pieceOnSquare is null && pieceToMove.type == Piece.Type.Pawn && enPassantPawn is not null) pieceOnSquare = enPassantPawn;

								enPassantGhostPawn = (null, null);

								bool isPawnMoving2SquaresForward = pieceToMove.type == Piece.Type.Pawn && Mathf.Abs(position.y - pieceToMove.position.y) == 2;
								if (isPawnMoving2SquaresForward) { enPassantGhostPawn.position = pieceToMove.position + new Vector2(0, pieceToMove.colour == Colour.White ? 1 : -1); enPassantGhostPawn.linkedPawn = pieceToMove; }
								//

								bool isCastle = pieceToMove.type == Piece.Type.King && Mathf.Abs(position.x - pieceToMove.position.x) > 1;

								pieceBoard.Remove(pieceToMove.position);

								if (pieceOnSquare) { pieceBoard.Remove(pieceOnSquare.position); }
								else { pieceBoard.Remove(position); }

								pieceToMove.position = position;
								pieceBoard[position] = pieceToMove;

								if (isCastle) { _CastlingHandler(position); } else { _CastlingHandler(); }

								pieceToMove.hasMoved = true;

								pieceOnSquare?.Capture();
								pieceToMove.Moved();

								bool isCheck = checkedColour != Colour.None;
								//bool isCheckMate = false;

								if (!bypassMoveNotation) GUI.MoveNotation( GenerateMoveString(pieceToMove, pieceToMove.position, position, pieceOnSquare ? true : false, pieceOnSquare?.type == Piece.Type.King, isCheck, isCastle) );

								GUI.GUIEndTurn(pieceToMove, position, 0.04f);
				}
				
}
