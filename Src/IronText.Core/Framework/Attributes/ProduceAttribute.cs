﻿using System;
using System.Linq;
using System.Reflection;
using IronText.Reflection;
using IronText.Reflection.Managed;

namespace IronText.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
    public class ProduceAttribute : RuleMethodAttribute
    {
        public ProduceAttribute(int precedence, params string[] keywordMask) 
            : this(keywordMask)
        {
            this.Precedence = precedence;
        }

        public ProduceAttribute(Associativity assoc, params string[] keywordMask) 
            : this(keywordMask)
        {
            this.Associativity = assoc;
        }

        public ProduceAttribute(int precedence, Associativity assoc, params string[] keywordMask) 
            : this(keywordMask)
        {
            this.Precedence = precedence;
            this.Associativity = assoc;
        }

        public ProduceAttribute(params string[] keywordMask) 
        {
            this.KeywordMask = keywordMask ?? new string[0];
        }

        public string[] KeywordMask { get; set; }

        protected override CilSymbolRef[] DoGetRuleMask(MethodInfo methodInfo)
        {
            int placeholderCount = KeywordMask.Count(item => item == null);
            int nonPlaceholderParameterCount = methodInfo.GetParameters().Length - placeholderCount;
            if (nonPlaceholderParameterCount < 0)
            {
                throw new InvalidOperationException("Insufficient rule-method arguments in " + this);
            }

            return KeywordMask
                .Select(item => item == null ? null : CilSymbolRef.Create(item))
                .ToArray();
        }
    }
}
