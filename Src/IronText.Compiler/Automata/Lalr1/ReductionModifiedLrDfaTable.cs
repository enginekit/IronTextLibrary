﻿using System.Collections.Generic;
using System.Linq;
using IronText.Algorithm;
using IronText.Compiler.Analysis;
using IronText.Reflection;
using IronText.Reflection.Reporting;
using IronText.Runtime;

namespace IronText.Automata.Lalr1
{
    class ReductionModifiedLrDfaTable : ILrParserTable
    {
        private readonly GrammarAnalysis grammar;
        private readonly Dictionary<TransitionKey, ParserConflictInfo> transitionToConflict 
            = new Dictionary<TransitionKey, ParserConflictInfo>();
        private readonly IMutableTable<int> actionTable;
        private int[]  conflictActionTable;
        private readonly bool canOptimizeReduceStates;

        public ReductionModifiedLrDfaTable(ILrDfa dfa, IMutableTable<int> actionTable = null)
        {
            var flag = LrTableOptimizations.EliminateLr0ReduceStates;
            this.canOptimizeReduceStates = (dfa.Optimizations & flag) == flag;

            this.grammar = dfa.GrammarAnalysis;
            var states = dfa.States;
            this.actionTable = actionTable ?? new MutableTable<int>(states.Length, dfa.GrammarAnalysis.Symbols.Count);
            FillDfaTable(states);
            BuildConflictActionTable();
        }

        public bool RequiresGlr { get { return true; } }

        public ParserConflictInfo[] Conflicts
        {
            get { return transitionToConflict.Values.ToArray(); }
        }

        public int[] GetConflictActionTable() { return conflictActionTable; }

        public ITable<int> GetParserActionTable() { return actionTable; }

        private void BuildConflictActionTable()
        {
            var conflictList = new List<int>();
            foreach (var conflict in transitionToConflict.Values)
            {
                var refAction = new ParserAction
                            {
                                Kind   = ParserActionKind.Conflict,
                                Value1 = conflictList.Count,
                                Size   = (short)conflict.Actions.Count
                            };
                             
                actionTable.Set(conflict.State, conflict.Token, ParserAction.Encode(refAction));
                foreach (var action in conflict.Actions)
                {
                    conflictList.Add(ParserAction.Encode(action)); 
                }
            }

            this.conflictActionTable = conflictList.ToArray();
        }

        private void FillDfaTable(DotState[] states)
        {
            for (int i = 0; i != states.Length; ++i)
            {
                var state = states[i];

                foreach (var item in state.Items)
                {
                    if (!item.IsReduce)
                    {
                        int nextToken = item.NextToken;

                        if (canOptimizeReduceStates
                            && item.IsShiftReduce
                            && !state.Transitions.Exists(t => t.Tokens.Contains(nextToken)))
                        {
                            var action = new ParserAction
                            {
                                Kind         = ParserActionKind.ShiftReduce,
                                ProductionId = item.ProductionId,
                                Size         = (short)item.Size
                            };

                            AddAction(i, nextToken, action);
                        }
                        else
                        {
                            var action = new ParserAction
                            {
                                Kind  = ParserActionKind.Shift,
                                State = state.GetNextIndex(nextToken)
                            };

                            AddAction(i, nextToken, action);
                        }
                    }

                    bool isStartRule = item.IsAugmented;

                    if (item.IsReduce || grammar.IsTailNullable(item))
                    {
                        ParserAction action;

                        if (isStartRule)
                        {
                            if (item.Position == 0)
                            {
                                continue;
                            }
                            else
                            {
                                action = new ParserAction { Kind = ParserActionKind.Accept };
                            }
                        }
                        else
                        {
                            action = new ParserAction
                            {
                                Kind         = ParserActionKind.Reduce,
                                ProductionId = item.ProductionId,
                                Size         = (short)item.Position
                            };
                        }

                        foreach (var lookahead in item.LA)
                        {
                            if (!IsValueOnlyEpsilonReduceItem(item, state, lookahead))
                            {
                                AddAction(i, lookahead, action);
                            }
                        }
                    }
                }
            }
        }

        private bool IsValueOnlyEpsilonReduceItem(DotItem item, DotState state, int lookahead)
        {
            if (item.Position != 0 
                || grammar.IsStartProduction(item.ProductionId)
                || !grammar.IsTailNullable(item)
                || !item.LA.Contains(lookahead))
            {
                return false;
            }

            int epsilonToken = item.Outcome;

            foreach (var parentItem in state.Items)
            {
                if (parentItem == item)
                {
                    continue;
                }

                if (parentItem.NextToken == epsilonToken)
                {
                    if (!grammar.IsTailNullable(parentItem))
                    {
                        // there is at least one rule which needs shift on epsilonToken
                        return false;
                    }

                    if (grammar.HasFirst(parentItem.CreateNextItem(), lookahead))
                    {
                        // One of the subseqent non-terms in parentItem can start parsing with current lookahead.
                        // It means that we need tested epsilonToken production for continue parsing on parentItem.
                        return false;
                    }
                }
            }

            return true;
        }

        private void AddAction(int state, int token, ParserAction action)
        {
            int cell = ParserAction.Encode(action);
            int currentCell = actionTable.Get(state, token);
            if (currentCell == 0)
            {
                actionTable.Set(state, token, cell);
            }
            else if (currentCell != cell)
            {
                int resolvedCell;
                if (!TryResolveShiftReduce(currentCell, cell, token, out resolvedCell))
                {
                    ParserConflictInfo conflict;
                    var key = new TransitionKey(state, token);
                    if (!transitionToConflict.TryGetValue(key, out conflict))
                    {
                        conflict = new ParserConflictInfo(state, token);
                        transitionToConflict[key] = conflict;
                        conflict.AddAction(currentCell);
                    }

                    if (!conflict.Actions.Contains(action))
                    {
                        conflict.AddAction(action);
                    }
                }

                actionTable.Set(state, token, resolvedCell);
            }
        }

        private bool TryResolveShiftReduce(int actionX, int actionY, int incomingToken, out int output)
        {
            output = 0;

            int shiftAction, reduceAction;
            if (ParserAction.GetKind(actionX) == ParserActionKind.Shift
                && ParserAction.GetKind(actionY) == ParserActionKind.Reduce)
            {
                shiftAction = actionX;
                reduceAction = actionY;
            }
            else if (ParserAction.GetKind(actionY) == ParserActionKind.Shift
                && ParserAction.GetKind(actionX) == ParserActionKind.Reduce)
            {
                shiftAction = actionY;
                reduceAction = actionX;
            }
            else
            {
#if LALR1_TOLERANT
                // Unsupported conflict type. Use first action
                output = actionX;
#else
                output = ParserAction.Encode(ParserActionKind.Conflict, 0);
#endif
                return false;
            }

            var shiftTokenPrecedence = grammar.GetTermPrecedence(incomingToken);
            var reduceRulePrecedence = grammar.GetProductionPrecedence(ParserAction.GetId(reduceAction));

            if (shiftTokenPrecedence == null && reduceRulePrecedence == null)
            {
#if LALR1_TOLERANT
                // In case of conflict prefer shift over reduce
                output = shiftAction;
#else
                output = ParserAction.Encode(ParserActionKind.Conflict, 0);
#endif
                return false;
            }
            else if (shiftTokenPrecedence == null)
            {
                output = reduceAction;
            }
            else if (reduceRulePrecedence == null)
            {
                output = shiftAction;
            }
            else if (Precedence.IsReduce(reduceRulePrecedence, shiftTokenPrecedence))
            {
                output = reduceAction;
            }
            else
            {
                output = shiftAction;
            }

            return true;
        }
    }
}
