﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronText.Reflection.Managed
{
    public abstract class CilContextRef
    {
        public static string GetName(Type type)
        {
            return type.AssemblyQualifiedName;
        }
        
        public static readonly CilContextRef None = new NoneContextRef();

        public static CilContextRef ByType(Type type)    { return new ByTypeContextRef(type); }

        public abstract string UniqueName { get; }

        public abstract IActionCode Load(IActionCode code);

        sealed class NoneContextRef : CilContextRef
        {
            public override string UniqueName { get { return "$none"; } }

            public override IActionCode Load(IActionCode code) { return code; }

            public override bool Equals(object obj)
            {
                return obj is NoneContextRef;
            }

            public override int GetHashCode() { return 0; }
        }

        sealed class ByTypeContextRef : CilContextRef
        {
            private readonly Type type;

            public ByTypeContextRef(Type type) { this.type = type; }

            public override string UniqueName { get { return GetName(type); } }

            public override IActionCode Load(IActionCode code)
            {
                return code.LdContext(UniqueName);
            }

            public override bool Equals(object obj)
            {
                var casted = obj as ByTypeContextRef;
                return casted != null && type == casted.type;
            }

            public override int GetHashCode()
            {
                return UniqueName.GetHashCode();
            }
        }
    }
}
