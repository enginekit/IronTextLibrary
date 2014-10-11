﻿using IronText.Collections;
using System;

namespace IronText.Reflection
{
    /// <summary>
    /// Represents 'Inherited Attribute' concpet from ECLR papers.
    /// </summary>
    /// <remarks>
    /// Inherited means that symbol will recieve some value from environment 
    /// before symbol even appear in stack.
    /// </remarks>
    [Serializable]
    internal class InheritedProperty : IndexableObject<IGrammarScope>
    {
        public InheritedProperty(Symbol symbol, string name)
        {
            this.Symbol = symbol;
            this.Name   = name;
        }

        public Symbol Symbol { get; private set; }

        public string Name   { get; private set; }
    }
}
