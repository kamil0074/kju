namespace KJU.Tests.Automata
{
    using System.Collections.Generic;
    using System.Linq;
    using KJU.Core;
    using KJU.Core.Automata;
    using KJU.Tests.Util;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DfaMergerTests
    {
        public static TLabel GetTargetLabel<TLabel>(IDfa<TLabel> dfa, string s, TLabel defaultLabel)
        {
            IState state = dfa.StartingState();
            foreach (char ch in s)
            {
                var transitions = dfa.Transitions(state);
                if (!transitions.ContainsKey(ch))
                {
                    return defaultLabel;
                }

                state = transitions[ch];
            }

            return dfa.Label(state);
        }

        [TestMethod]
        public void TestSimple()
        {
            var a1 = new ConcreteDfa<bool>(); // a+
            a1.AddEdge(0, 'a', 1);
            a1.AddEdge(1, 'a', 1);
            a1.Labels[0] = false;
            a1.Labels[1] = true;

            var a2 = new ConcreteDfa<bool>(); // b+
            a2.AddEdge(0, 'b', 1);
            a2.AddEdge(1, 'b', 1);
            a2.Labels[0] = false;
            a2.Labels[1] = true;

            Assert.AreEqual(true, GetTargetLabel(a2, "bbb", false));
            Assert.AreEqual(false, GetTargetLabel(a2, string.Empty, false));

            var merged = DfaMerger<int>.Merge(
                new Dictionary<int, IDfa<bool>> { { 1, a1 }, { 2, a2 } },
                (labels) => labels.Count() == 0 ? 0 : labels.Min());

            Assert.AreEqual(1, GetTargetLabel(merged, "a", 0));
            Assert.AreEqual(2, GetTargetLabel(merged, "b", 0));
            Assert.AreEqual(0, GetTargetLabel(merged, string.Empty, 0));
            Assert.AreEqual(0, GetTargetLabel(merged, "baa", 0));
        }

        [TestMethod]
        public void TestConflict()
        {
            var a1 = new ConcreteDfa<bool>(); // a+
            a1.AddEdge(0, 'a', 1);
            a1.AddEdge(1, 'a', 1);
            a1.Labels[0] = false;
            a1.Labels[1] = true;

            var merged1 = DfaMerger<int>.Merge(
                new Dictionary<int, IDfa<bool>> { { 1, a1 }, { 2, a1 } },
                (labels) => labels.Count() == 0 ? 0 : labels.Min());

            Assert.AreEqual(0, GetTargetLabel(merged1, string.Empty, 0));
            Assert.AreEqual(1, GetTargetLabel(merged1, "a", 0));
            Assert.AreEqual(0, GetTargetLabel(merged1, "b", 0));
        }

        [TestMethod]
        public void TestSimple2()
        {
            var a1 = new ConcreteDfa<bool>(); // ab
            a1.AddEdge(0, 'a', 1);
            a1.AddEdge(1, 'b', 2);
            a1.Labels[0] = false;
            a1.Labels[1] = false;
            a1.Labels[2] = true;

            var a2 = new ConcreteDfa<bool>(); // (ab)*
            a2.AddEdge(0, 'a', 1);
            a2.AddEdge(1, 'b', 0);
            a2.Labels[0] = true;
            a2.Labels[1] = false;

            var merged1 = DfaMerger<int>.Merge(
                new Dictionary<int, IDfa<bool>> { { 1, a1 }, { 2, a2 } },
                (labels) => labels.Count() == 0 ? 0 : labels.Min());

            Assert.AreEqual(2, GetTargetLabel(merged1, string.Empty, 0));
            Assert.AreEqual(0, GetTargetLabel(merged1, "a", 0));
            Assert.AreEqual(1, GetTargetLabel(merged1, "ab", 0));
            Assert.AreEqual(2, GetTargetLabel(merged1, "abab", 0));
            Assert.AreEqual(2, GetTargetLabel(merged1, "ababab", 0));
            Assert.AreEqual(0, GetTargetLabel(merged1, "ababa", 0));
        }
    }
}
