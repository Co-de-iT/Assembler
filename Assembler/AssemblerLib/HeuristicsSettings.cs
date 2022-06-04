using System.Collections.Generic;


namespace AssemblerLib
{
    public struct HeuristicsSettings
    {
        /// <summary>
        /// List of Heuristics Sets - each item in the list is a set of rules in text format, to be interpreted by the <see cref="Assemblage"/> class
        /// </summary>
        internal readonly List<string> heuSetsString;
        /// <summary>
        /// Index of the current heuristic set used during an Assemblage
        /// </summary>
        public int currentHeuristics;
        /// <summary>
        /// <list type="bullet">
        /// <item><description>0 - manual via <see cref="currentHeuristics"/> parameter</description></item>
        /// <item><description>1 - <see cref="Field"/> driven, via <see cref="Field"/> iWeights</description></item>
        /// </list>
        /// </summary>
        public int heuristicsMode;
        /// <summary>
        /// receiver selection mode
        /// <list type="bullet">
        /// <item><description>0 - random</description></item>
        /// <item><description>1 - scalar Field nearest</description></item>
        /// <item><description>2 - scalar Field interpolated</description></item>
        /// <item><description>3 - dense packing - minimum sum of weights around candidate</description></item>
        /// </list>
        /// </summary>
        public int receiverSelectionMode;
        /// <summary>
        /// <see cref="Rule"/> selection mode
        /// <list type="bullet">
        /// <item><description>0 - random</description></item>
        /// <item><description>1 - scalar field nearest</description></item>
        /// <item><description>2 - scalar field interpolated</description></item>
        /// <item><description>3 - vector field nearest (monodirectional)</description></item>
        /// <item><description>4 - vector field interpolated (monodirectional)</description></item>
        /// <item><description>5 - vector field nearest (bidirectional)</description></item>
        /// <item><description>6 - vector field interpolated (bidirectional)</description></item>
        /// <item><description>7 - minimum sender + receiver AABB (Axis-Aligned Bounding Box) volume</description></item>
        /// <item><description>8 - minimum sender + receiver AABB (Axis-Aligned Bounding Box) diagonal</description></item>
        /// <item><description>9 - weighted random choice</description></item>
        /// </list>
        /// </summary>
        public int ruleSelectionMode;

        public HeuristicsSettings(List<string> heuSetsString, int currentHeuristics, int heuristicsMode, int receiverSelectionMode, int ruleSelectionMode)
        {
            this.heuSetsString = heuSetsString;
            this.currentHeuristics = currentHeuristics % heuSetsString.Count; // this makes the index coherent from the get go
            this.heuristicsMode = heuristicsMode;
            this.receiverSelectionMode = receiverSelectionMode;
            this.ruleSelectionMode = ruleSelectionMode;
        }
    }
}
