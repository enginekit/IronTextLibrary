﻿using IronText.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IronText.Runtime
{
    using State = System.Int32;

    sealed class Gss<T>
        : IGraphStructuredStack<T>
        , IUndoable
    {
        private int currentLayer = 0;
        private byte currentStage = 0;
        private MutableArray<GssNode<T>> front;
        private readonly CircularStack<GssNode<T>> history;

        public Gss(int stateCount)
        {
            front = new MutableArray<GssNode<T>>(stateCount);
            history = new CircularStack<GssNode<T>>(2 * stateCount + 2);
            AddTopmost(0);
        }

        private Gss(int currentLayer, MutableArray<GssNode<T>> front)
        {
            this.currentLayer = currentLayer;
            this.front = front;
        }

        public bool HasLayers => currentLayer != 0;

        public ImmutableArray<GssNode<T>> Front => front;

        public bool IsFrontEmpty => front.Count == 0;

        public void PushLayer()
        {
            currentStage = 0;
            front.Clear();
            ++currentLayer;
        }

        public void PopLayer()
        {
            var visited = front;
            this.front = new MutableArray<GssNode<T>>(front.Capacity);

            --currentLayer;

            foreach (var node in visited)
            {
                if (node.Layer == currentLayer)
                {
                    front.Add(node);
                }

                if (node.Layer >= currentLayer)
                {
                    var link = node.FirstLink;
                    while (link != null)
                    {
                        if (!visited.Contains(link.LeftNode))
                        {
                            visited.Add(link.LeftNode);
                        }

                        link = link.NextLink;
                    }
                }
            }

            // Following line removes nodes created by the lookahead triggered 
            // reductions.
            //
            // This line optional since PopLayer() is used in the panic-mode 
            // error recovery only and such nodes should not cause incorrect
            // parsing recovery sequences (TODO: prove!). However removing 
            // these nodes should simplify debugging and can slightly improve
            // performance.
            front.RemoveAll(node => node.Stage != 0);
        }

        public GssNode<T> GetFrontNode(State state, int lookahead)
        {
            foreach (var node in front)
            {
                if (node.State == state && (lookahead < 0 || node.Lookahead < 0 || lookahead == node.Lookahead))
                {
                    return node;
                }
            }

            return default(GssNode<T>);
        }

        private GssNode<T> AddTopmost(State state, int lookahead = -1)
        {
            var result = new GssNode<T>(state, currentLayer, currentStage, lookahead);
            front.Add(result);
            return result;
        }

        public GssLink<T> Push(
            GssNode<T> leftNode,
            int rightState,
            T label,
            int lookahead = -1,
            ValueMergeDelegate<T> merge = null)
        {
            GssNode<T> rightmostNode = GetFrontNode(rightState, lookahead)
                                     ?? AddTopmost(rightState, lookahead);

            var link = GetLink(rightmostNode, leftNode);
            if (link != null)
            {
                if (merge == null)
                {
                    throw new InvalidOperationException("Merger is not provided.");
                }

                var value = merge(link.Label, label);
                link.AssignLabel(value);
                return null;
            }

            var result = rightmostNode.AddLink(leftNode, label);
            if (result.NextLink != null)
            {
                rightmostNode.DeterministicDepth = 0;
                UpdateDeterministicDepths();
            }
            else
            {
                rightmostNode.DeterministicDepth = leftNode.DeterministicDepth + 1;
            }

            return result;
        }

        private void UpdateDeterministicDepths()
        {
            int changes;
            do
            {
                changes = 0;
                // Note: Just added link can affect deterministic 
                //       depth only of the topmost state nodes.
                foreach (var topNode in front)
                {
                    int newDepth = topNode.ComputeDeterministicDepth();
                    if (newDepth != topNode.DeterministicDepth)
                    {
                        topNode.DeterministicDepth = newDepth;
                        ++changes;
                    }
                }
            }
            while (changes != 0);
        }

        private static GssLink<T> GetLink(GssNode<T> fromNode, GssNode<T> toNode)
        {
            var link = fromNode.FirstLink;
            while (link != null)
            {
                if (link.LeftNode == toNode)
                {
                    return link;
                }

                link = link.NextLink;
            }

            return default(GssLink<T>);
        }

        
        private static string StateName(GssNode<T> node)
        {
            return string.Format("{0}:{1}", node.State, node.Stage);
        }

        public void WriteGraph(IGraphView view, RuntimeGrammar grammar, int[] stateToSymbol)
        {
            var allAccessibleByLayer = GetAllNodes().GroupBy(state => state.Layer);

            var layers = Enumerable
                            .Range(0, currentLayer + 1)
                            .Select(i => new List<GssNode<T>>(2))
                            .ToList();

            foreach (var group in allAccessibleByLayer)
            {
                layers[group.Key].AddRange(group);
            }

            view.BeginDigraph("Gss");

            view.SetGraphProperties(rankDir: RankDir.RightToLeft);

            for (int layerIndex = 0; layerIndex != layers.Count; ++layerIndex)
            {
                var layer = layers[layerIndex];
                view.BeginCluster(layerIndex.ToString());

                view.SetGraphProperties(style: Style.Dotted, label: "U" + layerIndex);
                view.SetNodeProperties(shape: Shape.Circle);

                for (int nodeIndex = 0; nodeIndex != layer.Count; ++nodeIndex)
                {
                    view.AddNode(
                        Tuple.Create("s", layerIndex, nodeIndex),
                        label: StateName(layer[nodeIndex]));
                }

                view.EndCluster();
            }

            view.SetNodeProperties(shape: Shape.Rect);

            int linkIndex = 0;
            for (int layerIndex = 0; layerIndex != layers.Count; ++layerIndex)
            {
                var layer = layers[layerIndex];
                for (int nodeIndex = 0; nodeIndex != layer.Count; ++nodeIndex)
                {
                    var from = layer[nodeIndex];
                    if (from.State < 0)
                    {
                        continue;
                    }

                    int token = stateToSymbol[from.State];
                    var link = from.FirstLink;
                    while (link != null)
                    {
                        var to = link.LeftNode;

                        view.AddNode(Tuple.Create("t", linkIndex), grammar.SymbolName(token));

                        view.AddEdge(
                            Tuple.Create("s", layerIndex, nodeIndex),
                            Tuple.Create("t", linkIndex)
                            );

                        view.AddEdge(
                            Tuple.Create("t", linkIndex),
                            Tuple.Create("s", to.Layer, layers[to.Layer].IndexOf(to))
                            );

                        ++linkIndex;

                        link = link.NextLink;
                    }
                }
            }

            view.EndDigraph();
        }

        // Note: peformance non-critical
        private List<GssNode<T>> GetAllNodes()
        {
            var result = new List<GssNode<T>>(front);

            for (int i = 0; i != result.Count; ++i)
            {
                var item = result[i];
                var f = item.Links.Select(l => l.LeftNode).Except(result);
                result.AddRange(f);
            }

            return result;
        }

        public void BeginEdit()
        {
            currentStage = 1;

            // Save front to history before editing
            if (history != null)
            {
                Debug.Assert(front.Count != 0);

                history.Push(null); // sentinel

                foreach (var node in front)
                {
                    history.Push(node);
                }
            }
        }

        public void EndEdit()
        {
        }

        public void Undo(int inputCount)
        {
            Debug.Assert(history != null);

            GssNode<T> node;
            int i = inputCount;
            while (i-- != 0)
            {
                while ((node = history.Pop()) != null)
                {
                }
            }

            front.Clear();
            while ((node = history.Pop()) != null)
            {
                front.Add(node);
            }

            this.currentLayer -= inputCount;
            this.currentStage = 0;

            history.Push(null);

            UpdateDeterministicDepths();
        }

        public Gss<T> CloneWithoutData()
        {
            return new Gss<T>(currentLayer, front.Clone());
        }
    }
}
