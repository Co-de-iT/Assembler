//using AssemblerLib.Utils;
//using Grasshopper;
//using Grasshopper.Kernel.Data;
//using Rhino.Geometry;
//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq;
//using System.Threading.Tasks;

// TODO: check if this separation between the Assemblage and Compute classes is useful for future versions

//namespace AssemblerLib
//{
//    /// <summary>
//    /// A class that manages Assemblage computation
//    /// </summary>
//    public class AssemblageCompute
//    {
//        /*
//         A note on indexing:
        
//        . AInd is the unique object index, sequentialIndex is the index in the assemblyObject list
//        . each AssemblyObject in the Assemblage has an AInd assigned when added - this AInd is UNIQUE
//        . previous objects use their own stored AInds
//        . Topology (neighbour objects) stores the AInd of AssemblyObjects
//        . available and unreachable objects list use the AInd
//        . centroidsRTree associates Reference Plane origins with objects Aind
//        . the assemblyObjects Tree stores each object in a dedicated branch whose Path is the AInd
//        . in case an object is removed, other than updating connectivity of neighbours, centroid is removed, 
//        object is removed from available and unreachable list (if present)
//         */

//        #region fields-properties-delegates

//        #region fields

//        public Assemblage Ass;

//        #endregion fields

//        #region properties


//        #endregion properties

//        #region delegates
//        // . . . delegates for sender-receiver computation and selection


//        /// <summary>
//        /// Delegate variable for environment check mode
//        /// </summary>
//        public EnvironmentClashMethod environmentClash;

//        /// <summary>
//        /// Delegate variable for computing sender values
//        /// </summary>
//        public ComputeCandidatesValuesMethod<double> computeSendersValues;

//        /// <summary>
//        /// Delegate variable for computing receiver value
//        /// </summary>
//        public ComputeReceiverMethod<double> computeReceiverValue;
//        /// <summary>
//        /// Delegate variable for selecting a sender from candidates (based on their values)
//        /// </summary>
//        public SelectWinnerMethod<double> selectSender;
//        /// <summary>
//        /// Delegate variable for selecting a receiver from available ones (based on their values)
//        /// </summary>
//        public SelectWinnerMethod<double> selectReceiver;

//        #endregion delegates

//        #region fields

//        /// <summary>
//        /// Experimental fields variable
//        /// </summary>
//        internal Sandbox E_sb;

//        // . . . iteration fields (updated at each Assemblage iteration)
//        public int CurrentReceiver { get => iVars.i_receiverAInd; }
//        /// <summary>
//        /// Iteration variables
//        /// </summary>
//        internal IterationVariables iVars;

//        /// <summary>
//        /// struct with iteration variables (for compactness and practicity)
//        /// </summary>
//        internal struct IterationVariables
//        {
//            /// <summary>
//            /// Candidate sender objects at each iteration
//            /// </summary>
//            internal List<AssemblyObject> i_CandidateObjects;
//            /// <summary>
//            /// Multiplying factors for candidates values at each iteration - affect candidates selection
//            /// </summary>
//            internal List<double> i_CandidateFactors;
//            /// <summary>
//            /// stores selected sender sequential index (in candidates list) at each Assemblage iteration
//            /// </summary>
//            internal int i_senderSeqInd;
//            /// <summary>
//            /// stores selected receiver AInd at each Assemblage iteration
//            /// </summary>
//            internal int i_receiverAInd;
//            /// <summary>
//            /// stores receiver branch sequential index in <see cref="Assemblage.AssemblyObjects"/> Tree at each Assemblage iteration to speed up search 
//            /// </summary>
//            internal int i_receiverBranchSeqInd;
//            /// <summary>
//            /// stores <see cref="Rule"/>s pertaining the selected receiver at each Assemblage iteration
//            /// </summary>
//            internal List<Rule> i_receiverRules;
//            /// <summary>
//            /// stores indexes of filtered valid rules from <see cref="i_receiverRules"/> at each Assemblage iteration
//            /// </summary>
//            internal List<int> i_validRulesIndexes;
//            /// <summary>
//            /// Keeps arrays of neighbours AInd for each valid candidate at each Assemblage iteration
//            /// passes the winning candidate array to Obstruction check (avoid RTree search twice)
//            /// </summary>
//            internal List<int[]> i_candidatesNeighAInd;

//            internal void Reset()
//            {
//                i_CandidateObjects = new List<AssemblyObject>();
//                i_senderSeqInd = 0;
//                i_receiverAInd = 0;
//                i_receiverBranchSeqInd = 0;
//                i_CandidateFactors = new List<double>();
//                i_receiverRules = new List<Rule>();
//                i_validRulesIndexes = new List<int>();
//                i_candidatesNeighAInd = new List<int[]>();
//            }
//        }

//        #endregion fields

//        #endregion fields-properties-delegates

//        #region constructors and initializers
//        /// <summary>
//        /// Empty constructor
//        /// </summary>
//        public AssemblageCompute(Assemblage Ass)
//        {
//            this.Ass = Ass;
//            iVars = new IterationVariables();
//            ResetAssemblageStatus(Ass.HeuristicsSettings, Ass.ExogenousSettings);
//        }

//        #endregion

//        #region setup methods

//        private DataTree<Rule> InitHeuristics(List<string> heuristicsString)
//        {
//            // rules data tree has a path of {k;rT} where k is the heuristics set and rT the receiving type
//            DataTree<Rule> heuristicsTree = new DataTree<Rule>();
//            for (int k = 0; k < heuristicsString.Count; k++)
//            {
//                //             split by list of rules (,)
//                string[] rComp = heuristicsString[k].Split(new[] { ',' });

//                int rT, rH, rR, sT, sH;
//                double rRA;
//                int w;
//                for (int i = 0; i < rComp.Length; i++)
//                {
//                    string[] rule = rComp[i].Split(new[] { '<', '%' });
//                    string[] rec = rule[0].Split(new[] { '|' });
//                    string[] sen = rule[1].Split(new[] { '|' });
//                    // sender and receiver component types
//                    sT = Ass.AOSetDictionary[sen[0]];
//                    rT = Ass.AOSetDictionary[rec[0]];
//                    // sender handle index
//                    sH = Convert.ToInt32(sen[1]);
//                    // weight
//                    w = Convert.ToInt32(rule[2]);
//                    string[] rRot = rec[1].Split(new[] { '=' });
//                    // receiver handle index and rotation
//                    rH = Convert.ToInt32(rRot[0]);
//                    rRA = Convert.ToDouble(rRot[1]);
//                    // using rotations
//                    rR = Ass.AOSet[rT].Handles[rH].RDictionary[rRA];

//                    heuristicsTree.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, w), new GH_Path(k, rT));
//                }
//            }
//            return heuristicsTree;
//        }

//        /// <summary>
//        /// Sets Sandbox geometry
//        /// </summary>
//        /// <param name="sandbox"></param>
//        private void SetSandbox(Box sandbox)
//        {
//            if (!sandbox.IsValid)
//            {
//                E_sb.E_sandbox = Box.Unset;
//                return;
//            }

//            E_sb.E_sandbox = sandbox;
//            Transform scale = Transform.Scale(E_sb.E_sandbox.Center, Constants.SafeScaleMultiplier);
//            E_sb.E_sandbox.Transform(scale);
//            E_sb.E_sandboxAvailableObjects = new List<int>();
//            E_sb.E_sandboxUnreachableObjects = new List<int>();
//            ResetSandboxRtree();
//        }

//        /// <summary>
//        /// Sets Environment Check Method to use
//        /// </summary>
//        private void SetEnvCheckMethod()
//        {
//            switch (Ass.ExogenousSettings.EnvironmentMode)
//            {
//                case EnvironmentModes.Custom:
//                    // custom mode - method assigned in scripted component
//                    environmentClash = Ass.ExogenousSettings.customEnvironmentClash;
//                    break;
//                case EnvironmentModes.Ignore:
//                    environmentClash = (sO, EnvironmentMeshes) => { return false; };
//                    break;
//                case EnvironmentModes.ContainerCollision:
//                    environmentClash = AssemblageUtils.EnvClashCollision;
//                    break;
//                case EnvironmentModes.ContainerInclusion:
//                    environmentClash = AssemblageUtils.EnvClashInclusion;
//                    break;
//                default: // default is inclusion
//                    goto case EnvironmentModes.ContainerInclusion;
//            }
//        }

//        /// <summary>
//        /// sets appropriate delegates for computing receiver values and selection according to the chosen criteria
//        /// </summary>
//        /// <param name="receiverSelectionMode"></param>
//        private void SetReceiverSelectionMode(int receiverSelectionMode)
//        {
//            // set receiver compute and selection delegates
//            switch (receiverSelectionMode)
//            {
//                case -1:
//                    // custom mode - methods assigned in scripted component or iterative mode
//                    computeReceiverValue = Ass.HeuristicsSettings.customComputeReceiverValue;
//                    selectReceiver = Ass.HeuristicsSettings.customSelectReceiver;
//                    break;
//                case 0:
//                    // random selection among available objects
//                    computeReceiverValue = ComputeRZero;
//                    selectReceiver = AssemblageUtils.SelectRandomIndex;
//                    break;
//                case 1:
//                    // scalar Field search - closest Field point
//                    computeReceiverValue = ComputeRScalarField;
//                    selectReceiver = AssemblageUtils.SelectMinIndex;
//                    break;
//                case 2:
//                    // scalar Field search - interpolated values
//                    computeReceiverValue = ComputeRScalarFieldInterpolated;
//                    selectReceiver = AssemblageUtils.SelectMinIndex;
//                    break;
//                case 3:
//                    // maximum sum weight around candidate
//                    computeReceiverValue = ComputeRWeightDensity;
//                    selectReceiver = AssemblageUtils.SelectMaxIndex;
//                    break;

//                // add more criteria here (must return an avInd)
//                // density driven
//                // component weight driven
//                // ....

//                case 99:
//                    // "sequential" mode - return last available object in the list
//                    computeReceiverValue = (ao) => 0; // anonymous function that always returns 0
//                    selectReceiver = (a) => { return Ass.availableObjectsAInds.Count - 1; }; // anonymous function that returns AInd of last available object
//                    break;

//                default: goto case 0;
//            }
//        }

//        /// <summary>
//        /// sets appropriate delegates for computing sender candidates values and selection according to the chosen criteria
//        /// </summary>
//        /// <param name="senderSelectionMode"></param>
//        private void SetSenderSelectionMode(int senderSelectionMode)
//        {
//            // set sender candidates (rules) compute and selection delegates
//            switch (senderSelectionMode)
//            {
//                case -1:
//                    // custom mode - methods assigned in scripted component or iterative mode
//                    computeSendersValues = Ass.HeuristicsSettings.customComputeSendersValues;
//                    selectSender = Ass.HeuristicsSettings.customSelectSender;
//                    break;
//                case 0:
//                    // random selection - chooses one candidate at random
//                    computeSendersValues = ComputeZero;
//                    selectSender = AssemblageUtils.SelectRandomIndex;
//                    break;
//                case 1:
//                    // scalar Field nearest with threshold - chooses candidate whose centroid closest scalar Field value is closer to the threshold
//                    computeSendersValues = ComputeScalarField;
//                    selectSender = AssemblageUtils.SelectMinIndex;// SelectSenderMinIndex;
//                    break;
//                case 2:
//                    // scalar Field interpolated with threshold - chooses candidate whose centroid interpolated scalar Field value is closer to the threshold
//                    computeSendersValues = ComputeScalarFieldInterpolated;
//                    selectSender = AssemblageUtils.SelectMinIndex;//SelectSenderMinIndex;
//                    break;
//                case 3:
//                    // vector Field nearest - chooses candidate whose Direction has minimum angle with closest vector Field point
//                    computeSendersValues = ComputeVectorField;
//                    selectSender = AssemblageUtils.SelectMinIndex;//SelectSenderMinIndex;
//                    break;
//                case 4:
//                    // vector Field interpolated - chooses candidate whose Direction has minimum angle with interpolated vector Field point
//                    computeSendersValues = ComputeVectorFieldInterpolated;
//                    selectSender = AssemblageUtils.SelectMinIndex;//SelectSenderMinIndex;
//                    break;
//                case 5:
//                    // vector Field bidirectional nearest - chooses candidate whose Direction has minimum angle with closest vector Field point (bidirectional)
//                    computeSendersValues = ComputeVectorFieldBidirectional;
//                    selectSender = AssemblageUtils.SelectMinIndex;//SelectSenderMinIndex;
//                    break;
//                case 6:
//                    // vector Field bidirectional interpolated - chooses candidate whose Direction has minimum angle with interpolated vector Field point (bidirectional)
//                    computeSendersValues = ComputeVectorFieldBidirectionalInterpolated;
//                    selectSender = AssemblageUtils.SelectMinIndex;//SelectSenderMinIndex;
//                    break;
//                case 7:
//                    // density search 1 - chooses candidate with minimal bounding box volume with receiver
//                    computeSendersValues = ComputeBBVolume;
//                    selectSender = AssemblageUtils.SelectMinIndex;//SelectSenderMinIndex;
//                    break;
//                case 8:
//                    // density search 2 - chooses candidate with minimal bounding box diagonal with receiver
//                    computeSendersValues = ComputeBBDiagonal;
//                    selectSender = AssemblageUtils.SelectMinIndex;//SelectSenderMinIndex;
//                    break;
//                case 9:
//                    // Weighted Random Choice among valid rules
//                    computeSendersValues = ComputeWRC;
//                    selectSender = AssemblageUtils.SelectWRCIndex;
//                    break;
//                // . add more criteria here
//                // ...
//                //
//                //case 99:
//                //    // sequential Rule - tries to apply the heuristics set rules in sequence (buggy)
//                //    // anonymous function - the computation is not necessary
//                //    computeSendersValues = ComputeZero;
//                //    //computeSendersValues = (candidates) => { return candidates.Select(ri => 0.0).ToArray(); };
//                //    selectSender = SelectNextRuleIndex;
//                //    break;

//                default: goto case 0;
//            }
//        }

//        #endregion

//        #region update methods

//        /// <summary>
//        /// Update method
//        /// 
//        /// Update is composed by the following steps:
//        /// <list type="number">
//        /// <item><description>receiver selection (where do I add the next item?)</description></item>
//        /// <item><description>rule selection (which one and how?)</description></item>
//        /// <item><description>new object addition to the assemblage</description></item>
//        /// <item><description>assemblage status update</description></item>
//        /// </list>
//        /// </summary>
//        /// <remarks>The method is virtual, so it can be customized with an override</remarks>
//        /// <example>
//        /// This is the standard base implementation:
//        /// <code>
//        /// public override Update()
//        ///{
//        ///    // 0. reset iteration variables
//        ///    ResetIterationVariables();
//        ///    // 1. receiver selection and candidates retrieval attempt
//        ///    // if there are no available candidates return
//        ///    if (!TryGetReceiverAndCandidates()) return;
//        ///    // 2. candidate selection
//        ///    AssemblyObject newObject;
//        ///    Rule rule;
//        ///    (newObject, rule) = SelectCandidate(CandidateObjects, computeSendersValues(CandidateObjects));
//        ///    // 3. new object addition
//        ///    AddValidObject(newObject, rule);
//        ///    // 4. Assemblage status update
//        ///    AssemblageUtils.ObstructionCheckAssemblage(this, newObject.AInd, i_candidatesNeighAInd[i_senderSeqInd]);
//        ///}
//        /// </code>
//        /// </example>
//        public virtual void Update()
//        {
//            // . . . . . . .    0. reset iteration variables

//            iVars.Reset();
//            //ResetIterationVariables();

//            // . . . . . . .    1. receiver selection and candidates retrieval attempt

//            // if there are no available candidates return
//            if (!TryGetReceiverAndCandidates()) return;


//            // . . . . . . .    2. candidate selection

//            AssemblyObject newObject;
//            Rule rule;

//            // compute Candidates values
//            double[] candidateValues = computeSendersValues(iVars.i_CandidateObjects);

//            // add Candidates factors to the values
//            for (int i = 0; i < candidateValues.Length; i++)
//                candidateValues[i] += iVars.i_CandidateFactors[i];

//            (newObject, rule) = SelectCandidate(iVars.i_CandidateObjects, candidateValues);

//            // . . . . . . .    3. new object addition

//            AddValidObject(newObject, rule);

//            // . . . . . . .    4. Assemblage status update

//            // check for obstructions and/or secondary handle connections
//            // check if newly added object obstructs other Handles in the surroundings
//            // or its Handles are obstructed in turn by other objects
//            AssemblageUtils.ObstructionCheckAssemblage(Ass, newObject.AInd, iVars.i_candidatesNeighAInd[iVars.i_senderSeqInd]);
//        }

//        /// <summary>
//        /// A virtual method that tries to retrieve a valid receiver and compile related list of candidates and their factors
//        /// </summary>
//        /// <returns>True if a valid receiver is found, False otherwise</returns>
//        /// <remarks>The method is virtual, so it can be customized with an override</remarks>
//        public virtual bool TryGetReceiverAndCandidates()
//        {
//            while (Ass.availableObjectsAInds.Count > 0)
//            {
//                // . . . . . . .    0. receiver selection

//                // select receiver from list of available values
//                // sequential receiver index in the available objects AInd list
//                int receiverSeqInd = selectReceiver(Ass.availableReceiverValues.ToArray());

//                // convert sequential index into AInd and find its related branch Path
//                iVars.i_receiverAInd = Ass.availableObjectsAInds[receiverSeqInd];
//                iVars.i_receiverBranchSeqInd = Ass.AssemblyObjects.Paths.IndexOf(new GH_Path(iVars.i_receiverAInd));

//                if (IsReceiverOutsideField(iVars.i_receiverBranchSeqInd))
//                {
//                    MarkAsUnreachable(receiverSeqInd);
//                    continue;
//                }

//                // . . . . . . .    1. candidates retrieval attempt

//                if (RetrieveCandidates())
//                    break;
//                else
//                    MarkAsUnreachable(receiverSeqInd);
//            }

//            return iVars.i_CandidateObjects.Count > 0;
//        }

//        private void MarkAsUnreachable(int availableSeqIndex)
//        {
//            Ass.unreachableObjectsAInds.Add(Ass.availableObjectsAInds[availableSeqIndex]);
//            Ass.availableObjectsAInds.RemoveAt(availableSeqIndex);
//            Ass.availableReceiverValues.RemoveAt(availableSeqIndex);
//        }

//        /// <summary>
//        /// Tests whether the currently picked receiving <see cref="AssemblyObject"/> is outside the Field
//        /// </summary>
//        /// <param name="availableSeqIndex"></param>
//        /// <returns></returns>
//        private bool IsReceiverOutsideField(int AOBranchSeqInd)
//        {
//            // if Field is null or Heuristics Settings are not Field dependent return false
//            if (Ass.ExogenousSettings.Field == null || !Ass.HeuristicsSettings.IsFieldDependent) return false;

//            Point3d origin = Ass.AssemblyObjects.Branch(AOBranchSeqInd)[0].ReferencePlane.Origin;
//            int closestPointInd = Ass.ExogenousSettings.Field.GetClosestIndex(origin);
//            Point3d fieldClosestPoint = Ass.ExogenousSettings.Field.GetPoints()[closestPointInd];
//            double distanceSquared = origin.DistanceToSquared(fieldClosestPoint);

//            return distanceSquared > Ass.ExogenousSettings.Field.MaxDistSquare;
//        }

//        /// <summary>
//        /// Retrieve the <see cref="IterationVariables.i_CandidateObjects"/> for the current iteration
//        /// </summary>
//        /// <returns>True if at least one suitable candidate has been found, False otherwise</returns>
//        /// <remarks>This function sifts candidates filtering invalid results (i.e. collisions, environment), eventually
//        /// compiling lists of: <see cref="IterationVariables.i_validRulesIndexes"/>, <see cref="IterationVariables.i_CandidateObjects"/>, <see cref="IterationVariables.i_candidatesNeighAInd"/>, <see cref="IterationVariables.i_CandidateFactors"/></remarks>
//        public bool RetrieveCandidates()
//        {
//            // . find receiver object type
//            int receiverType = Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].Type;

//            // select current heuristics - check if heuristic mode is set to Field driven
//            // in that case, use the receiver's iWeight (where the heuristics index is stored)
//            if (Ass.HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
//                Ass.currentHeuristicsIndex = Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].IWeight;

//            // . sanity check on rules
//            // it is possible, when using a custom set of rules, that an object is only used as sender
//            // or it is not included in the selected heuristics. In such cases, there will be no associated rules
//            // in case it is picked at random as a potential receiver, so we return empties and false

//            if (!Ass.heuristicsTree.PathExists(Ass.currentHeuristicsIndex, receiverType))
//            {
//                iVars.i_receiverRules = new List<Rule>();
//                return false;
//            }

//            // if a path exists.....
//            // . retrieve all rules for receiving object and properly define return variables
//            AssemblyObject candidateObject;
//            iVars.i_receiverRules = Ass.heuristicsTree.Branch(Ass.currentHeuristicsIndex, receiverType);
//            int[] neighbourIndexes;

//            // orient all candidates around receiving object and keep track of valid indices
//            // parse through all rules and filter valid ones
//            for (int i = 0; i < iVars.i_receiverRules.Count; i++)
//            {
//                // if receiver handle isn't free skip to next rule
//                if (Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].Handles[iVars.i_receiverRules[i].rH].Occupancy != 0) continue;

//                if (!CheckCandidate(iVars.i_receiverRules[i], Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0], out candidateObject, out neighbourIndexes)) continue;

//                // if checks were passed add new objects to candidates and
//                // corresponding rule index to valid list
//                iVars.i_validRulesIndexes.Add(i);
//                iVars.i_CandidateObjects.Add(candidateObject);
//                double candidateFactor = iVars.i_receiverRules[i].iWeight + candidateObject.Weight + candidateObject.Handles[iVars.i_receiverRules[i].sH].Weight;
//                iVars.i_CandidateFactors.Add(candidateFactor);
//                // neighbourIndexes are saved for Obstruction check
//                iVars.i_candidatesNeighAInd.Add(neighbourIndexes);
//            }

//            return iVars.i_CandidateObjects.Count > 0;
//        }

//        private bool CheckCandidate(Rule rule, AssemblyObject receiver, out AssemblyObject candidateObject, out int[] neighbourIndexes)
//        {
//            // make a copy of corresponding sender type from catalog
//            candidateObject = AssemblyObjectUtils.Clone(Ass.AOSet[rule.sT]);
//            neighbourIndexes = null;

//            // create Transformation
//            Transform orient = Transform.PlaneToPlane(Ass.AOSet[rule.sT].Handles[rule.sH].SenderPlane, receiver.Handles[rule.rH].ReceiverPlanes[rule.rR]);

//            // transform candidate sender object
//            candidateObject.Transform(orient);

//            // verify Z lock
//            // if absolute Z lock is true for the current object...
//            if (Ass.CheckWorldZLock && candidateObject.WorldZLock)
//                // ...perform that check too - if test is not passed return false
//                if (!AssemblyObjectUtils.AbsoluteZCheck(candidateObject, Constants.RhinoAbsoluteTolerance))
//                {
//                    candidateObject = null;
//                    return false;
//                }

//            // verify environment clash
//            // if the object clashes with the environment return false
//            if (environmentClash(candidateObject, Ass.ExogenousSettings.EnvironmentMeshes))
//            {
//                candidateObject = null;
//                return false;
//            }

//            // verify clash with existing assemblage
//            // if the object clashes with surrounding objects return false
//            if (AssemblageUtils.IsAOCollidingWithAssemblage(Ass, candidateObject, out neighbourIndexes))
//            {
//                candidateObject = null;
//                neighbourIndexes = null;
//                return false;
//            }

//            return true;
//        }

//        ///// <summary>
//        ///// A virtual method for <see cref="Rule"/> selection - default version uses internally predefined criteria
//        ///// </summary>
//        ///// <param name="candidates">List of candidates <see cref="AssemblyObject"/>s</param>
//        ///// <param name="sendersvalues">Array of senders values to compute the winner candidate</param>
//        ///// <param name="newObject">new <see cref="AssemblyObject"/> to add to the <see cref="Assemblage"/></param>
//        ///// <returns>The selected <see cref="Rule"/></returns>
//        ///// <remarks>The method is virtual, so it can be customized with an override</remarks>
//        ////public virtual Rule SelectRule(List<AssemblyObject> candidates, out AssemblyObject newObject)
//        ////{
//        ////    // sequential index of winner candidate
//        ////    int winnerIndex;
//        ////    double[] sendersvalues = computeSendersValues(candidates);
//        ////    winnerIndex = selectSender(sendersvalues);
//        ////    i_senderSeqInd = winnerIndex;
//        ////    // new Object is found
//        ////    newObject = candidates[winnerIndex];
//        ////    // record its sender value before returning
//        ////    newObject.SenderValue = sendersvalues[winnerIndex];
//        ////    return i_receiverRules[i_validRulesIndexes[winnerIndex]];
//        ////}
//        //public virtual Rule SelectRule(List<AssemblyObject> candidates, double[] sendersvalues, out AssemblyObject newObject)
//        //{
//        //    // sequential index of winner candidate
//        //    int winnerIndex;
//        //    winnerIndex = selectSender(sendersvalues);
//        //    i_senderSeqInd = winnerIndex;
//        //    // new Object is found
//        //    newObject = candidates[winnerIndex];
//        //    // record its sender value before returning
//        //    newObject.SenderValue = sendersvalues[winnerIndex];
//        //    return i_receiverRules[i_validRulesIndexes[winnerIndex]];
//        //}

//        /// <summary>
//        /// A virtual method to select a winner <see cref="AssemblyObject"/> candidate and its associated <see cref="Rule"/>
//        /// </summary>
//        /// <param name="candidates">List of candidates <see cref="AssemblyObject"/>s</param>
//        /// <param name="sendersvalues">Array of senders values to compute the winner candidate</param>
//        /// <returns></returns>
//        public virtual (AssemblyObject, Rule) SelectCandidate(List<AssemblyObject> candidates, double[] sendersvalues)
//        {
//            AssemblyObject winnerCandidate;
//            Rule rule;

//            // sequential index of winner candidate
//            int winnerIndex;
//            winnerIndex = selectSender(sendersvalues);
//            iVars.i_senderSeqInd = winnerIndex;

//            // new Object is found
//            winnerCandidate = candidates[winnerIndex];

//            // record its sender value before returning
//            winnerCandidate.SenderValue = sendersvalues[winnerIndex];
//            rule = iVars.i_receiverRules[iVars.i_validRulesIndexes[winnerIndex]];

//            return (winnerCandidate, rule);
//        }

//        /// <summary>
//        /// Adds a valid <see cref="AssemblyObject"/> to the <see cref="Assemblage"/>, updating connectivity
//        /// </summary>
//        /// <param name="newObject"></param>
//        /// <param name="rule"></param>
//        /// 
//        public virtual void AddValidObject(AssemblyObject newObject, Rule rule)
//        {
//            // assign index (in future implementations, check for index uniqueness or transform in Hash)
//            newObject.AInd = Ass.nextAInd;
//            Ass.nextAInd++;
//            GH_Path newObjectPath = new GH_Path(newObject.AInd);

//            // add rule to sequence as string
//            Ass.AssemblageRules.Add(rule.ToString(), newObjectPath);

//            // add receiver object AInd to list
//            Ass.ReceiverAIndexes.Add(iVars.i_receiverAInd, newObjectPath);

//            // . . . . . UPDATE HANDLES
//            // update sender + receiver handle status (handle index, occupancy, neighbourObject, neighbourHandle, weight)
//            HandleUtils.UpdateHandlesOnConnection(newObject, rule.sH, Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0], rule.rH, 1, rule.rR);

//            // compute newObject receiver value (if Receiver Selection mode is not -1)
//            if (Ass.HeuristicsSettings.ReceiverSelectionMode != -1)
//            {
//                newObject.ReceiverValue = computeReceiverValue(newObject);
//                if (Ass.HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
//                    newObject.IWeight = ComputeReceiveriWeight(newObject);
//            }

//            // add centroid to assemblage centroids tree
//            // future implementation: if object has children, insert all children centroids under the same AO index (assemblage.Count in this case)
//            Ass.centroidsTree.Insert(newObject.ReferencePlane.Origin, newObject.AInd);
//            Ass.centroidsAInds.Add(newObject.AInd);

//            // add new object to available objects indexes and its receiver value to the list
//            Ass.availableObjectsAInds.Add(newObject.AInd);
//            Ass.availableReceiverValues.Add(newObject.ReceiverValue);

//            // add new object to assemblage, its AInd to the index map
//            Ass.AssemblyObjects.Add(newObject, newObjectPath);

//            // if receiving object is fully occupied (all Handles either connected or occluded) remove it from the available objects
//            if (Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].Handles.Where(x => x.Occupancy != 0).Sum(x => 1) == Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].Handles.Length)
//            {
//                Ass.availableReceiverValues.RemoveAt(Ass.availableObjectsAInds.IndexOf(iVars.i_receiverAInd));
//                Ass.availableObjectsAInds.Remove(iVars.i_receiverAInd);
//            }
//        }

//        #endregion

//        #region compute receiver methods

//        private double ComputeRZero(AssemblyObject receiver) => 0.0;

//        //private double ComputeRRandom(AssemblyObject receiver) => MathUtils.rnd.NextDouble();

//        /// <summary>
//        /// Computes receiver scalar Field with sign
//        /// </summary>
//        /// <param name="receiver"></param>
//        /// <returns></returns>
//        private double ComputeRScalarFieldSigned(AssemblyObject receiver)
//        {
//            return Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetClosestScalar(receiver.ReferencePlane.Origin);
//        }
//        /// <summary>
//        /// Computes receiver scalar Field within threshold; receivers outside will return -1
//        /// to stay within the threshold couple it with SelectMaxValue
//        /// </summary>
//        /// <param name="receiver"></param>
//        /// <returns></returns>
//        private double ComputeRScalarFieldWithin(AssemblyObject receiver)
//        {
//            double scalarValue = Ass.ExogenousSettings.Field.GetClosestScalar(receiver.ReferencePlane.Origin);
//            if (scalarValue < Ass.ExogenousSettings.FieldScalarThreshold) return -1;
//            else return scalarValue;
//        }

//        private double ComputeRScalarField(AssemblyObject receiver)
//        {
//            return Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetClosestScalar(receiver.ReferencePlane.Origin));
//        }

//        private double ComputeRScalarFieldInterpolated(AssemblyObject receiver)
//        {
//            return Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetInterpolatedScalar(receiver.ReferencePlane.Origin));
//        }
//        /// <summary>
//        /// Computes absolute difference between scalar <see cref="Field"/> value and threshold from each free <see cref="Handle"/> 
//        /// </summary>
//        /// <param name="receiver"></param>
//        /// <returns>the minimum absolute difference from the threshold</returns>
//        private double ComputeRScalarFieldHandles(AssemblyObject receiver)
//        {
//            double scalarValue = double.MaxValue;
//            double handleValue;
//            foreach (Handle h in receiver.Handles)
//            {
//                if (h.Occupancy != 0) continue;
//                handleValue = Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetClosestScalar(h.SenderPlane.Origin));
//                if (handleValue < scalarValue) scalarValue = handleValue;
//            }
//            return scalarValue;
//        }
//        /// <summary>
//        /// Computes sum of <see cref="AssemblyObject"/> weights in a search sphere, updating neighbours accordingly
//        /// </summary>
//        /// <param name="receiver"></param>
//        /// <returns>the weights sum</returns>
//        /// 
//        private double ComputeRWeightDensity(AssemblyObject receiver)
//        {
//            // search for neighbour objects in radius
//            double density = 0;
//            Ass.centroidsTree.Search(new Sphere(receiver.ReferencePlane.Origin, Ass.CollisionRadius), (s, args) =>
//            {
//                GH_Path neighPath = new GH_Path(Ass.centroidsAInds[args.Id]);
//                density += Ass.AssemblyObjects[neighPath, 0].Weight;
//                // update neighbour object receiver value with current weight
//                Ass.AssemblyObjects[neighPath, 0].ReceiverValue += receiver.Weight;
//            });

//            return density;
//        }

//        private void ComputeReceiverValue(AssemblyObject AO, double rValue = 0)
//        {
//            // compute AO receiver value (if Receiver Selection mode is not -1 or custom compute method is not null)
//            if (Ass.ShouldComputeReceiver)
//                //if (HeuristicsSettings.ReceiverSelectionMode != -1 || HeuristicsSettings.customComputeReceiverValue != null)
//                AO.ReceiverValue = computeReceiverValue(AO);
//            else
//                AO.ReceiverValue = rValue;
//        }

//        public void ComputeReceiversValues()
//        {
//            if (Ass.AssemblyObjects.BranchCount < 1000)
//                for (int i = 0; i < Ass.AssemblyObjects.BranchCount; i++)
//                    ComputeReceiverValue(Ass.AssemblyObjects.Branches[i][0]);
//            else
//            {
//                Parallel.For(0, Ass.AssemblyObjects.BranchCount, i =>
//                {
//                    ComputeReceiverValue(Ass.AssemblyObjects.Branches[i][0]);
//                });
//            }
//        }

//        private int ComputeReceiveriWeight(AssemblyObject receiver) => Ass.ExogenousSettings.Field.GetClosestiWeights(receiver.ReferencePlane.Origin)[0];

//        private void ComputeReceiversiWeights()
//        {
//            if (Ass.AssemblyObjects.BranchCount < 1000)
//                for (int i = 0; i < Ass.AssemblyObjects.BranchCount; i++)
//                    Ass.AssemblyObjects.Branches[i][0].IWeight = ComputeReceiveriWeight(Ass.AssemblyObjects.Branches[i][0]);
//            else
//            {
//                Parallel.For(0, Ass.AssemblyObjects.BranchCount, i =>
//                {
//                    Ass.AssemblyObjects.Branches[i][0].IWeight = ComputeReceiveriWeight(Ass.AssemblyObjects.Branches[i][0]);
//                });
//            }
//        }

//        /// <summary>
//        /// Compute scalar and iWeight values for all Receivers
//        /// </summary>
//        public void ComputeReceiversValuesiWeights()
//        {
//            // compute receiver values
//            ComputeReceiversValues();
//            // if heuristics is Field driven compute iWeights
//            if (Ass.HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
//                ComputeReceiversiWeights();
//        }

//        #endregion

//        #region compute candidates methods

//        private double[] ComputeZero(List<AssemblyObject> candidates) => candidates.Select(c => 0.0).ToArray();
//        private double[] ComputeRandom(List<AssemblyObject> candidates) => candidates.Select(c => MathUtils.rnd.NextDouble()).ToArray();
//        private double[] ComputeBBVolume(List<AssemblyObject> candidates)
//        {
//            BoundingBox bBox;

//            double[] BBvolumes = new double[candidates.Count];

//            // compute BBvolume for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    bBox = Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
//                    bBox.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
//                    BBvolumes[i] = bBox.Volume;
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    BoundingBox bBoxpar = Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
//                    bBoxpar.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
//                    BBvolumes[i] = bBoxpar.Volume;
//                });

//            return BBvolumes;
//        }

//        private double[] ComputeBBDiagonal(List<AssemblyObject> candidates)
//        {
//            BoundingBox bBox;

//            double[] BBdiagonals = new double[candidates.Count];

//            // compute BBvolume for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    bBox = Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
//                    bBox.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
//                    BBdiagonals[i] = bBox.Diagonal.Length;
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    BoundingBox bBoxpar = Ass.AssemblyObjects.Branch(iVars.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
//                    bBoxpar.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
//                    BBdiagonals[i] = bBoxpar.Diagonal.Length;
//                });

//            return BBdiagonals;
//        }

//        private double[] ComputeScalarField(List<AssemblyObject> candidates)
//        {
//            double[] scalarValues = new double[candidates.Count];

//            // compute scalarvalue for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    // TODO: try, instead of Math.Abs(), the versions in MathUtils
//                    scalarValues[i] = Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetClosestScalar(candidates[i].ReferencePlane.Origin));
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    scalarValues[i] = Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetClosestScalar(candidates[i].ReferencePlane.Origin));
//                });

//            return scalarValues;
//        }

//        private double[] ComputeScalarFieldInterpolated(List<AssemblyObject> candidates)
//        {
//            double[] scalarValues = new double[candidates.Count];

//            // compute scalarvalue for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    scalarValues[i] = Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetInterpolatedScalar(candidates[i].ReferencePlane.Origin));
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    scalarValues[i] = Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetInterpolatedScalar(candidates[i].ReferencePlane.Origin));
//                });

//            return scalarValues;
//        }

//        private double[] ComputeVectorField(List<AssemblyObject> candidates)
//        {
//            double[] vectorValues = new double[candidates.Count];

//            // compute Vector angle value for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].Direction, Ass.ExogenousSettings.Field.GetClosestVector(candidates[i].ReferencePlane.Origin));
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].Direction, Ass.ExogenousSettings.Field.GetClosestVector(candidates[i].ReferencePlane.Origin));
//                });

//            return vectorValues;
//        }

//        private double[] ComputeVectorFieldBidirectional(List<AssemblyObject> candidates)
//        {
//            double[] vectorValues = new double[candidates.Count];

//            // compute bidirectional Vector angle value for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    vectorValues[i] = 1 - Math.Abs(candidates[i].Direction * Ass.ExogenousSettings.Field.GetClosestVector(candidates[i].ReferencePlane.Origin));
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    vectorValues[i] = 1 - Math.Abs(candidates[i].Direction * Ass.ExogenousSettings.Field.GetClosestVector(candidates[i].ReferencePlane.Origin));
//                });

//            return vectorValues;
//        }

//        private double[] ComputeVectorFieldInterpolated(List<AssemblyObject> candidates)
//        {
//            double[] vectorValues = new double[candidates.Count];

//            // compute Vector angle value for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].Direction, Ass.ExogenousSettings.Field.GetInterpolatedVector(candidates[i].ReferencePlane.Origin));
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].Direction, Ass.ExogenousSettings.Field.GetInterpolatedVector(candidates[i].ReferencePlane.Origin));
//                });

//            return vectorValues;
//        }

//        private double[] ComputeVectorFieldBidirectionalInterpolated(List<AssemblyObject> candidates)
//        {
//            double[] vectorValues = new double[candidates.Count];

//            // compute bidirectional Vector angle value for all candidates
//            if (candidates.Count < 100)
//                for (int i = 0; i < candidates.Count; i++)
//                {
//                    vectorValues[i] = 1 - Math.Abs(candidates[i].Direction * Ass.ExogenousSettings.Field.GetInterpolatedVector(candidates[i].ReferencePlane.Origin));
//                }
//            else
//                Parallel.For(0, candidates.Count, i =>
//                {
//                    vectorValues[i] = 1 - Math.Abs(candidates[i].Direction * Ass.ExogenousSettings.Field.GetInterpolatedVector(candidates[i].ReferencePlane.Origin));
//                });

//            return vectorValues;
//        }

//        private double[] ComputeWRC(List<AssemblyObject> candidates)
//        {
//            double[] wrcWeights = new double[candidates.Count];

//            for (int i = 0; i < iVars.i_validRulesIndexes.Count; i++)
//                wrcWeights[i] = iVars.i_receiverRules[iVars.i_validRulesIndexes[i]].iWeight;

//            return wrcWeights;
//        }

//        #endregion

//        #region select value methods


//        #endregion

//        #region reset methods

//        /// <summary>
//        /// Reset Assemblage Status - Reset Heuristics and Exogenous Settings, Compute Receivers and Reset AOs Occupancy Status
//        /// </summary>
//        /// <param name="Heu"></param>
//        /// <param name="Exo"></param>
//        public void ResetAssemblageStatus(HeuristicsSettings Heu, ExogenousSettings Exo)
//        {
//            ResetSettings(Heu, Exo);
//            ComputeReceiversValuesiWeights();
//            ResetAOsOccupancyStatus();
//        }

//        /// <summary>
//        /// Reset Heuristics and Exogenous Settings
//        /// </summary>
//        /// <param name="Heu">Heuristics Setting</param>
//        /// <param name="Exo">Exogenous Settings</param>
//        public void ResetSettings(HeuristicsSettings Heu, ExogenousSettings Exo)
//        {
//            Ass.HeuristicsSettings = Heu;
//            Ass.ExogenousSettings = Exo;

//            // compute AO receiver value if Receiver Selection mode is not -1 or custom compute method is not null
//            Ass.ShouldComputeReceiver = Ass.HeuristicsSettings.ReceiverSelectionMode != -1 || Ass.HeuristicsSettings.customComputeReceiverValue != null;

//            // reset heuristicsTree
//            Ass.heuristicsTree = InitHeuristics(Ass.HeuristicsSettings.HeuSetsString);
//            // comment this in case CurrentHeuristics is taken directly from HeuristicsSettings
//            Ass.currentHeuristicsIndex = Ass.HeuristicsSettings.CurrentHeuristics;

//            // reset environment
//            SetEnvCheckMethod();
//            SetSandbox(Ass.ExogenousSettings.SandBox);

//            // reset selection modes
//            SetReceiverSelectionMode(Ass.HeuristicsSettings.ReceiverSelectionMode);
//            SetSenderSelectionMode(Ass.HeuristicsSettings.SenderSelectionMode);
//        }

//        // EXP: experimental. Yet to be implemented
//        private void ResetSandboxRtree()
//        {
//            E_sb.E_sandboxCentroidsTree = new RTree();
//            E_sb.E_sandboxCentroidsAO = new List<int>();
//            // create List of centroid correspondance with their AO
//            Ass.centroidsTree.Search(E_sb.E_sandbox.BoundingBox, (sender, args) =>
//            {
//                // recover the AssemblyObject centroid related to the found centroid
//                // args.Id contains the AInd
//                E_sb.E_sandboxCentroidsTree.Insert(Ass.AssemblyObjects[new GH_Path(args.Id), 0].ReferencePlane.Origin, args.Id);
//                E_sb.E_sandboxCentroidsAO.Add(args.Id);
//            });
//        }

//        /// <summary>
//        /// Verify list of available/unreachable objects according to current environment and heuristics
//        /// </summary>
//        public void ResetAOsOccupancyStatus()
//        {
//            // . . . . . . .    1. move all AInds to available AInd list and clear unreachable AInd list

//            ResetAvailableUnreachableObjects();

//            // . . . . . . .    2. scan available objects

//            ScanAvailableObjects();

//            // . . . . . . .    3. [OPTIONAL] add to specific Sandbox lists if a valid Sandbox is present

//            //if (E_sb.E_sandbox.Volume > 0)
//            //{
//            //    E_sb.E_sandboxAvailableObjects.Clear();
//            //    E_sb.E_sandboxUnreachableObjects.Clear();
//            //    foreach (int avOb in Ass.availableObjectsAInds)
//            //        if (E_sb.E_sandbox.Contains(Ass.AssemblyObjects[new GH_Path(avOb), 0].ReferencePlane.Origin)) E_sb.E_sandboxAvailableObjects.Add(avOb);
//            //    //if (E_sandboxCentroidsAO.Contains(avOb)) E_sandboxAvailableObjects.Add(avOb);
//            //    foreach (int unrOb in Ass.unreachableObjectsAInds)
//            //        if (E_sb.E_sandbox.Contains(Ass.AssemblyObjects[new GH_Path(unrOb), 0].ReferencePlane.Origin)) E_sb.E_sandboxAvailableObjects.Add(unrOb);
//            //    //if (E_sandboxCentroidsAO.Contains(unrOb)) E_sandboxUnreachableObjects.Add(unrOb);
//            //}

//        }

//        private void ResetAvailableUnreachableObjects()
//        {
//            // move all unreachable indexes to available and clear unrechable list 
//            foreach (int unreachAInd in Ass.unreachableObjectsAInds)
//                if (!Ass.availableObjectsAInds.Contains(unreachAInd)) Ass.availableObjectsAInds.Add(unreachAInd);

//            Ass.unreachableObjectsAInds.Clear();

//            // clear availableReceiverValues list and set updated values
//            Ass.availableReceiverValues.Clear();

//            PopulateAvailableReceiverValues();
//        }

//        public void PopulateAvailableReceiverValues()
//        {
//            for (int i = 0; i < Ass.availableObjectsAInds.Count; i++)
//                Ass.availableReceiverValues.Add(Ass.AssemblyObjects[new GH_Path(Ass.availableObjectsAInds[i]), 0].ReceiverValue);
//        }

//        private void ScanAvailableObjects()
//        {
//            // check for every available object and move from available to unreachable if:
//            // 2.1. they do not pass environmental geometry check
//            // 2.2. they do not pass rules checks:
//            //  2.2.1. there aren't rules with their rType
//            //  2.2.2. rules exists for their rType but no match for any of its free Handles
//            //  2.2.3. rules exist for a free Handle but sender placement is precluded by other objects (AOs or environmental)

//            for (int i = Ass.availableObjectsAInds.Count - 1; i >= 0; i--)
//            {
//                AssemblyObject avObject = Ass.AssemblyObjects[new GH_Path(Ass.availableObjectsAInds[i]), 0];

//                // . . . . 2.1. environmental geometry check

//                if (environmentClash(avObject, Ass.ExogenousSettings.EnvironmentMeshes))
//                {
//                    MarkAsUnreachable(i);
//                    continue;
//                }

//                // . . . . 2.2. rules check

//                // check if current heuristics is fixed or Field-dependent
//                if (Ass.HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
//                    Ass.currentHeuristicsIndex = avObject.IWeight; // when starting an Assemblage this is still -1!!!
//                // current heuristics tree path to search for {current heuristics; receiver type}
//                GH_Path rulesPath = new GH_Path(Ass.currentHeuristicsIndex, avObject.Type);

//                // . . . . . 2.2.1. check if there aren't rules with AO rType

//                // if object type cannot be a receiver (it hasn't a path in the heuristics tree)
//                // assign as unreachable and continue to the next available AO
//                if (!Ass.heuristicsTree.PathExists(rulesPath))
//                {
//                    MarkAsUnreachable(i);
//                    continue;
//                }

//                // . . . . . 2.2.2. rules exists for AO rType but no match for any of its free Handles
//                // . . . . . 2.2.3. rules exist for a free Handle but sender placement is precluded by other objects (AOs or environmental)

//                // test if unreachable after looping through all Handles
//                bool unreachable = CheckHandlesAndRules(avObject, rulesPath);
//                if (unreachable) MarkAsUnreachable(i);

//            }
//        }

//        private bool CheckHandlesAndRules(AssemblyObject avObject, GH_Path rulesPath)
//        {
//            bool unreachable = true;
//            // scan AO Handles against current heuristics rules
//            for (int j = 0; j < avObject.Handles.Length; j++)
//            {
//                // continue if handle is not available
//                if (avObject.Handles[j].Occupancy != 0) continue;

//                foreach (Rule rule in Ass.heuristicsTree.Branch(rulesPath))
//                    // if there is at least a free handle with an available rule for it (rH is the receiving handle index)
//                    // AND its related candidate fits without clashing (using discards on out variables)
//                    if (rule.rH == j && CheckCandidate(rule, avObject, out _, out _))
//                    {
//                        // activate flag and break loop
//                        unreachable = false;
//                        break;
//                    }
//                if (!unreachable) break;
//            }
//            return unreachable;
//        }
//        #endregion

//        #region extract methods

//        /// <summary>
//        /// Extract rules associated with each candidate
//        /// </summary>
//        /// <returns></returns>
//        public List<Rule> ExtractCandidatesRules()
//        {
//            List<Rule> rules = new List<Rule>();
//            for (int i = 0; i < iVars.i_validRulesIndexes.Count; i++)
//                rules.Add(iVars.i_receiverRules[iVars.i_validRulesIndexes[i]]);

//            return rules;
//        }

//        #endregion

//        #region debug methods

//        #endregion

//    }
//}
