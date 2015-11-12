﻿using System;

namespace IronText.Reflection.Managed
{
    public abstract class CilSemanticRef
    {
        public static readonly CilSemanticRef None = new NoneContextRef();

        public static CilSemanticRef ByType(Type type) { return new ByTypeContextRef(type); }

        public abstract string UniqueName { get; }

        sealed class NoneContextRef : CilSemanticRef
        {
            public override string UniqueName { get { return SharedConstants.NullSemanticRefName; } }

            public override bool Equals(object obj)
            {
                return obj is NoneContextRef;
            }

            public override int GetHashCode() { return 0; }
        }

        sealed class ByTypeContextRef : CilSemanticRef
        {
            private readonly Type type;

            public ByTypeContextRef(Type type) { this.type = type; }

            public override string UniqueName { get { return type.AssemblyQualifiedName; } }

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
