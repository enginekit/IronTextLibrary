﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronText.Framework;
using IronText.Algorithm;
using IronText.Framework.Reflection;

namespace IronText.Compiler
{
    /// <summary>
    /// Prebuilds various tables related to <see cref="IronText.Framework.BnfGrammar"/>
    /// </summary>
    sealed class EbnfGrammarAnalysis
    {
        private readonly EbnfGrammar grammar;

        public EbnfGrammarAnalysis(EbnfGrammar grammar)
        {
            this.grammar = grammar;
        }

        /// <summary>
        /// Fewer values are less dependent to higher values 
        /// Relation of values is non-determined for two mutally 
        /// dependent non-terms.
        /// </summary>
        public int[] GetTokenComplexity()
        {
            var result = Enumerable.Repeat(-1, grammar.Symbols.Count).ToArray();
            var sortedTokens = Graph.ToplogicalSort(
                                new [] { EbnfGrammar.AugmentedStart },
                                GetDependantTokens)
                                .ToArray();
            for (int i = 0; i != sortedTokens.Length; ++i)
            {
                result[sortedTokens[i]] = i;
            }

            return result;
        }

        private IEnumerable<int> GetDependantTokens(int token)
        {
            foreach (var rule in grammar.Symbols[token].Productions)
            {
                foreach (int part in rule.Pattern)
                {
                    yield return part;
                }
            }
        }

        public string SymbolName(int token)
        {
            return grammar.Symbols[token].Name;
        }

        public bool IsTerminal(int token)
        {
            return grammar.Symbols[token].IsTerminal;
        }

        public IEnumerable<Production> GetProductions(int leftToken)
        {
            return grammar.Symbols[leftToken].Productions;
        }

        public SymbolCollection Symbols
        {
            get { return grammar.Symbols; }
        }

        public Precedence GetTermPrecedence(int token)
        {
            return grammar.Symbols[token].Precedence;
        }

        public Production AugmentedProduction
        {
            get { return grammar.AugmentedProduction; }
        }

        public Precedence GetProductionPrecedence(int prodId)
        {
            return grammar.Productions[prodId].Precedence;
        }

        public bool IsStartProduction(int ruleId)
        {
            return grammar.IsStartProduction(ruleId);
        }

        public BitSetType TokenSet
        {
            get { return grammar.TokenSet; }
        }

        public IEnumerable<AmbiguousSymbol> AmbiguousSymbols
        {
            get { return grammar.AmbiguousSymbols; }
        }

        public bool AddFirst(int[] tokenChain, int startIndex, MutableIntSet output)
        {
            return grammar.AddFirst(tokenChain, startIndex, output);
        }

        public bool HasFirst(int[] tokenChain, int startIndex, int token)
        {
            return grammar.HasFirst(tokenChain, startIndex, token);
        }

        public bool IsTailNullable(int[] tokens, int startIndex)
        {
            return grammar.IsTailNullable(tokens, startIndex);
        }

        public int DefineAmbToken(int mainToken, IEnumerable<int> tokens)
        {
            var ambSymbol = new AmbiguousSymbol(mainToken, tokens);
            grammar.Symbols.Add(ambSymbol);
            return ambSymbol.Index;
        }
    }
}
