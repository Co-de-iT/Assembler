using System.Collections.Generic;


namespace AssemblerLib
{
    /// <summary>
    /// A structure that manages settings for Heuristics (rules, criteria)
    /// </summary>
    public struct HeuristicsSettings
    {
        /// <summary>
        /// List of Heuristics Sets - each item in the list is a set of rules in text format, to be interpreted by the <see cref="Assemblage"/> class
        /// </summary>
        public readonly List<string> HeuSetsString;
        /// <summary>
        /// Index of the current heuristic set used during an Assemblage
        /// </summary>
        public int CurrentHeuristics;
        /// <summary>
        /// <list type="bullet">
        /// <item><description>0 - manual via <see cref="CurrentHeuristics"/> parameter</description></item>
        /// <item><description>1 - <see cref="Field"/> driven, via <see cref="Field"/> iWeights</description></item>
        /// </list>
        /// </summary>
        public int HeuristicsMode;
        /// <summary>
        /// receiver selection mode
        /// <list type="bullet">
        /// <item><description>0 - random</description></item>
        /// <item><description>1 - scalar Field nearest</description></item>
        /// <item><description>2 - scalar Field interpolated</description></item>
        /// <item><description>3 - dense packing - minimum sum of weights around candidate</description></item>
        /// </list>
        /// </summary>
        public int ReceiverSelectionMode;
        /// <summary>
        /// <see cref="Rule"/> selection mode
        /// <list type="bullet">
        /// <item><description>0 - random</description></item>
        /// <item><description>1 - scalar Field nearest</description></item>
        /// <item><description>2 - scalar Field interpolated</description></item>
        /// <item><description>3 - vector Field nearest (monodirectional)</description></item>
        /// <item><description>4 - vector Field interpolated (monodirectional)</description></item>
        /// <item><description>5 - vector Field nearest (bidirectional)</description></item>
        /// <item><description>6 - vector Field interpolated (bidirectional)</description></item>
        /// <item><description>7 - minimum sender + receiver AABB (Axis-Aligned Bounding Box) volume</description></item>
        /// <item><description>8 - minimum sender + receiver AABB (Axis-Aligned Bounding Box) diagonal</description></item>
        /// <item><description>9 - weighted random choice</description></item>
        /// </list>
        /// </summary>
        public int SenderSelectionMode;
        /// <summary>
        /// Checks whether the Heuristics Settings require a <see cref="Field"/>
        /// </summary>
        public bool IsFieldDependent
        {
            get { return (HeuristicsMode == 1 ||
                    (ReceiverSelectionMode > 0 && ReceiverSelectionMode < 3) ||
                    (SenderSelectionMode > 0 && SenderSelectionMode < 7)); }
        }

        public HeuristicsSettings(List<string> HeuSetsString, int CurrentHeuristics, int HeuristicsMode, int ReceiverSelectionMode, int SenderSelectionMode)
        {
            this.HeuSetsString = HeuSetsString;
            this.CurrentHeuristics = CurrentHeuristics % HeuSetsString.Count; // this makes the index coherent from the get go
            this.HeuristicsMode = HeuristicsMode;
            this.ReceiverSelectionMode = ReceiverSelectionMode;
            this.SenderSelectionMode = SenderSelectionMode;
        }
    }
}
