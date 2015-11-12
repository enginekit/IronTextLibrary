﻿using System;
using System.Text;

namespace IronText.Reflection
{
    /// <summary>
    /// Ambiguous terminal. Represents unresolved alternative betwen 2 or more symbols.
    /// </summary>
    /// <remarks>
    /// Alternative can be resolved in a deterministic way when current parser state 
    /// allows only single alternative or by a forking extended-GLR parsing paths.
    /// </remarks>
    [Serializable]
    public sealed class AmbiguousTerminal : ITerminal
    {
        public AmbiguousTerminal(Symbol[] symbols)
        {
            this.Alternatives = symbols;
        }

        public string Name
        {
            get
            {
                var builder = new StringBuilder();

                builder.Append("{");

                bool first = true;
                foreach (var alt in Alternatives)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        builder.Append("|");
                    }

                    builder.Append(alt.Name);
                }

                builder.Append("}");

                return builder.ToString();

            }
        }

        public Symbol[] Alternatives { get; private set; }

        public override string ToString() { return Name; }
    }
}
