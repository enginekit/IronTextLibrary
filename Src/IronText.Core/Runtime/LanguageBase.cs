﻿using IronText.Logging;
using IronText.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace IronText.Runtime
{
    public abstract class LanguageBase : ILanguageRuntime, IInternalInitializable, ISourceGrammarProvider
    {
        public static class Fields
        {
            public static readonly FieldInfo targetParserRuntime = ExpressionUtils.GetField((LanguageBase lang) => lang.targetParserRuntime);

            public static readonly FieldInfo grammarBytes = ExpressionUtils.GetField((LanguageBase lang) => lang.grammarBytes);

            public static readonly FieldInfo rtGrammarBytes = ExpressionUtils.GetField((LanguageBase lang) => lang.rtGrammarBytes);

            public static readonly FieldInfo getParserAction = ExpressionUtils.GetField((LanguageBase lang) => lang.getParserAction);

            public static readonly FieldInfo tokenKeyToId = ExpressionUtils.GetField((LanguageBase lang) => lang.tokenKeyToId);

            public static readonly FieldInfo scan1 = ExpressionUtils.GetField((LanguageBase lang) => lang.scan1);

            public static readonly FieldInfo termFactory = ExpressionUtils.GetField((LanguageBase lang) => lang.termFactory);

            public static readonly FieldInfo grammarAction = ExpressionUtils.GetField((LanguageBase lang) => lang.grammarAction);

            public static readonly FieldInfo merge = ExpressionUtils.GetField((LanguageBase lang) => lang.merge);

            public static readonly FieldInfo stateToSymbol = ExpressionUtils.GetField((LanguageBase lang) => lang.stateToSymbol);

            public static readonly FieldInfo tokenComplexity = ExpressionUtils.GetField((LanguageBase lang) => lang.tokenComplexity);

            public static readonly FieldInfo matcherToToken = ExpressionUtils.GetField((LanguageBase lang) => lang.matcherToToken);

            public static readonly FieldInfo createDefaultContext = ExpressionUtils.GetField((LanguageBase lang) => lang.createDefaultContext);
        }

        protected internal ParserRuntime targetParserRuntime;
        protected object sourceGrammar;
        private readonly object sourceGrammarLock = new object();
        protected byte[] grammarBytes;
        protected byte[] rtGrammarBytes;
        protected TransitionDelegate getParserAction;
        protected Dictionary<object, int> tokenKeyToId;
        protected Scan1Delegate scan1;
        protected TermFactoryDelegate termFactory;
        protected ProductionActionDelegate grammarAction;
        protected MergeDelegate merge;
        protected int[] stateToSymbol;
        protected int[] tokenComplexity;
        protected int[] matcherToToken;
        protected Func<object> createDefaultContext;
        private const int maxActionCount = 16;
        private RuntimeGrammar _runtimeGrammar;

        public LanguageBase()
        {
            this.merge = DefaultMerge;
        }

        public RuntimeGrammar RuntimeGrammar { get { return _runtimeGrammar; } }

        public ParserRuntime TargetParserRuntime => targetParserRuntime;

        public void Init()
        {
            this._runtimeGrammar = ByteSerialization.DeserializeBytes<RuntimeGrammar>(rtGrammarBytes);
        }

        public object CreateDefaultContext()
        {
            return createDefaultContext();
        }

        public IScanner CreateScanner(object context, TextReader input, string document, ILogging logging)
        {
            if (logging == null)
            {
                logging = ExceptionLogging.Instance;
            }

            if (context != null)
            {
                ServicesInitializer.SetServiceProperties(
                    context.GetType(),
                    context,
                    typeof(ILanguageRuntime),
                    this);

                ServicesInitializer.SetServiceProperties(
                    context.GetType(),
                    context,
                    typeof(ILogging),
                    logging);
            }

            return new Scanner(scan1, input, document, context, maxActionCount, matcherToToken, logging);
        }

        public IPushParser CreateParser<TNode>(IProducer<TNode> producer, ILogging logging)
        {
            switch (targetParserRuntime)
            {
                case ParserRuntime.Deterministic:
                    return new DeterministicParser<TNode>(producer, _runtimeGrammar, getParserAction, logging);
                case ParserRuntime.Glr:
                    return new GlrParser<TNode>(
                        _runtimeGrammar,
                        tokenComplexity,
                        getParserAction,
                        stateToSymbol,
                        producer,
                        logging);
                case ParserRuntime.Generic:
                    return new GenericParser<TNode>(
                        producer,
                        _runtimeGrammar,
                        getParserAction,
                        logging);
                default:
                    throw new InvalidOperationException(
                        $"Unsupported parser runtime: {targetParserRuntime}");
            }
        }

        public IProducer<ActionNode> CreateProducer(object context)
        {
            var result = new ActionProducer(
                            _runtimeGrammar,
                            context,
                            grammarAction,
                            termFactory,
                            null,
                            this.merge,
                            new Dictionary<int, object>());

            return result;
        }

        public int Identify(Type tokenType)
        {
            int result;
            if (!tokenKeyToId.TryGetValue(tokenType, out result))
            {
                result = -1;
            }

            return result;
        }

        public int Identify(string literal)
        {
            int result;
            if (!tokenKeyToId.TryGetValue(literal, out result))
            {
                result = -1;
            }

            return result;
        }

        private object DefaultMerge(
            int token,
            object alt1,
            object alt2,
            object context,
            IStackLookback<ActionNode> stackLookback)
        {
            var result = alt2;
#if true
            Debug.WriteLine("------------------------------");
            Debug.WriteLine(
                "Default merging of token {0} values in state {1}:",
                (object)_runtimeGrammar.SymbolName(token),
                stackLookback.GetState(1));
            Debug.WriteLine("  '{0}'", alt1);
            Debug.WriteLine(" and");
            Debug.WriteLine("  '{0}'", alt2);
            Debug.WriteLine(" into the value: '{0}'", (object)result);
#endif
            return result;
        }

        object ISourceGrammarProvider.GetSourceGrammar()
        {
            if (sourceGrammar == null)
            {
                lock (sourceGrammarLock)
                {
                    if (sourceGrammar == null)
                    {
                        this.sourceGrammar = ByteSerialization.DeserializeBytes(grammarBytes);
                    }
                }
            }

            return sourceGrammar;
        }
    }
}
