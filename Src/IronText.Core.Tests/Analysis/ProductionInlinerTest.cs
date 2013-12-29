﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using IronText.Analysis;
using IronText.Framework;
using IronText.Framework.Reflection;
using NUnit.Framework;

namespace IronText.Tests.Analysis
{
    [TestFixture]
    public class ProductionInlinerTest
    {
        private EbnfGrammar originalGrammar;
        private EbnfGrammar resultGrammar;

        private Symbol start;
        private Symbol prefix;
        private Symbol suffix;
        private Symbol inlinedNonTerm;
        private Symbol term1;
        private Symbol term2;
        private Symbol term3;
        private Symbol term4;
        private Symbol term5;
        private Symbol nestedNonTerm;

        [SetUp]
        public void SetUp()
        {
            this.originalGrammar = new EbnfGrammar();
            var symbols = originalGrammar.Symbols;
            this.start  = symbols.Add("start");
            this.prefix = symbols.Add("prefix");
            this.suffix = symbols.Add("suffix");
            this.term1  = symbols.Add("term1");
            this.term2  = symbols.Add("term2");
            this.term3  = symbols.Add("term3");
            this.term4  = symbols.Add("term4");
            this.term5  = symbols.Add("term5");
            this.inlinedNonTerm = symbols.Add("inlinedNonTerm");
            this.nestedNonTerm = symbols.Add("nestedNonTerm");

            originalGrammar.Start = start;

            var prod = originalGrammar.Productions.Add(start,  new[] { prefix, inlinedNonTerm, suffix });
            prod.PlatformToAction.Set<TestPlatform>(new ProductionAction(0, 3));
        }

        [TearDown]
        public void TearDown()
        {
            Debug.WriteLine(resultGrammar);

            AssertGrammarStartIsPreserved();
            AssertInlinedSymbolsDoNotHaveProductions();
        }

        [Test]
        public void EpsilonIsInlined()
        {
            GivenInlinePatterns(new Symbol[0]);

            WhenGrammarIsInlined();

            AssertFlattenedProductionsPatternsAre(new[] { prefix, suffix });
            /*
            AssertInlinedActionContainsSimpleActions(
                new ProductionAction(2, 0),
                new ProductionAction(3, 0));
            */
        }

#if false
        private void AssertInlinedActionContainsSimpleActions(
            params ProductionAction[] productionActions)
        {
            var prod = resultGrammar.Symbols[start.Index].Productions.Single();
            int count = productionActions.Length;
            for (int i = 0; i != count; ++i)
            {
                var expectedAction = productionActions[i];
                ProductionAction gotAction = prod.Actions.First().Subactions[i];
                Assert.AreEqual(expectedAction.BackOffset, gotAction.BackOffset);
                Assert.AreEqual(expectedAction.ArgumentCount, gotAction.ArgumentCount);
            }
        }
#endif

        [Test]
        public void UnaryTerminalIsInlined()
        {
            GivenInlinePatterns(new [] { term1 });

            WhenGrammarIsInlined();

            AssertFlattenedProductionsPatternsAre(new[] { prefix, term1, suffix });
        }

        [Test]
        public void UnaryMultiTerminalsAreInlined()
        {
            GivenInlinePatterns(new [] { term1, term2, term3 });

            WhenGrammarIsInlined();

            AssertFlattenedProductionsPatternsAre(new[] { prefix, term1, term2, term3, suffix });
        }

        [Test]
        public void NestedEpsilonIsInlined()
        {
            GivenInlinePatterns(new [] { nestedNonTerm });
            GivenNestedInlinePatterns(new Symbol[0]);

            WhenGrammarIsInlined();

            AssertFlattenedProductionsPatternsAre(new[] { prefix, suffix });
        }

        [Test]
        public void NestedUnaryIsInlined()
        {
            GivenInlinePatterns(new Symbol[] { nestedNonTerm });
            GivenNestedInlinePatterns(new[] { term4 });

            WhenGrammarIsInlined();

            AssertFlattenedProductionsPatternsAre(new[] { prefix, term4, suffix });
        }

        [Test]
        public void NestedMultitokenIsInlined()
        {
            GivenInlinePatterns(new [] { nestedNonTerm });
            GivenNestedInlinePatterns(new [] { term4, term5 });

            WhenGrammarIsInlined();

            AssertFlattenedProductionsPatternsAre(new[] { prefix, term4, term5, suffix });
        }

        private void WhenGrammarIsInlined()
        {
            IProductionInliner target = new ProductionInliner(originalGrammar);
            this.resultGrammar = target.Inline();
        }

        private void GivenNestedInlinePatterns(params Symbol[][] nestedInlinePatterns)
        {
            foreach (var pattern in nestedInlinePatterns)
            {
                originalGrammar.Productions.Add(nestedNonTerm, pattern);
            }
        }

        private void GivenInlinePatterns(params Symbol[][] inlinePatterns)
        {
            foreach (var pattern in inlinePatterns)
            {
                originalGrammar.Productions.Add(inlinedNonTerm, pattern);
            }
        }

        private void AssertInlinedSymbolsDoNotHaveProductions()
        {
            Assert.AreEqual(
                0,
                resultGrammar.Symbols[inlinedNonTerm.Index].Productions.Count,
                "Inlined productions should be removed.");
        }

        private void AssertFlattenedProductionsPatternsAre(params Symbol[][] expectedFlattenedPatterns)
        {
            Assert.AreEqual(
                expectedFlattenedPatterns,
                resultGrammar.Symbols[start.Index].Productions.Select(p => p.Pattern).ToArray(),
                "Flattened patterns should have correct inlines.");
        }

        private void AssertGrammarStartIsPreserved()
        {
            Assert.IsNotNull(resultGrammar.Start);
            Assert.AreEqual(originalGrammar.Start.Index, resultGrammar.Start.Index);
        }

        interface TestPlatform { }
    }
}
