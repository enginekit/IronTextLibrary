﻿using System;
using IronText.Collections;

namespace IronText.Reflection
{
    public class ForeignContextRef 
        : IndexableObject<ISharedGrammarEntities>
        , IEquatable<ForeignContextRef>
    {
        public static readonly ForeignContextRef None = new ForeignContextRef("$none");

        public ForeignContextRef(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            this.UniqueName = name;
            this.Joint      = new Joint();
        }

        public string UniqueName  { get; private set; }

        public Joint  Joint { get; private set; }

        public override bool Equals(object obj)
        {
            var casted = obj as ForeignContextRef;
            return Equals(casted);
        }

        public bool Equals(ForeignContextRef other)
        {
            return other != null && UniqueName == other.UniqueName;
        }

        public override int GetHashCode()
        {
            return UniqueName.GetHashCode();
        }

        public override string ToString() { return UniqueName; }
    }
}
