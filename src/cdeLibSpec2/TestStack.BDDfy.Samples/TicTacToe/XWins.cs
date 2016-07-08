using Shouldly;
using TestStack.BDDfy;

namespace cdeLibSpec2.TestStack.BDDfy.Samples.TicTacToe
{
    public class XWins : GameUnderTest
    {
        [RunStepWithArgs(
                new[] { X, X, O },
                new[] { X, X, O },
                new[] { O, O, N },
                StepTextTemplate = BoardStateTemplate)]
        void GivenTheFollowingBoard(string[] firstRow, string[] secondRow, string[] thirdRow)
        {
            Game = new Game(firstRow, secondRow, thirdRow);
        }

        void WhenXPlaysInTheBottomRight()
        {
            Game.PlayAt(2, 2);
        }

        void ThenTheWinnerShouldBeX()
        {
            Game.Winner.ShouldBe(X);
        }
    }
}