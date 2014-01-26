﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IronText.Extensibility;

namespace IronText.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public class MergeAttribute : LanguageMetadataAttribute
    {
        private MethodInfo Method { get { return (MethodInfo)Member; } }

        public override IEnumerable<CilMerger> GetMergers(IEnumerable<CilSymbolRef> leftSides)
        {
            var returnToken = CilSymbolRef.Typed(Method.ReturnType);
            if (leftSides.Contains(returnToken))
            {
                var type = Method.ReturnType;
                var method = Method;

                yield return new CilMerger
                {
                    Symbol = returnToken,
                    ActionBuilder = code =>
                        {
                            if (!Method.IsStatic)
                            {
                                code.ContextResolver.LdContextType(method.DeclaringType);
                            }

                            code = code.LdOldValue();
                            if (type.IsValueType)
                            {
                                code = code.Emit(il => il.Unbox_Any(il.Types.Import(type)));
                            }
                                
                            code = code.LdNewValue();
                            if (type.IsValueType)
                            {
                                code = code.Emit(il => il.Unbox_Any(il.Types.Import(type)));
                            }

                            code = code
                                .Emit(il => il.Call(il.Methods.Import(method)));

                            if (type.IsValueType)
                            {
                                code = code.Emit(il => il.Box(il.Types.Import(type)));
                            }
                        }
                };
            }
        }
    }
}
