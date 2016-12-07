﻿using System.Collections.Generic;

namespace IronText.Automata.TurnPlanning
{
    class TokenDfaState
    {
        public Dictionary<int, TokenDfaDecision> Transitions { get; }
            = new Dictionary<int, TokenDfaDecision>();
    }
}