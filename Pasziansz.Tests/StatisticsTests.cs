using Pasziansz.Game;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>A statisztika (Statistics) számítási logikájának tesztjei.</summary>
public class StatisticsTests
{
    [Fact]
    public void NewStatistics_IsEmpty()
    {
        var stats = new Statistics();

        Assert.Equal(0, stats.GamesStarted);
        Assert.Equal(0, stats.GamesWon);
        Assert.Equal(0, stats.WinRate);
        Assert.Equal("–", stats.BestTimeDisplay);
        Assert.Equal("–", stats.BestScoreDisplay);
        Assert.Equal("–", stats.FewestMovesDisplay);
        Assert.Null(stats.LastWinDate);
    }

    [Fact]
    public void WinRate_IsPercentageOfStartedGames()
    {
        var stats = new Statistics { GamesStarted = 4, GamesWon = 1 };

        Assert.Equal(25, stats.WinRate);
    }

    [Fact]
    public void RegisterGameStarted_IncrementsCounter()
    {
        var stats = new Statistics();

        stats.RegisterGameStarted();
        stats.RegisterGameStarted();

        Assert.Equal(2, stats.GamesStarted);
    }

    [Fact]
    public void RegisterWin_CountsByDrawMode()
    {
        var stats = new Statistics();

        stats.RegisterWin(100, 60, 120, 1);
        stats.RegisterWin(80, 90, 150, 3);

        Assert.Equal(2, stats.GamesWon);
        Assert.Equal(1, stats.WinsWithOneCardDraw);
        Assert.Equal(1, stats.WinsWithThreeCardDraw);
    }

    [Fact]
    public void RegisterWin_KeepsBestValues()
    {
        var stats = new Statistics();

        stats.RegisterWin(100, 200, 300, 1);
        Assert.Equal(100, stats.BestScore);
        Assert.Equal(200, stats.BestTimeSeconds);
        Assert.Equal(300, stats.FewestMovesInWin);

        stats.RegisterWin(50, 300, 400, 1); // gyengébb mindenben
        Assert.Equal(100, stats.BestScore);
        Assert.Equal(200, stats.BestTimeSeconds);
        Assert.Equal(300, stats.FewestMovesInWin);

        stats.RegisterWin(150, 150, 250, 3);
        Assert.Equal(150, stats.BestScore);
        Assert.Equal(150, stats.BestTimeSeconds);
        Assert.Equal(250, stats.FewestMovesInWin);
    }

    [Fact]
    public void RegisterWin_ReportsNewRecord()
    {
        var stats = new Statistics();

        Assert.True(stats.RegisterWin(100, 100, 100, 1));   // minden első rekord
        Assert.False(stats.RegisterWin(90, 120, 130, 1));   // semmi nem jobb
        Assert.True(stats.RegisterWin(110, 130, 140, 1));   // jobb pontszám
    }

    [Fact]
    public void RegisterWin_SetsLastWinDate()
    {
        var stats = new Statistics();

        stats.RegisterWin(10, 10, 10, 1);

        Assert.NotNull(stats.LastWinDate);
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var stats = new Statistics();
        stats.RegisterGameStarted();
        stats.RegisterWin(100, 100, 100, 1);

        stats.Reset();

        Assert.Equal(0, stats.GamesStarted);
        Assert.Equal(0, stats.GamesWon);
        Assert.Equal(0, stats.BestScore);
        Assert.Null(stats.LastWinDate);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var stats = new Statistics { BestScore = 42 };

        var clone = stats.Clone();
        clone.BestScore = 7;

        Assert.Equal(42, stats.BestScore);
        Assert.Equal(7, clone.BestScore);
    }

    [Fact]
    public void BestTimeDisplay_FormatsAsMinutesAndSeconds()
    {
        var stats = new Statistics { BestTimeSeconds = 125 };

        Assert.Equal("02:05", stats.BestTimeDisplay);
    }
}
