﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IronText.Logging;
using IronText.Misc;
using IronText.Framework;

namespace IronText.Framework
{
    public abstract class LanguageBase : ILanguage, IInternalInitializable
    {
        public static class Fields
        {
            public static readonly FieldInfo isAmbiguous     = ExpressionUtils.GetField((LanguageBase lang) => lang.isAmbiguous);

            public static readonly FieldInfo grammar         = ExpressionUtils.GetField((LanguageBase lang) => lang.grammar);

            public static readonly FieldInfo getParserAction = ExpressionUtils.GetField((LanguageBase lang) => lang.getParserAction);

            public static readonly FieldInfo tokenKeyToId    = ExpressionUtils.GetField((LanguageBase lang) => lang.tokenKeyToId);

            public static readonly FieldInfo scan1           = ExpressionUtils.GetField((LanguageBase lang) => lang.scan1);

            public static readonly FieldInfo termFactory     = ExpressionUtils.GetField((LanguageBase lang) => lang.scanAction);

            public static readonly FieldInfo grammarAction   = ExpressionUtils.GetField((LanguageBase lang) => lang.grammarAction);

            public static readonly FieldInfo merge           = ExpressionUtils.GetField((LanguageBase lang) => lang.merge);

            public static readonly FieldInfo switchFactory   = ExpressionUtils.GetField((LanguageBase lang) => lang.switchFactory);

            public static readonly FieldInfo name            = ExpressionUtils.GetField((LanguageBase lang) => lang.name);

            public static readonly FieldInfo stateToSymbol   = ExpressionUtils.GetField((LanguageBase lang) => lang.stateToSymbol);

            public static readonly FieldInfo parserConflictActions = ExpressionUtils.GetField((LanguageBase lang) => lang.parserConflictActions);

            public static readonly FieldInfo createDefaultContext = ExpressionUtils.GetField((LanguageBase lang) => lang.createDefaultContext);
        }

        protected internal bool          isAmbiguous;
        protected BnfGrammar             grammar;
        protected TransitionDelegate     getParserAction;
        protected Dictionary<object,int> tokenKeyToId;
        protected Scan1Delegate          scan1;
        protected ScanActionDelegate     scanAction;
        protected GrammarActionDelegate  grammarAction;
        protected MergeDelegate          merge;
        protected SwitchFactory          switchFactory;
        protected LanguageName           name;
        protected int[]                  stateToSymbol;
        protected int[]                  parserConflictActions;
        private ResourceAllocator        allocator;
        protected Func<object>           createDefaultContext;

        public LanguageBase(LanguageName name) 
        { 
            this.name = name;
            this.merge = (int token, object x, object y, object context, IStackLookback<Msg> stackLookback) => y;
        }

        public bool IsAmbiguous { get { return isAmbiguous; } }

        public void Init()
        {
            this.allocator = new ResourceAllocator(grammar);
        }

        public LanguageName Name { get { return name; } }

        public BnfGrammar Grammar { get { return grammar; } }

        private ResourceAllocator Allocator
        {
            get
            {
                if (allocator == null)
                {
                    allocator = new ResourceAllocator(grammar);
                }

                return allocator;
            }
        }
        
        public void Heatup()
        {
            getParserAction(0, 0);
            var cursor = new ScanCursor { Buffer = new char[] { '\0' } };
            scan1(cursor);
            object ignore;
            scanAction(cursor, out ignore);
            grammarAction(grammar.Rules[0], new Msg[0], 0, null, null);
            switchFactory(null, 0, null, this);
            merge(BnfGrammar.AugmentedStart, null, null, null, null);
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
                    typeof(ILanguage),
                    this);

                ServicesInitializer.SetServiceProperties(
                    context.GetType(),
                    context,
                    typeof(ILogging),
                    logging);
            }

            return new Scanner(scan1, input, document, context, scanAction, logging);
        }

        public IPushParser CreateParser<TNode>(IProducer<TNode> producer, ILogging logging)
        {
            if (isAmbiguous)
            {
                return new RnGlrParser<TNode>(
                    grammar,
                    getParserAction,
                    stateToSymbol,
                    parserConflictActions,
                    producer,
                    allocator,
                    logging);
            }
            else
            {
                return new DeterministicParser<TNode>(
                    producer,
                    grammar,
                    getParserAction,
                    allocator
#if SWITCH_FEATURE
                    , (ctx, id, exit) => switchFactory(ctx, id, exit, this)
#endif
                    , logging
                    );
            }
        }

        public IProducer<Msg> CreateActionProducer(object context)
        {
            var result = new ActionProducer(grammar, context, grammarAction, this.merge);

            if (context != null)
            {
                ServicesInitializer.SetServiceProperties(
                    context.GetType(),
                    context,
                    typeof(IParsing),
                    result);
            }

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
    }
}
