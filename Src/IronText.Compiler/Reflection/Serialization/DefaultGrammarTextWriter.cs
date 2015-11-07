﻿using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using IronText.Misc;

namespace IronText.Reflection
{
    class DefaultTextGrammarWriter : IGrammarTextWriter
    {
        private const string Indent = "    ";

        public void Write(Grammar grammar, TextWriter output)
        {
            WriteIndented(grammar, new IndentedTextWriter(output, Indent));
        }

        private void WriteIndented(Grammar grammar, IndentedTextWriter output)
        {
            WriteSummaryComment(grammar, output);
            WriteProductions(grammar, output);
            WriteMatchers(grammar, output);
        }

        private static void WriteSummaryComment(Grammar grammar, IndentedTextWriter output)
        {
            output.WriteLine("/*");
            ++output.Indent;
            WriteSummary(output, "symbols       : {0}", grammar.Symbols.PublicCount);
            WriteSummary(output, "terminals     : {0}", grammar.Symbols.Where(s => s.IsTerminal).Count());
            WriteSummary(output, "non-terminals : {0}", grammar.Symbols.Where(s => !s.IsTerminal).Count());
            WriteSummary(output, "productions(+): {0}", grammar.Productions.PublicCount);
            WriteSummary(output, "productions(-): {0}", grammar.Productions.Hidden.Count());
            WriteSummary(output, "mergers       : {0}", grammar.Mergers.PublicCount);
            WriteSummary(output, "matchers      : {0}", grammar.Matchers.PublicCount);
            --output.Indent;
            output.WriteLine("*/");
            output.WriteLine();
        }

        private static void WriteSummary(IndentedTextWriter output, string title, int count)
        {
            if (count != 0)
            {
                output.WriteLine(title, count);
            }
        }

        private static void WriteProductions(Grammar grammar, IndentedTextWriter output)
        {
//            output.WriteLine("// Production rules:");
//            output.WriteLine();

            foreach (var prod in grammar.Productions.All)
            {
//                output.WriteLine("// {0}:", prod.Index);

                if (prod.ExplicitPrecedence != null)
                {
                    if (prod.IsSoftRemoved)
                    {
                        output.Write("// *deleted*:  ");
                    }

                    output.WriteLine(
                        "[Precedence({0},{1})]",
                        prod.EffectivePrecedence.Value,
                        Enum.GetName(typeof(Associativity), prod.EffectivePrecedence.Assoc));
                }

                if (prod.IsSoftRemoved)
                {
                    output.Write("// *hidden*:  ");
                }

                InternalWriteComponent(output, prod);

                output.WriteLine(";");
                output.WriteLine();
            }
        }

        private static void InternalWriteComponent(
            IndentedTextWriter   output,
            IProductionComponent component,
            bool                 showParenthes = false)
        {
            Symbol     symbol;
            Production prod;

            switch (component.Match(out symbol, out prod))
            {
                case 0:
                    {
                        output.Write(symbol.Name);
                        break;
                    }
                case 1:
                    {
                        if (showParenthes)
                        {
                            output.Write('(');
                        }

                        output.Write("{0} :", prod.Outcome.Name);
                        if (prod.ChildComponents.Length == 0)
                        {
                            output.Write(" /*empty*/");
                        }
                        else
                        {
                            foreach (var child in prod.ChildComponents)
                            {
                                output.Write(' ');
                                InternalWriteComponent(output, child, true);
                            }
                        }

                        if (showParenthes)
                        {
                            output.Write(')');
                        }

                        break;
                    }
            }
        }

        private void WriteMatchers(Grammar grammar, IndentedTextWriter output)
        {
            output.WriteLine("scanner");
            output.WriteLine("{");
            ++output.Indent;

            foreach (var matcher in grammar.Matchers)
            {
                output.WriteLine(
                    "{0}: /{1}/;",
                    Name(matcher.Outcome),
                    matcher.Pattern);
            }

            --output.Indent;
            output.WriteLine("}");
            output.WriteLine();
        }

        private static string Name(ITerminal symbol)
        {
            return symbol == null ? "$skip" : symbol.Name; 
        }
    }
}
