using NUnit.Framework;

public class InterfaceRepairTests
{
    [Test]
    public void FiveCardsStayOnOneRowAtSixteenByNine()
    {
        RowPlan wide = SelectorLayout.Plan(1800f, 5, 12f, 220f);
        RowPlan narrow = SelectorLayout.Plan(900f, 5, 12f, 220f);

        Assert.AreEqual(1, wide.Rows);
        Assert.AreEqual(5, wide.FirstCount);
        Assert.Greater(wide.CardWidth, 220f);
        Assert.AreEqual(2, narrow.Rows);
        Assert.AreEqual(3, narrow.FirstCount);
        Assert.Greater(narrow.CardWidth, 220f);
    }

    [Test]
    public void PlayableBandKeepsTheLowerPlotAboveTheSelector()
    {
        const float top = 96f / 1080f;
        const float bottom = 336f / 1080f;
        FrameFit frame = PlayfieldFrame.Fit(
            -7.4f,
            7.4f,
            -4.4f,
            4.4f,
            top,
            bottom,
            16f / 9f,
            0.35f);
        PlayableBand band = PlayfieldFrame.Playable(frame, top, bottom);

        Assert.LessOrEqual(band.MinX, -6.5f);
        Assert.GreaterOrEqual(band.MaxX, 6.5f);
        Assert.LessOrEqual(band.MinY, -3.5f);
        Assert.GreaterOrEqual(band.MaxY, 3.5f);
    }

    [Test]
    public void StartAndVictoryFollowTheVisiblePhase()
    {
        Assert.AreEqual(MatchView.Preparation, MatchPhase.View(true, WaveState.Waiting));
        Assert.AreEqual(MatchView.Spawning, MatchPhase.View(true, WaveState.Spawning));
        Assert.AreEqual(MatchView.Combat, MatchPhase.View(true, WaveState.Active));
        Assert.AreEqual(MatchView.Result, MatchPhase.View(false, WaveState.Completed));
        Assert.AreEqual(MatchView.SpawnFailure, MatchPhase.View(true, WaveState.SpawnError));
        Assert.IsTrue(MatchPhase.AllowStart(true, WaveState.Waiting, false, false));
        Assert.IsFalse(MatchPhase.AllowStart(true, WaveState.Waiting, true, false));
        Assert.IsFalse(MatchPhase.AllowStart(true, WaveState.Spawning, false, true));
        Assert.IsFalse(MatchPhase.AllowStart(true, WaveState.Active, false, false));
        Assert.IsFalse(MatchPhase.AllowStart(false, WaveState.Waiting, false, false));
        Assert.IsFalse(MatchPhase.DeclareVictory(
            true, false, 1, 2, true, true, true));
        Assert.IsFalse(MatchPhase.DeclareVictory(
            true, false, 1, 2, false, true, false));
        Assert.IsTrue(MatchPhase.DeclareVictory(
            true, false, 1, 2, true, true, false));
        Assert.IsFalse(MatchPhase.DeclareVictory(
            true, false, 0, 2, true, true, false));
    }
}
