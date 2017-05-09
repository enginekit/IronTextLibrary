﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronText.Runtime
{
    class ReductionQueueWithPriority<T> : PriorityQueue<Reduction<T>>
    {
        public ReductionQueueWithPriority(int[] tokenCompexity)
            : base(new ReductionPriorityComparer<T>(tokenCompexity))
        {
        }
    }

    class ReductionPriorityComparer<T> : IComparer<Reduction<T>>
    {
        private int[] tokenComplexity;

        public ReductionPriorityComparer(int[] tokenComplexity)
        {
            this.tokenComplexity = tokenComplexity;
        }

        public int Compare(Reduction<T> x, Reduction<T> y)
        {
            int xNewerBy = x.LeftmostLayer - y.LeftmostLayer;
            if (xNewerBy != 0)
            {
                return xNewerBy;
            }

            int xLessComplexBy = tokenComplexity[y.Production.Outcome]
                               - tokenComplexity[x.Production.Outcome];
            return xLessComplexBy;
        }
    }

    class PriorityQueue<T>
    {
        private readonly LinkedList<T> paths = new LinkedList<T>();
        private readonly IComparer<T> comparer;

        public PriorityQueue(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        public bool IsEmpty => paths.Count == 0;

        public bool HasEntries => paths.Count != 0;

        public void Clear()
        {
            paths.Clear();
        }

        public void Enqueue(T path)
        {
            var node = paths.First;
            while (node != null)
            {
                if (GoesBefore(path, node.Value))
                {
                    paths.AddBefore(node, path);
                    return;
                }

                node = node.Next;
            }

            paths.AddLast(path);
        }

        public int TryDequeue(ICollection<T> output)
        {
            if (IsEmpty)
            {
                return 0;
            }

            T prior;

            int result = 0;
            do
            {
                prior = paths.First.Value;
                output.Add(prior);
                ++result;
                paths.RemoveFirst();
            }
            while (paths.Count != 0 && 0 == comparer.Compare(paths.First.Value, prior));

            return result;
        }

        private bool GoesBefore(T x, T y)
        {
            return comparer.Compare(x, y) > 0;
        }
    }
}