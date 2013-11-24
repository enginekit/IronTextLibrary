﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronText.Framework.Collections
{
    public interface IOwner<T>
    {
        void Own(T item);

        void Unown(T item);
    }
}