﻿using System;
using System.Reflection;
using IronText.Automata.Regular;
using IronText.Build;
using IronText.Framework;
using IronText.Lib.IL;
using IronText.Lib.IL.Generators;
using IronText.Lib.Shared;
using IronText.Logging;
using IronText.Misc;
using IronText.Reflection;
using IronText.Reflection.Managed;
using IronText.Runtime;

namespace IronText.MetadataCompiler
{
    public class LanguageDerivedBuilder : IDerivedBuilder<CilDocumentSyntax>
    {
        private const string CreateGrammarMethodName            = "CreateGrammar";
        private const string CreateTokenKeyToIdMethodName       = "CreateTokenKeyToId";
        private const string RuleActionMethodName               = "RuleAction";
        private const string MergeActionMethodName              = "MergeAction";
        private const string TermFactoryMethodName              = "TermFactory";
        private const string GetParserActionMethodName          = "GetParserAction";
        private const string CreateStateToSymbolMethodName      = "CreateStateToSymbol";
        private const string CreateParserActionConflictsMethodName = "CreateParserActionConflicts";
        private const string CreateTokenComplexityTableMethodName = "CreateTokenComplexity";
        private const string CreateDefaultContextMethodName     = "InternalCreateDefaultContext";

        private readonly LanguageData data;
        private readonly CilGrammarSource languageName;
        private Ref<Types> declaringTypeRef;
        private readonly ImplementationGenerator implementationGenerator;
        private ILogging logging;

        public LanguageDerivedBuilder(Type definitionType)
        {
            this.languageName = new CilGrammarSource(definitionType);

            var dataProvider = new LanguageDataProvider(languageName, false);
            ResourceContext.Instance.LoadOrBuild(dataProvider, out this.data);

            // TODO: Share abstraction impelementation between languages i.e. shared plan
            this.implementationGenerator = new ImplementationGenerator(
                                    null,
                                    IsMethodWithNonNullResult);
        }

        public CilDocumentSyntax Build(ILogging logging, CilDocumentSyntax context)
        {
            if (data == null)
            {
                logging.Write(
                    new LogEntry
                    {
                        Severity = Severity.Error,
                        Message = string.Format(
                            "Failed to compile '{0}' language definition.",
                            languageName.FullLanguageName),
                        Origin = languageName.Origin
                    });

                return null;
            }

            this.logging = logging;

            this.declaringTypeRef = context.Types.Class_(
                            ClassName.Parse(languageName.LanguageTypeName));

            logging.Write(
                new LogEntry
                {
                    Severity = Severity.Verbose,
                    Message = string.Format("Started Compiling Derived assembly for {0}", languageName.FullLanguageName)
                });

            var result = context
                .Class_()
                        .Public
                        .Named(languageName.LanguageTypeName)
                        .Extends(context.Types.Import(typeof(LanguageBase)))
                    .Do(BuildMethod_CreateGrammar)
                    .Do(BuildMethod_GetParserAction)
                    .Do(BuildMethod_CreateTokenIdentities)
                    .Do(BuildMethod_Scan1)
                    .Do(BuildMethod_TermFactory)
                    .Do(BuildMethod_GrammarAction)
                    .Do(BuildMethod_MergeAction)
                    .Do(BuildMethod_CreateStateToSymbol)
                    .Do(BuildMethod_CreateParserActionConflicts)
                    .Do(BuildMethod_CreateTokenComplexityTable)
                    .Do(BuildMethod_CreateDefaultContext)
                    .Do(Build_Ctor)
                .EndClass()
                ;

            logging.Write(
                new LogEntry
                {
                    Severity = Severity.Verbose,
                    Message = string.Format("Done Compiling Derived assembly for {0}", languageName.FullLanguageName)
                });

            implementationGenerator.Generate(context);
            return result;
        }

        private ClassSyntax BuildMethod_GrammarAction(ClassSyntax context)
        {
            var generator = new ProductionActionGenerator();
            return generator.BuildMethod(context, RuleActionMethodName, data);
        }

        private ClassSyntax BuildMethod_MergeAction(ClassSyntax context)
        {
            var generator = new MergeActionGenerator();
            generator.BuildMethod(context, MergeActionMethodName, data);
            return context;
        }

        private ClassSyntax BuildMethod_Scan1(ClassSyntax context)
        {
            logging.Write(
                new LogEntry
                {
                    Severity = Severity.Verbose,
                    Message = string.Format("Started compiling Scan1 modes for {0} language", languageName.LanguageName)
                });

            foreach (var condition in data.Grammar.Conditions)
            {
                ITdfaData dfa = condition.Joint.The<ITdfaData>();
                var dfaSerialization = new DfaSerialization(dfa);
                var generator = new ScannerGenerator(dfaSerialization);

                var methodName = ConditionMethods.GetMethodName(condition.Index);
                var args = context
                            .Method()
                                .Static
                                .Returning(context.Types.Int32)
                                .Named(methodName)
                                    .BeginArgs();

                var emit = args
                        .Argument(
                            context.Types.Import(typeof(ScanCursor)),
                            args.Args.Generate("cursor"))      // input
                    .EndArgs()
                        .NoInlining
                        .NoOptimization
                    .BeginBody();

                generator.Build(emit);

                context = emit.EndBody();
            }

            logging.Write(
                new LogEntry
                {
                    Severity = Severity.Verbose,
                    Message = string.Format("Done compiling Scan1 modes for {0} language", languageName.LanguageName)
                });
            return context;
        }

        private ClassSyntax BuildMethod_TermFactory(ClassSyntax context)
        {
            var args = context
                        .Method()
                            .Static
                            .Returning(context.Types.Int32)
                            .Named(TermFactoryMethodName)
                            .BeginArgs();

            var cursorArg = args.Args.Generate("cursor");
            var tokenArg  = args.Args.Generate("token");

            var emit = args
                    .Argument(context.Types.Import(typeof(ScanCursor)), cursorArg)       // ruleId
                    .Out.Argument(context.Types.Import(typeof(object).MakeByRefType()), tokenArg)
                .EndArgs()
                .BeginBody();

            var generator = new TermFactoryGenerator(data, declaringTypeRef);
            generator.Build(
                emit,
                il => il.Ldarg(cursorArg.GetRef()),
                il => il.Ldarg(tokenArg.GetRef()));

            return emit.EndBody();
        }

        private ClassSyntax BuildMethod_CreateTokenIdentities(ClassSyntax context)
        {
            var generator = new TokenIdentitiesSerializer(data.Grammar);

            return context
                .Method().Private.Static
                        .Returning(context.Types.Import(LanguageBase.Fields.tokenKeyToId.FieldType))
                        .Named(CreateTokenKeyToIdMethodName)
                    .BeginArgs()
                    .EndArgs()
                .BeginBody()
                    .Do(generator.Build)
                    .Ret()
                .EndBody();
        }

        private ClassSyntax BuildMethod_GetParserAction(ClassSyntax context)
        {
            var generator = new ReadOnlyTableGenerator(
                                    data.ParserActionTable,
                                    il => il.Ldarg(0),
                                    il => il.Ldarg(1));

            var args = context.Method()
                              .Private.Static
                              .Returning(context.Types.Int32)
                              .Named(GetParserActionMethodName)
                              .BeginArgs();

            return args
                        .Argument(context.Types.Int32, args.Args.Generate())
                        .Argument(context.Types.Int32, args.Args.Generate())
                    .EndArgs()

                    .NoOptimization

                .BeginBody()
                    .Do(generator.Build)
                    .Ret()
                .EndBody();
        }

        private ClassSyntax BuildMethod_CreateDefaultContext(ClassSyntax context)
        {
            return context
                .Method()
                      .Private.Static
                      .Returning(context.Types.Object)
                      .Named(CreateDefaultContextMethodName)
                      .BeginArgs()
                      .EndArgs()
                .BeginBody()
                    // Plan implementation of abstraction as needed
                    .Do(il=> implementationGenerator
                                .EmitFactoryCode(il, languageName.DefinitionType))
                    .Ret()
                .EndBody();
        }

        private static bool IsMethodWithNonNullResult(MethodInfo method)
        {
            if (method.Name.StartsWith("get_"))
            {
                var prop = method.DeclaringType.GetProperty(method.Name.Substring(4));
                if (prop != null && Attributes.Exists<SubContextAttribute>(prop))
                {
                    return true;
                }
            }

            return Attributes.Exists<DemandAttribute>(method.ReturnType);
        }

        private ClassSyntax BuildMethod_CreateGrammar(ClassSyntax context)
        {
            var grammarSerializer = new GrammarSerializer(data.Grammar);

            return context
                .Method()   
                    .Private.Static
                    .Returning(context.Types.Import(typeof(Grammar)))
                    .Named(CreateGrammarMethodName)
                        .BeginArgs()
                        .EndArgs()
                .BeginBody()
                    .Do(grammarSerializer.Build)
                    .Ret()
                .EndBody();
        }

        private ClassSyntax BuildMethod_CreateParserActionConflicts(ClassSyntax context)
        {
            var emit = context
                        .Method()
                            .Private.Static
                            .Returning(context.Types.Import(typeof(int[])))
                            .Named(CreateParserActionConflictsMethodName)
                                .BeginArgs()
                                .EndArgs()
                        .BeginBody();
            
            var resultLoc = emit.Locals.Generate().GetRef();
            var itemLoc = emit.Locals.Generate().GetRef();
            var conflicts = data.ParserConflictActionTable;

            emit = emit
                .Local(resultLoc.Def, emit.Types.Import(typeof(int[])))
                .Ldc_I4(conflicts.Length)
                .Newarr(emit.Types.Import(typeof(int)))
                .Stloc(resultLoc)
                ;

            for (int i = 0; i != conflicts.Length; ++i)
            {
                emit
                    .Ldloc(resultLoc)
                    .Ldc_I4(i)
                    .Ldc_I4(conflicts[i])
                    .Stelem_I4();
            }

            return emit
                    .Ldloc(resultLoc)
                    .Ret()
                .EndBody();
        }

        private ClassSyntax BuildMethod_CreateTokenComplexityTable(ClassSyntax context)
        {
            var emit = context
                        .Method()
                            .Private.Static
                            .Returning(context.Types.Import(typeof(int[])))
                            .Named(CreateTokenComplexityTableMethodName)
                                .BeginArgs()
                                .EndArgs()
                        .BeginBody();
            
            var resultLoc = emit.Locals.Generate().GetRef();
            var itemLoc   = emit.Locals.Generate().GetRef();
            var table     = data.TokenComplexity;

            emit = emit
                .Local(resultLoc.Def, emit.Types.Import(typeof(int[])))
                .Ldc_I4(table.Length)
                .Newarr(emit.Types.Import(typeof(int)))
                .Stloc(resultLoc)
                ;

            for (int i = 0; i != table.Length; ++i)
            {
                emit
                    .Ldloc(resultLoc)
                    .Ldc_I4(i)
                    .Ldc_I4(table[i])
                    .Stelem_I4();
            }

            return emit
                    .Ldloc(resultLoc)
                    .Ret()
                .EndBody();
        }

        private ClassSyntax BuildMethod_CreateStateToSymbol(ClassSyntax context)
        {
            var emit = context
                        .Method()
                            .Private.Static
                            .Returning(context.Types.Import(typeof(int[])))
                            .Named(CreateStateToSymbolMethodName)
                                .BeginArgs()
                                .EndArgs()
                        .BeginBody();
            var resultLoc = emit.Locals.Generate().GetRef();
            var stateToSymbol = data.StateToToken;

            emit = emit
                .Local(resultLoc.Def, context.Types.Import(typeof(int[])))
                .Ldc_I4(stateToSymbol.Length)
                .Newarr(context.Types.Import(typeof(int)))
                .Stloc(resultLoc);

            for (int i = 0; i != stateToSymbol.Length; ++i)
            {
                emit = emit
                    .Ldloc(resultLoc)
                    .Ldc_I4(i)
                    .Ldc_I4(stateToSymbol[i])
                    .Stelem_I4();
            }
                
            return emit
                    .Ldloc(resultLoc)
                    .Ret()
                .EndBody();
        }

        private ClassSyntax Build_Ctor(ClassSyntax context)
        {
            var emit = context
                        .Method()
                            .Public.Instance
                            .Returning(context.Types.Void)
                            .Named(".ctor")
                                .BeginArgs()
                                .Do(args =>
                                    {
                                        var type = context.Types.Import(typeof(CilGrammarSource));
                                        var arg = args.Args.Generate();
                                        args.Argument(type, arg);
                                        return args;
                                    })
                                .EndArgs()

                            .BeginBody();

            // Call base constructor:
            // this:
            emit = emit
                .Ldarg(0) // this
                .Ldarg(1) // LanguageName
                .Call(emit.Methods.Import(typeof(LanguageBase).GetConstructor(new[] { typeof(CilGrammarSource) })))
                ;

            emit
                .Ldarg(0)
                .Ldc_I4(data.IsDeterministic ? 1 : 0)
                .Stfld(LanguageBase.Fields.isDeterministic);

            return emit
                // Init grammar
                .Ldarg(0)
                .Call(emit.Methods.Method(
                    _=>_
                        .StartSignature
                        .Returning(emit.Types.Import(typeof(Grammar)))
                        .DecaringType(declaringTypeRef)
                        .Named(CreateGrammarMethodName)
                        .BeginArgs()
                        .EndArgs()
                    ))
                .Stfld(LanguageBase.Fields.grammar)

                // Init state->token table
                .Ldarg(0)
                .Call(emit.Methods.Method(
                    _=>_
                        .StartSignature
                        .Returning(emit.Types.Import(typeof(int[])))
                        .DecaringType(declaringTypeRef)
                        .Named(CreateStateToSymbolMethodName)
                        .BeginArgs()
                        .EndArgs()
                    ))
                .Stfld(LanguageBase.Fields.stateToSymbol)

                // Init parser action conflicts table
                .Ldarg(0)
                .Call(emit.Methods.Method(
                    _=>_
                        .StartSignature
                        .Returning(emit.Types.Import(typeof(int[])))
                        .DecaringType(declaringTypeRef)
                        .Named(CreateParserActionConflictsMethodName)
                        .BeginArgs()
                        .EndArgs()
                    ))
                .Stfld(LanguageBase.Fields.parserConflictActions)

                // Init token complexity table
                .Ldarg(0)
                .Call(emit.Methods.Method(
                    _=>_
                        .StartSignature
                        .Returning(emit.Types.Import(typeof(int[])))
                        .DecaringType(declaringTypeRef)
                        .Named(CreateTokenComplexityTableMethodName)
                        .BeginArgs()
                        .EndArgs()
                    ))
                .Stfld(LanguageBase.Fields.tokenComplexity)

                // Init grammarAction field
                .Ldarg(0)
                .LdMethodDelegate(
                    declaringTypeRef,
                    RuleActionMethodName,
                    typeof(ProductionActionDelegate))
                .Stfld(LanguageBase.Fields.grammarAction)

                // Init grammarAction field
                .Ldarg(0)
                .LdMethodDelegate(
                    declaringTypeRef,
                    MergeActionMethodName,
                    typeof(MergeDelegate))
                .Stfld(LanguageBase.Fields.merge)


                // Init tokenIdentities
                .Ldarg(0)
                .Call(emit
                        .Methods.Method(
                            _=>_ 
                            .StartSignature
                            .Returning(emit.Types.Import(LanguageBase.Fields.tokenKeyToId.FieldType))
                            .DecaringType(declaringTypeRef)
                            .Named(CreateTokenKeyToIdMethodName)
                            .BeginArgs()
                            .EndArgs() ))
                .Stfld(LanguageBase.Fields.tokenKeyToId)


                // Init scan field
                .Ldarg(0)
                .LdMethodDelegate(
                    declaringTypeRef,
                    ConditionMethods.GetMethodName(0),
                    typeof(Scan1Delegate))
                .Stfld(LanguageBase.Fields.scan1)

                // Init termFactory field
                .Ldarg(0)
                .LdMethodDelegate(
                    declaringTypeRef,
                    TermFactoryMethodName,
                    typeof(ScanActionDelegate))
                .Stfld(LanguageBase.Fields.termFactory)

                // Init getParserAction field
                .Ldarg(0)
                .LdMethodDelegate(
                    declaringTypeRef,
                    GetParserActionMethodName,
                    typeof(TransitionDelegate))
                .Stfld(LanguageBase.Fields.getParserAction)

                // Init defaul context factory
                .Ldarg(0)
                .LdMethodDelegate(
                    declaringTypeRef,
                    CreateDefaultContextMethodName,
                    typeof(Func<object>))
                .Stfld(LanguageBase.Fields.createDefaultContext)

                .Ret()
            .EndBody();
        }
    }
}
