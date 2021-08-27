using ColorShapeLinks.Common;
using ColorShapeLinks.Common.AI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DIABISMAI : AbstractThinker
{
    
 


    public override void Setup(string str)
    {
        // Try to get the maximum depth from the parameters
        if (!int.TryParse(str, out maxDepth))
        {
            // If not possible, set it to the default
            maxDepth = defaultMaxDepth;
        }

        // If a non-positive integer was provided, reset it to the default
        if (maxDepth < 1) maxDepth = defaultMaxDepth;
    }

    /// <summary>
    /// Returns the name of this AI thinker which will include the
    /// maximum search depth.
    /// </summary>
    /// <returns>The name of this AI thinker.</returns>
    public override string ToString()
    {  // Maximum Minimax search depth.
         
        private int maxDepth;

        public const int defaultMaxDepth = 1;
        return base.ToString() + "D" + maxDepth;
    }

    public override FutureMove Think(Board board, CancellationToken ct)
    {
        // Invoke minimax, starting with zero depth
        (FutureMove move, float score) decision =
            Minimax(board, ct, board.Turn, board.Turn, 0);

        // Return best move
        return decision.move;
    }


    private (FutureMove move, float score) Minimax(
        Board board, CancellationToken ct,
        PColor player, PColor turn, int depth)
    {
        // Move to return and its heuristic value
        (FutureMove move, float score) selectedMove;

        // Current board state
        Winner winner;

        // If a cancellation request was made...
        if (ct.IsCancellationRequested)
        {
            // ...set a "no move" and skip the remaining part of
            // the algorithm
            selectedMove = (FutureMove.NoMove, float.NaN);
        }
        // Otherwise, if it's a final board, return the appropriate
        // evaluation
        else if ((winner = board.CheckWinner()) != Winner.None)
        {
            if (winner.ToPColor() == player)
            {
                // AI player wins, return highest possible score
                selectedMove = (FutureMove.NoMove, float.PositiveInfinity);
            }
            else if (winner.ToPColor() == player.Other())
            {
                // Opponent wins, return lowest possible score
                selectedMove = (FutureMove.NoMove, float.NegativeInfinity);
            }
            else
            {
                // A draw, return zero
                selectedMove = (FutureMove.NoMove, 0f);
            }
        }
        // If we're at maximum depth and don't have a final board, use
        // the heuristic
        else if (depth == maxDepth)
        {
            selectedMove = (FutureMove.NoMove, Heuristic(board, player));
        }
        else // Board not final and depth not at max...
        {
            //...so let's test all possible moves and recursively call
            // Minimax() for each one of them, maximizing or minimizing
            // depending on who's turn it is

            //// Initialize the selected move...
            //selectedMove = turn == player
            //    // ...with negative infinity if it's the AI's turn and
            //    // we're maximizing (so anything except defeat will be
            //    // better than this)
            //    ? (FutureMove.NoMove, float.NegativeInfinity)
            //    // ...or with positive infinity if it's the opponent's
            //    // turn and we're minimizing (so anything except victory
            //    // will be worse than this)
            //    : (FutureMove.NoMove, float.PositiveInfinity);
            // Test each column on seperated thread.. we are separating in two parts equals
            (FutureMove move, float score)[] SelectedMoveForThreads = new (FutureMove move, float score)[7];
            int step = Cols / 7;
            Task[] tasks = new Task[7];
            tasks[0] = Task.Run(() => SelectedMoveForThreads[0] = SearchSolutionInIndexRange(0, step, board.Copy(), turn, player, depth, ct));
            tasks[1] = Task.Run(() => SelectedMoveForThreads[2] = SearchSolutionInIndexRange(step * 1, step*2, board.Copy(), turn, player, depth, ct));
            tasks[2] = Task.Run(() => SelectedMoveForThreads[3] = SearchSolutionInIndexRange(step * 2, step * 3, board.Copy(), turn, player, depth, ct));
            tasks[3] = Task.Run(() => SelectedMoveForThreads[4] = SearchSolutionInIndexRange(step * 3, step * 4, board.Copy(), turn, player, depth, ct));
            tasks[4] = Task.Run(() => SelectedMoveForThreads[5] = SearchSolutionInIndexRange(step * 4, step * 5, board.Copy(), turn, player, depth, ct));
            tasks[5] = Task.Run(() => SelectedMoveForThreads[5] = SearchSolutionInIndexRange(step * 5, step * 6, board.Copy(), turn, player, depth, ct));
            tasks[6] = Task.Run(() => SelectedMoveForThreads[6] = SearchSolutionInIndexRange(step * 6, step * 7, board.Copy(), turn, player, depth, ct));

            while (tasks.Any(t => !t.IsCompleted)) ;

            selectedMove = SelectedMoveForThreads.OrderByDescending(s => s.score).First();
        }

        // Return movement and its heuristic value
        return selectedMove;
    }

    private (FutureMove move, float score) SearchSolutionInIndexRange(int StartIndex, int FinishIndex, Board Board, PColor Turn, PColor Player, int Depth, CancellationToken CancelationToken)
    {
        if (CancelationToken.IsCancellationRequested)
        {
            return (FutureMove.NoMove, float.NaN);

        }

        if (Board.cols < FinishIndex || StartIndex < 0) throw new ArgumentOutOfRangeException();

        (FutureMove move, float score) SelectedMove = Turn == Player
                // ...with negative infinity if it's the AI's turn and
                // we're maximizing (so anything except defeat will be
                // better than this)
                ? (FutureMove.NoMove, float.NegativeInfinity)
                // ...or with positive infinity if it's the opponent's
                // turn and we're minimizing (so anything except victory
                // will be worse than this)
                : (FutureMove.NoMove, float.PositiveInfinity);

        for (int i = StartIndex; i < FinishIndex; i++)
        {
            if (Board.IsColumnFull(i)) continue;
            for (int j = 0; j < 2; j++)
            {
                // Get current shape
                PShape shape = (PShape)j;

                // Use this variable to keep the current board's score
                float eval;

                // Skip unavailable shapes
                if (Board.PieceCount(Turn, shape) == 0) continue;

                // Test move, call minimax and undo move
                Board.DoMove(shape, i);
                eval = Minimax(
                    Board, CancelationToken, Player, Turn.Other(), Depth + 1).score;
                Board.UndoMove();

                // If we're maximizing, is this the best move so far?
                if (Turn == Player
                    && eval >= SelectedMove.score)
                {
                    // If so, keep it
                    SelectedMove = (new FutureMove(i, shape), eval);
                }
                // Otherwise, if we're minimizing, is this the worst
                // move so far?
                else if (Turn == Player.Other()
                    && eval <= SelectedMove.score)
                {
                    // If so, keep it
                    SelectedMove = (new FutureMove(i, shape), eval);
                }
            }
        }
        return SelectedMove;
    }



    /// <summary>
    /// Naive heuristic function which previledges center board positions.
    /// </summary>
    /// <param name="Board">The game board.</param>
    /// <param name="color">
    /// Perspective from which the board will be evaluated.
    /// </param>
    /// <returns>
    /// The heuristic value of the given <paramref name="Board"/> from
    /// the perspective of the specified <paramref name="color"/.
    /// </returns>
    private float Heuristic(Board board, PColor color)
    {
        // Distance between two points
        float Dist(float x1, float y1, float x2, float y2)
        {
            return (float)Math.Sqrt(
                Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }

        // Determine the center row
        float centerRow = board.rows / 2;
        float centerCol = board.cols / 2;

        // Maximum points a piece can be awarded when it's at the center
        float maxPoints = Dist(centerRow, centerCol, 0, 0);

        // Current heuristic value
        float h = 0;

        // Loop through the board looking for pieces
        for (int i = 0; i < board.rows; i++)
        {
            for (int j = 0; j < board.cols; j++)
            {
                // Get piece in current board position
                Piece? piece = board[i, j];

                // Is there any piece there?
                if (piece.HasValue)
                {
                    // If the piece is of our color, increment the
                    // heuristic inversely to the distance from the center
                    if (piece.Value.color == color)
                        h += maxPoints - Dist(centerRow, centerCol, i, j);
                    // Otherwise decrement the heuristic value using the
                    // same criteria
                    else
                        h -= maxPoints - Dist(centerRow, centerCol, i, j);
                    // If the piece is of our shape, increment the
                    // heuristic inversely to the distance from the center
                    if (piece.Value.shape == color.Shape())
                        h += maxPoints - Dist(centerRow, centerCol, i, j);
                    // Otherwise decrement the heuristic value using the
                    // same criteria
                    else
                        h -= maxPoints - Dist(centerRow, centerCol, i, j);
                }
            }
        }
        // Return the final heuristic score for the given board
        return h;
    }

}


