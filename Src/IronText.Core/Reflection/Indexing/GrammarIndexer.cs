﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronText.Reflection.Indexing
{
    class GrammarIndexer : IGrammarIndexer
    {
        private readonly Grammar grammar;

        public GrammarIndexer(Grammar grammar)
        {
            this.grammar = grammar;
        }

        public int IndexOf(SymbolBase symbol)
        {
            return symbol.Index;
        }

        public int IndexOf(Production production)
        {
            return production.Index;
        }

        public SymbolBase GetAnySymbol(int symbolIndex)
        {
            return grammar.Symbols[symbolIndex];
        }

        public Symbol GetSymbol(int symbolIndex)
        {
            return (Symbol)grammar.Symbols[symbolIndex];
        }

        public Production GetProduction(int prodIndex)
        {
            return grammar.Productions[prodIndex];
        }
    }
}