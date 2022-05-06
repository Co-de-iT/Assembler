using System.Collections.Generic;


namespace AssemblerLib
{
    public struct HeuristicsSettings
    {
        /// <summary>
        /// List of Heuristics Sets - each item in the list is a set of rules in text format, to be interpreted by the <see cref="Assemblage"/> class
        /// </summary>
        public List<string> heuSetsString;
        /// <summary>
        /// Index of the current heuristic set used during an Assemblage
        /// </summary>
        public int currentHeuristics;
        /// <summary>
        /// <list type="bullet">
        /// <item>0 - manual via <see cref="currentHeuristics"/> parameter</item>
        /// <item>1 - <see cref="Field"/> driven, via <see cref="Field"/> iWeights</item>
        /// </list>
        /// </summary>
        public int heuristicsMode;
        /// <summary>
        /// receiver selection mode
        /// <list type="bullet">
        /// <item>0 - random</item>
        /// <item>1 - scalar field nearest</item>
        /// <item>2 - scalar field interpolated</item>
        /// <item>3 - dense packing</item>
        /// </list>
        /// </summary>
        public int receiverSelectionMode;
        /// <summary>
        /// <see cref="Rule"/> selection mode
        /// <list type="bullet">
        /// <item>0 - random</item>
        /// <item>1 - scalar field nearest</item>
        /// <item>2 - scalar field interpolated</item>
        /// <item>3 - vector field nearest (bidirectional)</item>
        /// <item>4 - vector field interpolated (bidirectional)</item>
        /// <item>5 - minimum local bounding box volume</item>
        /// <item>6 - minimum local bounding box diagonal</item>
        /// <item>7 - weighted random choice</item>
        /// </list>
        /// </summary>
        public int ruleSelectionMode;

        public HeuristicsSettings(List<string> heuristicsString, int currentHeuristics, int heuristicsMode, int receiverSelectionMode, int ruleSelectionMode)
        {
            this.heuSetsString = heuristicsString;
            this.currentHeuristics = currentHeuristics % heuristicsString.Count; // this makes the index coherent from the get go
            this.heuristicsMode = heuristicsMode;
            this.receiverSelectionMode = receiverSelectionMode;
            this.ruleSelectionMode = ruleSelectionMode;
        }
    }
}
