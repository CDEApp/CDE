using NUnit.Framework;
using Shouldly;
using TestStack.BDDfy;

//using Xunit.Extensions;

namespace cdeLibSpec2.TestStack.BDDfy.Samples.TicTacToe
{
    [Story(
        AsA = "As a player",
        IWant = "I want to have a tic tac toe game",
        SoThat = "So that I can waste some time!")]
    public class TicTacToe : NewGame
    {
        class Cell
        {
            public Cell(int row, int col)
            {
                Row = row;
                Col = col;
            }

            public int Row { get; }
            public int Col { get; }

            public override string ToString()
            {
                return $"({Row}, {Col})";
            }
        }

        void GivenTheFollowingBoard(string[] firstRow, string[] secondRow, string[] thirdrow)
        {
            Game = new Game(firstRow, secondRow, thirdrow);
        }

        void ThenTheBoardStateShouldBe(string[] firstRow, string[] secondRow, string[] thirdrow)
        {
            Game.VerifyBoardState(firstRow, secondRow, thirdrow);
        }

        void ThenTheWinnerShouldBe(string expectedWinner)
        {
            Game.Winner.ShouldBe(expectedWinner);
        }

        void ThenItShouldBeACatsGame()
        {
            Game.Winner.ShouldBe(null);
        }

        void WhenTheGameIsPlayedAt(params Cell[] cells)
        {
            foreach (var cell in cells)
            {
                Game.PlayAt(cell.Row, cell.Col);
            }
        }

        [Test]
        public void CatsGame()
        {
            this.Given(s => s.GivenTheFollowingBoard(
                    new[] { X, O, X }, 
                    new[] { O, O, X },
                    new[] { X, X, O }), 
                    BoardStateTemplate)
                .Then(s => s.ThenItShouldBeACatsGame())
                .BDDfy("Cat's game");
        }

        [Test]
        public void WhenXAndOPlayTheirFirstMoves()
        {
            this.Given(s => s.GivenANewGame())
                .When(s => s.WhenTheGameIsPlayedAt(
                    new Cell(0, 0), new Cell(2, 2)), "When X and O play on {0}")
                .Then(s => s.ThenTheBoardStateShouldBe(
                    new[] { X, N, N }, 
                    new[] { N, N, N }, 
                    new[] { N, N, O }))
                .BDDfy();
        }

        [Test]
        public void OWins()
        {
            var cell = new Cell(2, 0);
            this.Given(s => s.GivenTheFollowingBoard(
                    new[] { X, X, O }, 
                    new[] { X, O, N }, 
                    new[] { N, N, N }))
                .When(s => s.WhenTheGameIsPlayedAt(cell))
                .Then(s => s.ThenTheWinnerShouldBe(O))
                .BDDfy();
        }

        [Test]
        public void XWins()
        {
            // This is implemented like this to show you the possibilities
            new XWins().BDDfy();
        }

        [Test]
        [TestCase("Vertical win in the right", 
            new[] { X, O, X }, 
            new[] { O, O, X }, 
            new[] { O, X, X }, X)]
        [TestCase("Vertical win in the middle", 
            new[] { N, X, O }, 
            new[] { O, X, O }, 
            new[] { O, X, X }, X)]
        [TestCase("Diagonal win", 
            new[] { X, O, O }, 
            new[] { X, O, X }, 
            new[] { O, X, N }, O)]
        [TestCase("Horizontal win in the bottom", 
            new[] { X, X, N }, 
            new[] { X, O, X }, 
            new[] { O, O, O }, O)]
        [TestCase("Horizontal win in the middle", 
            new[] { X, O, O }, 
            new[] { X, X, X }, 
            new[] { O, O, X }, X)]
        [TestCase("Vertical win in the left", 
            new[] { X, O, O }, 
            new[] { X, O, X }, 
            new[] { X, X, O }, X)]
        [TestCase("Horizontal win", 
            new[] { X, X, X }, 
            new[] { X, O, O }, 
            new[] { O, O, X }, X)]
        public void WinnerGame(string title, string[] firstRow, string[] secondRow, string[] thirdRow, string expectedWinner)
        {
            new WinnerGame(firstRow, secondRow, thirdRow, expectedWinner).BDDfy<TicTacToe>(title);
        }
    }
}
