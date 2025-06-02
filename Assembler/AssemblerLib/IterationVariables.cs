using System.Collections.Generic;

namespace AssemblerLib
{
    // TODO: implement these in the Assemblge class for better readability
    internal struct IterationVariables
    {
        internal List<AssemblyObject> i_CandidateObjects;
        internal int i_senderSeqInd;
        internal int i_receiverAInd;
        internal int i_receiverBranchSeqInd;
        internal List<Rule> i_receiverRules;
        internal List<int> i_validRulesIndexes;
        internal List<int[]> i_candidatesNeighAInd;

        internal void Reset()
        {
            i_CandidateObjects = new List<AssemblyObject>();
            i_senderSeqInd = 0;
            i_receiverAInd = 0;
            i_receiverBranchSeqInd = 0;
            i_receiverRules = new List<Rule>();
            i_validRulesIndexes = new List<int>();
            i_candidatesNeighAInd = new List<int[]>();
        }
    }
}
