using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class SmokeTests
    {
        [Test]
        public void GridPos_Neighbours_AreOrthogonal()
        {
            var a = new GridPos(2, 3);
            Assert.IsTrue(a.IsOrthogonalNeighbourOf(new GridPos(2, 4)));
            Assert.IsTrue(a.IsOrthogonalNeighbourOf(new GridPos(1, 3)));
            Assert.IsFalse(a.IsOrthogonalNeighbourOf(new GridPos(3, 4)));
            Assert.IsFalse(a.IsOrthogonalNeighbourOf(a));
        }
    }
}
