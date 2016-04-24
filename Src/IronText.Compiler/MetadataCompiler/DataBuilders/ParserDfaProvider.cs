﻿using IronText.Automata.Lalr1;
using IronText.Compiler.Analysis;
using IronText.Logging;
using IronText.Runtime;

namespace IronText.MetadataCompiler
{
    class ParserDfaProvider
    {
        public ParserDfaProvider(
            ILogging        logging,
            ILanguageSource source,
            GrammarAnalysis analysis)
        {
            logging.WithTimeLogging(
                source.LanguageName,
                source.GrammarOrigin,
                () =>
                {
                    Dfa = new Lalr1Dfa(analysis, LrTableOptimizations.Default);
                },
                "building LALR1 DFA");
        }

        public ILrDfa Dfa { get; private set; }
    }
}