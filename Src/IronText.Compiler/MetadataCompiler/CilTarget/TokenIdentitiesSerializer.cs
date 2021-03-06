﻿using System;
using System.Collections.Generic;
using IronText.Lib.Ctem;
using IronText.Lib.IL;
using IronText.Reflection;
using IronText.Reflection.Managed;
using IronText.Runtime;

namespace IronText.MetadataCompiler
{
    class TokenIdentitiesSerializer
    {
        private readonly Grammar grammar;

        public TokenIdentitiesSerializer(Grammar grammar)
        {
            this.grammar = grammar;
        }

        public EmitSyntax Build(EmitSyntax emit)
        {
            var result = emit.Locals.Generate().GetRef();

            emit
                .Local(result.Def, emit.Types.Import(LanguageBase.Fields.tokenKeyToId.FieldType))
                .Newobj(() => new Dictionary<object,int>())
                .Stloc(result);

            foreach (var pair in EnumerateTokenKeyToId())
            {
                emit.Ldloc(result);

                if (pair.Item1 is string)
                {
                    emit.Ldstr(new QStr((string)pair.Item1));
                }
                else if (pair.Item1 is Type)
                {
                    emit
                        .Ldtoken(emit.Types.Import((Type)pair.Item1))
                        .Call((RuntimeTypeHandle h) => Type.GetTypeFromHandle(h));
                }
                else
                {
                    throw new InvalidOperationException("Internal error: Unsupported token key type");
                }

                emit
                    .Ldc_I4(pair.Item2)
                    .Call((Dictionary<object, int> self, object _key, int _id) => self.Add(_key, _id))
                    ;
            }

            return emit.Ldloc(result);
        }

        private IEnumerable<Tuple<object,int>> EnumerateTokenKeyToId()
        {
            foreach (var symbolBase in grammar.Symbols)
            {
                var symbol = symbolBase as Symbol;
                if (symbol == null)
                {
                    continue;
                }

                var cilSymbol = symbol.Joint.Get<CilSymbol>();
                if (cilSymbol == null)
                {
                    continue;
                }

                foreach (var literal in cilSymbol.Literals)
                {
                    yield return Tuple.Create((object)literal, cilSymbol.Id);
                }

                if (cilSymbol.Type != null)
                {
                    yield return Tuple.Create((object)cilSymbol.Type, cilSymbol.Id);
                }
            }
        }

        private void LdTid(EmitSyntax emit, CilSymbolRef tid)
        {
            if (tid.Type != null)
            {
                emit
                    .Ldtoken(emit.Types.Import(tid.Type))
                    .Call((RuntimeTypeHandle h) => Type.GetTypeFromHandle(h))
                    ;
            }
            else
            {
                emit.Ldnull();
            }

            if (tid.Literal == null)
            {
                emit.Ldnull();
            }
            else
            {
                emit.Ldstr(new QStr(tid.Literal));
            }

            emit
                .Newobj(() => new CilSymbolRef(default(Type), default(string)));
        }
    }
}
