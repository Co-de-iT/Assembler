using System;
using System.Collections.Generic;


namespace AssemblerLib
{
    public struct HeuristicsSettings
    {
        public List<string> heuristicsString;
        public int currentHeuristics;
        public int heuristicsMode;
        public int receiverSelectionMode;
        public int ruleSelectionMode;

        public HeuristicsSettings(List<string> heuristicsString, int currentHeuristics, int heuristicsMode, int receiverSelectionMode, int ruleSelectionMode)
        {
            this.heuristicsString = heuristicsString;
            this.currentHeuristics = currentHeuristics;
            this.heuristicsMode = heuristicsMode;
            this.receiverSelectionMode = receiverSelectionMode;
            this.ruleSelectionMode = ruleSelectionMode;
        }
    }
}
