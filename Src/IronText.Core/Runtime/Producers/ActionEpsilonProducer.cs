﻿using System.Diagnostics;
using System.Linq;
using IronText.Framework.Reflection;

namespace IronText.Framework
{
    class ActionEpsilonProducer
    {
        private readonly IRuntimeBnfGrammar grammar;
        private readonly GrammarActionDelegate grammarAction;
        private readonly object context;

        public ActionEpsilonProducer(EbnfGrammar grammar, object context, GrammarActionDelegate grammarAction)
        {
            this.grammar = grammar;
            this.context = context;
            this.grammarAction = grammarAction;
        }

        public Msg GetDefault(int nonTerm, IStackLookback<Msg> stackLookback)
        {
            Msg result = InternalGetNullable(nonTerm, stackLookback);
            return result;
        }

        private Msg InternalGetNullable(int nonTerm, IStackLookback<Msg> stackLookback)
        {
            Debug.Assert(grammar.IsNullable(nonTerm));

            var rule = (from r in grammar.GetProductions(nonTerm)
                       where r.Parts.All(grammar.IsNullable)
                       orderby r.Parts.Length ascending
                       select r)
                       .First();

            var args = new Msg[rule.Parts.Length];
            for (int i = 0; i != args.Length; ++i)
            {
                args[i] = InternalGetNullable(rule.Parts[i], stackLookback);
            }

            var value = grammarAction(rule, args, 0, context, stackLookback);
            return new Msg(nonTerm, value, Loc.Unknown);
        }

        public void FillEpsilonSuffix(int ruleId, int prefixSize, Msg[] buffer, int destIndex, IStackLookback<Msg> stackLookback)
        {
            var rule = grammar.Rules[ruleId];
            int i = prefixSize;
            int end = rule.Parts.Length;
            while (i != end)
            {
                buffer[destIndex++] = GetDefault(rule.Parts[i++], stackLookback);
            }
        }
    }
}
