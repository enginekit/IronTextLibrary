﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IronText.Extensibility;
using IronText.Lib.IL;
using IronText.Lib.Shared;

namespace IronText.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class ScanBaseAttribute : LanguageMetadataAttribute
    {
        protected ScanBaseAttribute() 
        {
        }

        /// <summary>
        /// Raw text
        /// </summary>
        public string LiteralText { get; protected set; }

        /// <summary>
        /// <see cref="ScannerSyntax"/> pattern
        /// </summary>
        public string Pattern { get; protected set; }

        internal string RegexPattern { get; set; }

        public override IEnumerable<IScanRule> GetScanRules(ITokenPool tokenPool)
        {
            var method = (MethodInfo)Member;
            var tokenType = method.ReturnType;

            TokenRef thisToken;
            if (LiteralText == null || method.ReturnType == typeof(void))
            {
                thisToken = tokenPool.GetToken(tokenType);
            }
            else
            {
                thisToken = tokenPool.GetLiteral(LiteralText);
            }

            var nextModeType = GetNextModeType();


            ScanRule scanRule;

            if (method.ReturnType == typeof(void))
            {
                scanRule = new SkipScanRule();
            }
            else
            {
                var singleTokenScanRule = new SingleTokenScanRule();
                singleTokenScanRule.TokenType = tokenType;
                singleTokenScanRule.LiteralText = LiteralText;
                scanRule = singleTokenScanRule;
            }

            scanRule.DefiningMember = method;
            scanRule.Pattern = Pattern;
            scanRule.BootstrapRegexPattern = RegexPattern;
            scanRule.NextModeType = nextModeType;
            scanRule.ActionBuilder =
                context =>
                {
                    if (!method.IsStatic)
                    {
                        context.ContextResolver.LdContextType(method.DeclaringType);
                    }

                    var parameters = method.GetParameters().ToList();
                    ParameterInfo nextModeParameter;
                    if (parameters.Count != 0 && parameters.Last().IsOut)
                    {
                        nextModeParameter = parameters.Last();
                        parameters.RemoveAt(parameters.Count - 1);
                    }
                    else
                    {
                        nextModeParameter = null;
                    }

                    if (parameters.Count == 0)
                    {
                    }
                    else if (parameters.Count == 1
                            && parameters[0].ParameterType == typeof(string))
                    {
                        context.LdTokenString();
                    }
                    else if (parameters.Count == 3
                            && parameters[0].ParameterType == typeof(char[])
                            && parameters[1].ParameterType == typeof(int)
                            && parameters[2].ParameterType == typeof(int))
                    {
                        context
                            .LdBuffer()
                            .LdStartIndex()
                            .LdLength();
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Unsupported scan-method signature: "
                            + string.Join(", ", parameters.Select(p => p.ParameterType.Name)));
                    }

                    Ref<Locals> nextModeVar = null;
                    if (nextModeParameter != null)
                    {
                        context
                            .Emit(il =>
                            {
                                nextModeVar = il.Locals.Generate().GetRef();
                                return il
                                    .Local(nextModeVar.Def, il.Types.Object)
                                    .Ldloca(nextModeVar);
                            });
                    }

                    context.Emit(il => il.Call(method));

                    if (nextModeParameter != null)
                    {
                        context
                            .Emit(il => il.Ldloc(nextModeVar))
                            .ChangeMode(nextModeType);
                    }

                    if (method.ReturnType == typeof(void))
                    {
                        context.SkipAction();
                    }
                    else
                    {
                        if (method.ReturnType.IsValueType)
                        {
                            context.Emit(il => il.Box(il.Types.Import(method.ReturnType)));
                        }

                        context
                            .ReturnFromAction()
                            ;
                    }
                };


            return new[] { scanRule };
        }

        private Type GetNextModeType()
        {
            var method = (MethodInfo)Member;
            var tokenType = method.ReturnType;

            return method
                    .GetParameters()
                    .Where(p => p.IsOut)
                    .Select(p => p.ParameterType.GetElementType())
                    .SingleOrDefault();
        }

        private Type TokenType 
        { 
            get 
            {
                var method = (MethodInfo)Member;
                var result = method.ReturnType;
                if (result == typeof(object))
                {
                    return null;
                }

                return result;
            }
        }

        private TokenRef[] GetProducedTokens()
        {
            List<TokenRef> resultList = new List<TokenRef>();
            if (LiteralText != null)
            {
                resultList.Add(TokenRef.Literal(LiteralText));
            }

            if (TokenType != null)
            {
                resultList.Add(TokenRef.Typed(TokenType));
            }

            return resultList.ToArray();
        }
    }
}
