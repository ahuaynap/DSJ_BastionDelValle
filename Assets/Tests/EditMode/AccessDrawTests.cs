using NUnit.Framework;

public class AccessDrawTests
{
    [Test]
    public void FixedSeedRepeatsTheSameFourteenDraws()
    {
        const int seed = 12345;
        int[] first = AccessDraw.Sequence(seed, 2, 14);
        int[] second = AccessDraw.Sequence(seed, 2, 14);

        Assert.AreEqual(14, first.Length);
        CollectionAssert.AreEqual(first, second);

        for (int i = 0; i < first.Length; i++)
        {
            Assert.That(first[i], Is.EqualTo(0).Or.EqualTo(1));
        }
    }

    [Test]
    public void SeveralSeedsUseBothAccessesAndAllowRepeats()
    {
        bool sawUpper = false;
        bool sawLower = false;
        bool sawRepeat = false;

        for (int seed = 1; seed <= 40; seed++)
        {
            int[] draws = AccessDraw.Sequence(seed, 2, 14);

            for (int i = 0; i < draws.Length; i++)
            {
                sawUpper |= draws[i] == 0;
                sawLower |= draws[i] == 1;

                if (i > 0 && draws[i] == draws[i - 1])
                {
                    sawRepeat = true;
                }
            }
        }

        Assert.IsTrue(sawUpper);
        Assert.IsTrue(sawLower);
        Assert.IsTrue(sawRepeat);
    }

    [Test]
    public void InvalidDrawReturnsNoAccess()
    {
        Assert.AreEqual(-1, AccessDraw.Next(null, 2));
        Assert.AreEqual(-1, AccessDraw.Next(new System.Random(1), 0));
        Assert.AreEqual(0, AccessDraw.Sequence(7, 2, 0).Length);
    }
}
