﻿using IronText.Reflection;

namespace IronText.Extensibility
{
    internal class LocalContextBinding
    {
        /// <summary>
        /// ID of the parent state
        /// </summary>
        public int StackState    { get; set; }

        /// <summary>
        /// Tail relative position of the context token in stack
        /// </summary>
        public int StackLookback { get; set; }

        public ForeignContextProvider Provider      { get; set; }

        public ForeignContextRef      Consumer      { get; set; }
    }
}
