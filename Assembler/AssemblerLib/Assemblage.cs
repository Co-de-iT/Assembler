using AssemblerLib.Utils;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace AssemblerLib
{
    /// <summary>
    /// A class that manages Assemblages, including <see cref="AssemblyObject"/>s, their Topology,
    /// <see cref="HeuristicsSettings"/>, <see cref="ExogenousSettings"/>, and all related data.
    /// </summary>
    public class Assemblage
    {
        /*
         A note on indexing:
        
        . AInd is the unique object index, sequentialIndex is the index in the assemblyObject list
        . each AssemblyObject in the Assemblage has an AInd assigned when added - this AInd is UNIQUE
        . previous objects use their own stored AInds
        . Topology (neighbour objects) stores the AInd of AssemblyObjects
        . available and unreachable objects list use the AInd
        . centroidsRTree associates Reference Plane origins with objects Aind
        . the assemblyObjects Tree stores each object in a dedicated branch whose Path is the AInd
        . in case an object is removed, other than updating connectivity of neighbours, centroid is removed, 
        object is removed from available and unreachable list (if present)
         */

        #region fields-properties-delegates

        #region properties
        /// <summary>
        /// The DataTree of <see cref="AssemblyObject"/>s in the Assemblage; each Branch Path is the object AInd
        /// </summary>
        public DataTree<AssemblyObject> AssemblyObjects
        { get; internal set; }
        /// <summary>
        /// The set of unique <see cref="AssemblyObject"/> kinds
        /// </summary>
        public AssemblyObject[] AOSet
        { get; internal set; }
        /// <summary>
        /// Search radius for collision checks
        /// </summary>
        public double CollisionRadius
        { get; internal set; }
        /// <summary>
        /// Data Tree of Heuristics used at each iteration during the assemblage
        /// </summary>
        public DataTree<string> AssemblageRules
        { get; internal set; }
        /// <summary>
        /// Data Tree of Receivers AInd indexes used in the assemblage
        /// </summary>
        public DataTree<int> ReceiverAIndexes
        { get; internal set; }
        /// <summary>
        /// Gets or Sets the check for absolute Z Direction lock.
        /// If True, only candidates whose <see cref="AssemblyObject.ReferencePlane"/>'s Z axis is parallel to the World Z axis (fixed up Direction) are selected
        /// </summary>
        public bool CheckWorldZLock
        { get; set; }


        // EXP: Supports are experimental. Yet to be implemented
        /// <summary>
        /// Gets or Sets the check for Supports
        /// if True, AssemblyObjects with <see cref="Support"/>s will be added if they pass support check
        /// </summary>
        /// <remarks><see cref="Support"/>s NOT IMPLEMENTED YET</remarks>
        /// <exclude>Exclude from documentation</exclude>
        public bool UseSupports
        { get; set; }

        #endregion properties

        #region fields

        private bool ShouldComputeReceiver;

        /// <summary>
        /// Heuristics settings - <see cref="Rule"/>s + selection criteria for Receiver and Sender suring the Assemblage
        /// </summary>
        public HeuristicsSettings HeuristicsSettings;
        /// <summary>
        /// Exogenous settings - external influences such as environment geometries and a <see cref="Field"/>
        /// </summary>
        public ExogenousSettings ExogenousSettings;

        // . . . internal fields (E_ stands for Experimental feature) - used across the .dll library
        /// <summary>
        /// Heuristics as a <see cref="Rule"/> DataTree
        /// </summary>
        internal DataTree<Rule> heuristicsTree;
        /// <summary>
        /// Index of currently used heuristics
        /// </summary>
        internal int currentHeuristicsIndex;
        /// <summary>
        /// The (Name, type) Dictionary built from the AOSet
        /// </summary>
        internal Dictionary<string, int> AOSetDictionary;
        /// <summary>
        /// RTree of AssemblyObjects centroids
        /// </summary>
        internal RTree centroidsTree;
        /// <summary>
        /// list of AssemblyObject AInd for each centroid in the RTree - obsolete?
        /// </summary>
        internal List<int> centroidsAInds;
        /// <summary>
        /// List of available objects AInd
        /// </summary>
        internal List<int> availableObjectsAInds;
        /// <summary>
        /// Stores receiver values for available objects (for faster retrieval)
        /// </summary>
        internal List<double> availableReceiverValues;
        /// <summary>
        /// List of unreachable objects AInd
        /// </summary>
        internal List<int> unreachableObjectsAInds;
        /// <summary>
        /// stores next AIndex to assign
        /// </summary>
        internal int nextAInd;

        // EXP: experimental. Yet to be implemented
        /// <summary>
        /// Experimental fields variable
        /// </summary>
        internal Sandbox E_sb;


        // . . . iteration fields (updated at each Assemblage iteration)

        /// <summary>
        /// Candidate sender objects at each iteration
        /// </summary>
        public List<AssemblyObject> i_CandidateObjects
        { get; private set; }
        /// <summary>
        /// Multiplying factors for candidates values at each iteration - affect candidates selection
        /// </summary>
        public List<double> i_CandidateFactors
        { get; private set; }
        /// <summary>
        /// stores selected sender sequential index (in candidates list) at each Assemblage iteration
        /// </summary>
        internal int i_senderSeqInd;
        /// <summary>
        /// stores selected receiver AInd at each Assemblage iteration
        /// </summary>
        internal int i_receiverAInd;
        /// <summary>
        /// stores receiver branch sequential index in <see cref="AssemblyObjects"/> Tree at each Assemblage iteration to speed up search 
        /// </summary>
        internal int i_receiverBranchSeqInd;
        /// <summary>
        /// stores <see cref="Rule"/>s pertaining the selected receiver at each Assemblage iteration
        /// </summary>
        internal List<Rule> i_receiverRules;
        /// <summary>
        /// stores indexes of filtered valid rules from <see cref="i_receiverRules"/> at each Assemblage iteration
        /// </summary>
        internal List<int> i_validRulesIndexes;
        /// <summary>
        /// Keeps arrays of neighbours AInd for each valid candidate at each Assemblage iteration
        /// passes the winning candidate array to Obstruction check (avoid RTree search twice)
        /// </summary>
        internal List<int[]> i_candidatesNeighAInd;
        /// <summary>
        /// receiver AInd at each Assemblage iteration
        /// </summary>
        public int i_CurrentReceiver { get => i_receiverAInd; }

        //internal IterationVariables iVars;

        #endregion fields

        #endregion fields-properties-delegates

        #region constructors and initializers
        /// <summary>
        /// Empty constructor
        /// </summary>
        public Assemblage()
        { }
        /// <summary>
        /// Construct an Assemblage from essential parameters
        /// </summary>
        /// <param name="AOSet">A list of unique <see cref="AssemblyObject"/>s</param>
        /// <param name="startAOs">A list of one or more <see cref="AssemblyObject"/>s to start the Assemblage from</param>
        /// <param name="HeuristicsSettings">The Heuristics Settings</param>
        /// <param name="ExogenousSettings">The Exogenous Settings</param>
        /// <param name="CheckWorldZLock">boolean flag for checking alignment with World Z-axis</param>
        public Assemblage(List<AssemblyObject> AOSet, List<AssemblyObject> startAOs, HeuristicsSettings HeuristicsSettings, ExogenousSettings ExogenousSettings, bool CheckWorldZLock = false)//, bool UseSupports = false)
        {
            if (startAOs == null || startAOs.Count == 0) return;

            // initialize AssemblyObjects variables
            AssemblyObjects = new DataTree<AssemblyObject>();
            centroidsTree = new RTree();
            centroidsAInds = new List<int>();
            availableObjectsAInds = new List<int>();
            availableReceiverValues = new List<double>();
            unreachableObjectsAInds = new List<int>();
            // initialize other variables
            //i_CandidateObjects = new List<AssemblyObject>(); // iteration variable - Update takes care of this
            AssemblageRules = new DataTree<string>();
            ReceiverAIndexes = new DataTree<int>();

            this.CheckWorldZLock = CheckWorldZLock;
            // EXP: experimental. Yet to be implemented
            this.UseSupports = false;// UseSupports;
            E_sb = new Sandbox();
            //iVars = new IterationVariables(); // transfer to AssemblageCompute class?
            //E_sandboxAvailableObjects = new List<int>();
            //E_sandboxUnreachableObjects = new List<int>();
            //rnd = new Random();

            // initialize AOSet and AOSetDictionary
            this.AOSet = AOSet.ToArray();

            // build the dictionary (needed by AssignType)
            AOSetDictionary = AssemblageUtils.BuildDictionary(this.AOSet);

            // populate the Assemblage with the starting AOs
            PopulateAssemblage(startAOs);

            // compute collision radius
            CollisionRadius = ComputeCollisionRadius(this.AOSet);

            // reset Assemblage Status (Heu-Exo settings, compute receivers, update occupancy)
            ResetAssemblageStatus(HeuristicsSettings, ExogenousSettings);
        }

        /// <summary>
        /// Construct an Assemblage from essential parameters
        /// </summary>
        /// <param name="AOSet"></param>
        /// <param name="pAO"></param>
        /// <param name="startPlane"></param>
        /// <param name="startType"></param>
        /// <param name="HeuristicsSettings"></param>
        /// <param name="ExogenousSettings"></param>
        /// <remarks>LEGACY CONSTRUCTOR</remarks>
        /// <exclude>Eclude from documentation</exclude>
        [Obsolete]
        public Assemblage(List<AssemblyObject> AOSet, List<AssemblyObject> pAO, Plane startPlane, int startType, HeuristicsSettings HeuristicsSettings, ExogenousSettings ExogenousSettings)
        {

            // initialize AssemblyObjects variables
            AssemblyObjects = new DataTree<AssemblyObject>();
            centroidsTree = new RTree();
            centroidsAInds = new List<int>();
            availableObjectsAInds = new List<int>();
            availableReceiverValues = new List<double>();
            unreachableObjectsAInds = new List<int>();
            // initialize other variables
            i_CandidateObjects = new List<AssemblyObject>();
            AssemblageRules = new DataTree<string>();
            ReceiverAIndexes = new DataTree<int>();

            // initialize AOSet and AOSetDictionary
            this.AOSet = AOSet.ToArray();
            // build the dictionary (needed by AssignType)
            AOSetDictionary = AssemblageUtils.BuildDictionary(this.AOSet);

            // compute collision radius
            CollisionRadius = ComputeCollisionRadius(this.AOSet);

            // if there is a previous AssemblyObjects list popoulate, else start with one object
            if (pAO != null && pAO.Count > 0) PopulateAssemblage(pAO);
            else StartAssemblage(startType, startPlane);
        }

        private double ComputeCollisionRadius(AssemblyObject[] AOset)
        {
            double collisionRadius = AOset[0].CollisionMesh.GetBoundingBox(false).Diagonal.Length;
            double diag;

            for (int i = 1; i < AOset.Length; i++)
            {
                diag = AOset[i].CollisionMesh.GetBoundingBox(false).Diagonal.Length;
                if (diag > collisionRadius) collisionRadius = diag;
            }

            return collisionRadius * Constants.CollisionRadiusMultiplier;
        }

        [Obsolete]
        private void StartAssemblage(int startType, Plane startPlane)
        {
            // start the assemblage with one object
            AssemblyObject startObject = AssemblyObjectUtils.Clone(AOSet[startType]);
            startObject.Transform(Transform.PlaneToPlane(startObject.ReferencePlane, startPlane));
            startObject.AInd = 0;
            startObject.ReceiverValue = 0;
            nextAInd = 1;
            AssemblyObjects.Add(startObject, new GH_Path(startObject.AInd));
            centroidsTree.Insert(AssemblyObjects[new GH_Path(startObject.AInd), 0].ReferencePlane.Origin, startObject.AInd);
            // future implementation: if object has children or multiple centroids, insert all children centroids under the same AInd (0 in this case)
            centroidsAInds.Add(startObject.AInd);
            // just for initialization (environment checks will come later)
            availableObjectsAInds.Add(startObject.AInd);
            // just for initialization (compute methods aren't defined yet)
            availableReceiverValues.Add(startObject.ReceiverValue);
        }

        private void PopulateAssemblage(List<AssemblyObject> startAOs)
        {
            // reassign type, update AOSet & AOSetDictionary
            AssemblyObjects = AssignType(startAOs);

            // add previous objects to the centroids tree and check their occupancy/availability status
            for (int i = 0; i < AssemblyObjects.BranchCount; i++)
            {
                // fill the Rules and Receiver index trees with data for the starting AOs
                AssemblageRules.Add("", AssemblyObjects.Path(i));
                ReceiverAIndexes.Add(-1, AssemblyObjects.Path(i));

                // add object to the centroids tree
                // future implementation: if object has children, insert all children centroids under the same AInd
                centroidsTree.Insert(AssemblyObjects.Branches[i][0].ReferencePlane.Origin, AssemblyObjects.Branches[i][0].AInd);
                centroidsAInds.Add(AssemblyObjects.Branches[i][0].AInd);

                // ResetAvailableObjects() needs lists of availables and unreachables
                // initialize availableObjects list
                foreach (Handle h in AssemblyObjects.Branches[i][0].Handles)
                    if (h.Occupancy == 0)
                    {
                        availableObjectsAInds.Add(AssemblyObjects.Branches[i][0].AInd);
                        availableReceiverValues.Add(0); // just for initialization (these are computed later)
                        break;
                    }
            }
        }

        #endregion

        #region setup methods
        /// <summary>
        /// Assigns types to previous AssemblyObjects, updating <see cref="AOSet"/>, <see cref="AOSetDictionary"/>, and <see cref="nextAInd"/>
        /// </summary>
        /// <param name="startingAOs">List of starting <see cref="AssemblyObject"/>s in input</param>
        /// <returns>Data Tree of type-updated AssemblyObjects, with their AInd as branch Path</returns>
        private DataTree<AssemblyObject> AssignType(List<AssemblyObject> startingAOs)
        {
            nextAInd = 0;
            // previous objects are checked against the dictionary
            // if they are not part of the existing set, a new type is added

            // checks if AssemblyObjects need to be reindexed (ex. two or more with same AInd)
            bool reIndex = false;
            int ind = startingAOs[0].AInd;
            for (int i = 1; i < startingAOs.Count; i++)
                if (startingAOs[i].AInd == ind)
                {
                    reIndex = true;
                    break;
                }

            List<AssemblyObject> newTypes = new List<AssemblyObject>();
            DataTree<AssemblyObject> typedStartingAOs = new DataTree<AssemblyObject>();
            int newTypeIndex = AOSetDictionary.Count;
            for (int i = 0; i < startingAOs.Count; i++)
            {
                // if object Name isn't already in the dictionary - new type identified
                if (!AOSetDictionary.ContainsKey(startingAOs[i].Name))
                {
                    // add new type to the dictionary
                    AOSetDictionary.Add(startingAOs[i].Name, newTypeIndex);
                    // add new type object to the dictionary candidates
                    AssemblyObject AOnewType = AssemblyObjectUtils.Clone(startingAOs[i]);
                    AOnewType.Type = newTypeIndex;
                    newTypes.Add(AOnewType);
                    newTypeIndex++;
                }
                else
                {
                    // assign type from dictionary
                    startingAOs[i].Type = AOSetDictionary[startingAOs[i].Name];
                }
                // if reIndex or AssemblyObjects do not belong to an assemblage (i.e. user-made starting input list)
                if (reIndex || startingAOs[i].AInd == -1)
                    startingAOs[i].AInd = nextAInd;
                // update next AInd
                if (startingAOs[i].AInd >= nextAInd) nextAInd = startingAOs[i].AInd + 1;
                // reassign type
                startingAOs[i].Type = AOSetDictionary[startingAOs[i].Name];
                // add a copy of the original object to the AssemblyObject Data Tree
                typedStartingAOs.Add(AssemblyObjectUtils.CloneWithConnectivity(startingAOs[i]), new GH_Path(startingAOs[i].AInd));
            }

            // update AOSet with new types
            if (newTypes.Count > 0)
            {
                List<AssemblyObject> AOsetList = AOSet.ToList();
                AOsetList.AddRange(newTypes);
                AOSet = AOsetList.ToArray();
            }

            return typedStartingAOs;
        }

        private DataTree<Rule> InitHeuristics(List<string> heuristicsString)
        {
            // rules data tree has a path of {k;rT} where k is the heuristics set and rT the receiving type
            DataTree<Rule> heuristicsTree = new DataTree<Rule>();
            for (int k = 0; k < heuristicsString.Count; k++)
            {
                //             split by list of rules (,)
                string[] rComp = heuristicsString[k].Split(new[] { ',' });

                int rT, rH, rR, sT, sH;
                double rRA;
                int w;
                for (int i = 0; i < rComp.Length; i++)
                {
                    string[] rule = rComp[i].Split(new[] { '<', '%' });
                    string[] rec = rule[0].Split(new[] { '|' });
                    string[] sen = rule[1].Split(new[] { '|' });
                    // sender and receiver component types
                    sT = AOSetDictionary[sen[0]];
                    rT = AOSetDictionary[rec[0]];
                    // sender handle index
                    sH = Convert.ToInt32(sen[1]);
                    // weight
                    w = Convert.ToInt32(rule[2]);
                    string[] rRot = rec[1].Split(new[] { '=' });
                    // receiver handle index and rotation
                    rH = Convert.ToInt32(rRot[0]);
                    rRA = Convert.ToDouble(rRot[1]);
                    // using rotations
                    rR = AOSet[rT].Handles[rH].RDictionary[rRA];

                    heuristicsTree.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, w), new GH_Path(k, rT));
                }
            }
            return heuristicsTree;
        }

        // EXP: experimental. Yet to be implemented
        /// <summary>
        /// Sets Sandbox geometry
        /// </summary>
        /// <param name="sandbox"></param>
        private void SetSandbox(Box sandbox)
        {
            if (!sandbox.IsValid)
            {
                E_sb.E_sandbox = Box.Unset;
                return;
            }

            E_sb.E_sandbox = sandbox;
            Transform scale = Transform.Scale(E_sb.E_sandbox.Center, Constants.SafeScaleMultiplier);
            E_sb.E_sandbox.Transform(scale);
            E_sb.E_sandboxAvailableObjects = new List<int>();
            E_sb.E_sandboxUnreachableObjects = new List<int>();
            ResetSandboxRtree();
        }
        #endregion

        #region update methods

        /// <summary>
        /// Update method
        /// 
        /// Update is composed by the following steps:
        /// <list type="number">
        /// <item><description>receiver selection (where do I add the next item?)</description></item>
        /// <item><description>rule selection (which one and how?)</description></item>
        /// <item><description>new object addition to the assemblage</description></item>
        /// <item><description>assemblage status update</description></item>
        /// </list>
        /// </summary>
        /// <remarks>The method is virtual, so it can be customized with an override</remarks>
        /// <example>
        /// This is the standard base implementation:
        /// <code>
        /// public override Update()
        ///{
        ///    // 0. reset iteration variables
        ///    ResetIterationVariables();
        ///    // 1. receiver selection and candidates retrieval attempt
        ///    // if there are no available candidates return
        ///    if (!TryGetReceiverAndCandidates()) return;
        ///    // 2. candidate selection
        ///    AssemblyObject newObject;
        ///    Rule rule;
        ///    (newObject, rule, _) = SelectCandidate(CandidateObjects, computeSendersValues(CandidateObjects));
        ///    // add Candidates factors to the values
        ///    AddCandidateFactors(ref candidateValues);
        ///    // 3. new object addition
        ///    AddValidObject(newObject, rule);
        ///    // 4. Assemblage status update
        ///    AssemblageUtils.ObstructionCheckAssemblage(this, newObject.AInd, i_candidatesNeighAInd[i_senderSeqInd]);
        ///}
        /// </code>
        /// </example>
        public virtual void Update()
        {
            // . . . . . . .    0. reset iteration variables

            ResetIterationVariables();

            // . . . . . . .    1. receiver selection and candidates retrieval attempt

            // if there are no available candidates return
            if (!TryGetReceiverAndCandidates()) return;


            // . . . . . . .    2. candidate selection

            AssemblyObject newObject;
            Rule rule;

            // compute Candidates values
            double[] candidateValues = HeuristicsSettings.computeSendersValues(this, i_CandidateObjects);

            // add Candidates factors to the values
            AddCandidateFactors(ref candidateValues, i_CandidateFactors);
            //for (int i = 0; i < candidateValues.Length; i++)
            //    candidateValues[i] += i_CandidateFactors[i];

            (newObject, rule, _) = SelectCandidate(i_CandidateObjects, candidateValues);

            // compute selected candidate's receiver value & iWeight (if that's the case)
            CallComputeReceiverValue(newObject);
            if (HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
                newObject.IWeight = CallComputeReceiveriWeight(newObject);

            // . . . . . . .    3. new object addition

            AddValidObject(newObject, rule);

            // . . . . . . .    4. Assemblage status update

            // check for obstructions and/or secondary handle connections
            // check if newly added object obstructs other Handles in the surroundings
            // or its Handles are obstructed in turn by other objects
            AssemblageUtils.ObstructionCheckAssemblage(this, newObject.AInd, i_candidatesNeighAInd[i_senderSeqInd]);
        }

        /// <summary>
        /// Resets iteration-dependent variables in the Assemblage
        /// </summary>
        public void ResetIterationVariables()
        {
            i_CandidateObjects = new List<AssemblyObject>();
            i_CandidateFactors = new List<double>();
            i_senderSeqInd = 0;
            i_receiverAInd = 0;
            i_receiverBranchSeqInd = 0;
            i_receiverRules = new List<Rule>();
            i_validRulesIndexes = new List<int>();
            i_candidatesNeighAInd = new List<int[]>();
        }

        private void AddCandidateFactors(ref double[] candidateValues, List<double> i_CandidateFactors)
        {
            for (int i = 0; i < candidateValues.Length; i++)
                candidateValues[i] += i_CandidateFactors[i];
        }

        /// <summary>
        /// A virtual method that tries to retrieve a valid receiver and compile related list of candidates and their factors
        /// </summary>
        /// <returns>True if a valid receiver is found, False otherwise</returns>
        /// <remarks>The method is virtual, so it can be customized with an override</remarks>
        public virtual bool TryGetReceiverAndCandidates()
        {
            // while there are available receivers...
            while (availableObjectsAInds.Count > 0)
            {
                // . . . . . . .    0. receiver selection

                // select receiver from list of available values
                // sequential receiver index in the available objects AInd list
                int availableReceiverInd = HeuristicsSettings.selectReceiver(availableReceiverValues.ToArray());

                // convert sequential index into AInd and find its related branch Path
                i_receiverAInd = availableObjectsAInds[availableReceiverInd];
                i_receiverBranchSeqInd = AssemblyObjects.Paths.IndexOf(new GH_Path(i_receiverAInd));

                // check if receiver is outside field
                if (IsReceiverOutsideField(i_receiverBranchSeqInd))
                {
                    MarkAsUnreachable(availableReceiverInd);
                    continue;
                }

                // . . . . . . .    1. candidates retrieval attempt

                if (RetrieveCandidates())
                    break;
                else
                    MarkAsUnreachable(availableReceiverInd);
            }

            return i_CandidateObjects.Count > 0;
        }
        /// <summary>
        /// Marks an object as unreachable, adding its index to <see cref="unreachableObjectsAInds"/> removing its related index from <see cref="availableObjectsAInds"/> and its related value from <see cref="availableReceiverValues"/>
        /// </summary>
        /// <param name="availableSeqIndex"></param>
        private void MarkAsUnreachable(int availableSeqIndex)
        {
            unreachableObjectsAInds.Add(availableObjectsAInds[availableSeqIndex]);
            availableObjectsAInds.RemoveAt(availableSeqIndex);
            availableReceiverValues.RemoveAt(availableSeqIndex);
        }

        /// <summary>
        /// Tests whether the currently picked receiving <see cref="AssemblyObject"/> is outside the Field
        /// </summary>
        /// <param name="availableSeqIndex"></param>
        /// <returns></returns>
        private bool IsReceiverOutsideField(int AOBranchSeqInd)
        {
            // if Field is null or Heuristics Settings are not Field dependent return false
            if (ExogenousSettings.Field == null || !HeuristicsSettings.IsFieldDependent) return false;

            Point3d origin = AssemblyObjects.Branch(AOBranchSeqInd)[0].ReferencePlane.Origin;
            int closestPointInd = ExogenousSettings.Field.GetClosestIndex(origin);
            Point3d fieldClosestPoint = ExogenousSettings.Field.GetPoints()[closestPointInd];
            double distanceSquared = origin.DistanceToSquared(fieldClosestPoint);

            return distanceSquared > ExogenousSettings.Field.MaxDistSquare;
        }

        /// <summary>
        /// This method sifts candidates filtering invalid results (i.e. collisions, environment), eventually
        /// populating lists of: <see cref="i_validRulesIndexes"/>, <see cref="i_CandidateObjects"/>, <see cref="i_candidatesNeighAInd"/>, <see cref="i_CandidateFactors"/>
        /// </summary>
        /// <returns>True if at least one suitable candidate has been found, False otherwise</returns>
        private bool RetrieveCandidates()
        {
            // . find receiver object type
            int receiverType = AssemblyObjects.Branch(i_receiverBranchSeqInd)[0].Type;

            // select current heuristics - check if heuristic mode is set to Field driven
            // in that case, use the receiver's iWeight (where the heuristics index is stored)
            if (HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
                currentHeuristicsIndex = AssemblyObjects.Branch(i_receiverBranchSeqInd)[0].IWeight;

            // . sanity check on rules
            // it is possible, when using a custom set of rules, that an object is only used as sender
            // or it is not included in the selected heuristics. In such cases, there will be no associated rules
            // in case it is picked at random as a potential receiver, so we return empties and false

            if (!heuristicsTree.PathExists(currentHeuristicsIndex, receiverType))
            {
                i_receiverRules = new List<Rule>();
                return false;
            }

            // if a path exists.....
            // . retrieve all rules for receiving object and properly define return variables
            AssemblyObject candidateObject;
            i_receiverRules = heuristicsTree.Branch(currentHeuristicsIndex, receiverType);
            int[] neighbourIndexes;

            // orient all candidates around receiving object and keep track of valid indices
            // parse through all rules and filter valid ones
            for (int i = 0; i < i_receiverRules.Count; i++)
            {
                // if receiver handle isn't free skip to next rule
                if (AssemblyObjects.Branch(i_receiverBranchSeqInd)[0].Handles[i_receiverRules[i].rH].Occupancy != 0) continue;

                if (!IsCandidateValid(i_receiverRules[i], AssemblyObjects.Branch(i_receiverBranchSeqInd)[0], out candidateObject, out neighbourIndexes)) continue;

                // if checks are passed add new objects to candidates and corresponding rule index to valid list
                i_validRulesIndexes.Add(i);
                i_CandidateObjects.Add(candidateObject);
                double candidateFactor = i_receiverRules[i].iWeight + candidateObject.Weight + candidateObject.Handles[i_receiverRules[i].sH].Weight;
                i_CandidateFactors.Add(candidateFactor);
                // neighbourIndexes are saved for Obstruction check
                i_candidatesNeighAInd.Add(neighbourIndexes);
            }

            return i_CandidateObjects.Count > 0;
        }

        private bool IsCandidateValid(Rule rule, AssemblyObject receiver, out AssemblyObject candidateObject, out int[] neighbourIndexes)
        {
            neighbourIndexes = null;

            // make a copy of corresponding sender type from catalog
            candidateObject = AssemblyObjectUtils.Clone(AOSet[rule.sT]);

            // create Transformation
            Transform orient = Transform.PlaneToPlane(AOSet[rule.sT].Handles[rule.sH].SenderPlane, receiver.Handles[rule.rH].ReceiverPlanes[rule.rR]);

            // transform candidate sender object
            candidateObject.Transform(orient);

            // verify Z lock
            // if absolute Z lock is true for the current object...
            if (CheckWorldZLock && candidateObject.WorldZLock)
                // ...perform that check too - if test is not passed return false
                if (!AssemblyObjectUtils.AbsoluteZCheck(candidateObject, Constants.RhinoAbsoluteTolerance))
                {
                    candidateObject = null;
                    return false;
                }

            // verify environment clash
            // if the object clashes with the environment return false
            if (ExogenousSettings.environmentClash(candidateObject, ExogenousSettings.EnvironmentMeshes))
            {
                candidateObject = null;
                return false;
            }

            // verify clash with existing assemblage
            // if the object clashes with surrounding objects return false
            if (AssemblageUtils.IsAOCollidingWithAssemblage(this, candidateObject, out neighbourIndexes))
            {
                candidateObject = null;
                neighbourIndexes = null;
                return false;
            }

            return true;
        }

        private AssemblyObject OrientCandidate(Rule rule, AssemblyObject receiver)
        {
            // make a copy of corresponding sender type from catalog
            AssemblyObject candidateObject = AssemblyObjectUtils.Clone(AOSet[rule.sT]);

            // create Transformation
            Transform orient = Transform.PlaneToPlane(AOSet[rule.sT].Handles[rule.sH].SenderPlane, receiver.Handles[rule.rH].ReceiverPlanes[rule.rR]);

            // transform candidate sender object
            candidateObject.Transform(orient);

            return candidateObject;
        }

        ///// <summary>
        ///// A virtual method for <see cref="Rule"/> selection - default version uses internally predefined criteria
        ///// </summary>
        ///// <param name="candidates">List of candidates <see cref="AssemblyObject"/>s</param>
        ///// <param name="sendersvalues">Array of senders values to compute the winner candidate</param>
        ///// <param name="newObject">new <see cref="AssemblyObject"/> to add to the <see cref="Assemblage"/></param>
        ///// <returns>The selected <see cref="Rule"/></returns>
        ///// <remarks>The method is virtual, so it can be customized with an override</remarks>
        ////public virtual Rule SelectRule(List<AssemblyObject> candidates, out AssemblyObject newObject)
        ////{
        ////    // sequential index of winner candidate
        ////    int winnerIndex;
        ////    double[] sendersvalues = computeSendersValues(candidates);
        ////    winnerIndex = selectSender(sendersvalues);
        ////    i_senderSeqInd = winnerIndex;
        ////    // new Object is found
        ////    newObject = candidates[winnerIndex];
        ////    // record its sender value before returning
        ////    newObject.SenderValue = sendersvalues[winnerIndex];
        ////    return i_receiverRules[i_validRulesIndexes[winnerIndex]];
        ////}
        //public virtual Rule SelectRule(List<AssemblyObject> candidates, double[] sendersvalues, out AssemblyObject newObject)
        //{
        //    // sequential index of winner candidate
        //    int winnerIndex;
        //    winnerIndex = selectSender(sendersvalues);
        //    i_senderSeqInd = winnerIndex;
        //    // new Object is found
        //    newObject = candidates[winnerIndex];
        //    // record its sender value before returning
        //    newObject.SenderValue = sendersvalues[winnerIndex];
        //    return i_receiverRules[i_validRulesIndexes[winnerIndex]];
        //}

        /// <summary>
        /// A virtual method to select a winner <see cref="AssemblyObject"/> candidate and its associated <see cref="Rule"/>
        /// </summary>
        /// <param name="candidates">List of candidates <see cref="AssemblyObject"/>s</param>
        /// <param name="sendersValues">Array of senders values to compute the winner candidate</param>
        /// <returns>A Tuple with the winning <see cref="AssemblyObject"/>, the used <see cref="Rule"/>, and its index in the candidates list</returns>
        public virtual (AssemblyObject, Rule, int) SelectCandidate(List<AssemblyObject> candidates, double[] sendersValues)
        {
            AssemblyObject winnerCandidate;
            Rule rule;

            // sequential index of winner candidate
            int winnerIndex;
            winnerIndex = HeuristicsSettings.selectSender(sendersValues);
            i_senderSeqInd = winnerIndex;

            // new Object is found
            winnerCandidate = candidates[winnerIndex];

            // record its sender value before returning
            winnerCandidate.SenderValue = sendersValues[winnerIndex];
            rule = i_receiverRules[i_validRulesIndexes[winnerIndex]];

            return (winnerCandidate, rule, winnerIndex);
        }

        /// <summary>
        /// Adds a valid <see cref="AssemblyObject"/> to the <see cref="Assemblage"/>, updating connectivity
        /// </summary>
        /// <param name="newObject"><see cref="AssemblyObject"/> to add to the Assemblage</param>
        /// <param name="rule">The <see cref="Rule"/> associated with the object</param>
        /// 
        public virtual void AddValidObject(AssemblyObject newObject, Rule rule)
        {
            // assign index
            // TODO: in future implementations, check for index uniqueness or transform in Hash
            newObject.AInd = nextAInd;
            nextAInd++;
            GH_Path newObjectPath = new GH_Path(newObject.AInd);

            // add rule to sequence as string
            AssemblageRules.Add(rule.ToString(), newObjectPath);

            // add receiver object AInd to list
            ReceiverAIndexes.Add(i_receiverAInd, newObjectPath);

            // . . . . . UPDATE HANDLES
            // update sender + receiver handle status (handle index, occupancy, neighbourObject, neighbourHandle, weight)
            HandleUtils.UpdateHandlesOnConnection(newObject, rule.sH, AssemblyObjects.Branch(i_receiverBranchSeqInd)[0], rule.rH, 1, rule.rR);

            // add centroid to assemblage centroids tree
            // future implementation: if object has children, insert all children centroids under the same AO index (assemblage.Count in this case)
            centroidsTree.Insert(newObject.ReferencePlane.Origin, newObject.AInd);
            centroidsAInds.Add(newObject.AInd);

            // add new object to available objects indexes and its receiver value to the list
            availableObjectsAInds.Add(newObject.AInd);
            availableReceiverValues.Add(newObject.ReceiverValue);

            // add new object to assemblage, its AInd to the index map
            AssemblyObjects.Add(newObject, newObjectPath);

            // if receiving object is fully occupied (all Handles either connected or occluded) remove it from the available objects
            if (AssemblyObjects.Branch(i_receiverBranchSeqInd)[0].Handles.Where(x => x.Occupancy != 0).Sum(x => 1) == AssemblyObjects.Branch(i_receiverBranchSeqInd)[0].Handles.Length)
            {
                availableReceiverValues.RemoveAt(availableObjectsAInds.IndexOf(i_receiverAInd));
                availableObjectsAInds.Remove(i_receiverAInd);
            }
        }

        #endregion

        #region call compute receivers methods

        private void CallComputeReceiverValue(AssemblyObject AO, double rValue = 0)
        {
            // compute AO receiver value (if Receiver Selection mode is not -1 or custom compute method is not null)
            if (ShouldComputeReceiver)
                AO.ReceiverValue = HeuristicsSettings.computeReceiverValue(this, AO);
            else
                AO.ReceiverValue = rValue;
        }

        /// <summary>
        /// Calls the <see cref="HeuristicsSettings.computeReceiverValue"/> delegate method for each <see cref="AssemblyObject"/> in the Assemblage
        /// </summary>
        public void CallComputeReceiversValues()
        {
            if (AssemblyObjects.BranchCount < Constants.parallelLimit)
                for (int i = 0; i < AssemblyObjects.BranchCount; i++)
                    CallComputeReceiverValue(AssemblyObjects.Branches[i][0]);
            else
            {
                Parallel.For(0, AssemblyObjects.BranchCount, i =>
                {
                    CallComputeReceiverValue(AssemblyObjects.Branches[i][0]);
                });
            }
        }

        private int CallComputeReceiveriWeight(AssemblyObject receiver) => ExogenousSettings.Field.GetClosestiWeights(receiver.ReferencePlane.Origin)[0];

        private void CallComputeReceiversiWeights()
        {
            if (AssemblyObjects.BranchCount < Constants.parallelLimit)
                for (int i = 0; i < AssemblyObjects.BranchCount; i++)
                    AssemblyObjects.Branches[i][0].IWeight = CallComputeReceiveriWeight(AssemblyObjects.Branches[i][0]);
            else
            {
                Parallel.For(0, AssemblyObjects.BranchCount, i =>
                {
                    AssemblyObjects.Branches[i][0].IWeight = CallComputeReceiveriWeight(AssemblyObjects.Branches[i][0]);
                });
            }
        }

        /// <summary>
        /// Calls the <see cref="HeuristicsSettings.computeReceiverValue"/> delegate method for each <see cref="AssemblyObject"/> in the Assemblage, as well as computing their respective iWeights if the <see cref="HeuristicsSettings.HeuristicsMode"/> is set to Field
        /// </summary>
        public void CallComputeReceiversValuesiWeights()
        {
            // compute receiver values
            CallComputeReceiversValues();
            // if heuristics is Field driven compute iWeights
            if (HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
                CallComputeReceiversiWeights();
        }

        #endregion

        #region reset methods

        /// <summary>
        /// Reset Assemblage Status - reset Heuristics and Exogenous Settings, compute all Receivers and reset AOs Occupancy Status
        /// </summary>
        /// <param name="Heu"></param>
        /// <param name="Exo"></param>
        public void ResetAssemblageStatus(HeuristicsSettings Heu, ExogenousSettings Exo)
        {
            ResetHESettings(Heu, Exo);
            CallComputeReceiversValuesiWeights();
            ResetAOsOccupancyStatus();
        }

        /// <summary>
        /// Reset Heuristics and Exogenous Settings
        /// </summary>
        /// <param name="Heu">Heuristics Setting</param>
        /// <param name="Exo">Exogenous Settings</param>
        public void ResetHESettings(HeuristicsSettings Heu, ExogenousSettings Exo)
        {
            // reset all settings, including computing and selection modes for senders and receivers
            HeuristicsSettings = Heu;
            ExogenousSettings = Exo;

            // compute AO receiver value if Receiver Selection mode is not -1 or compute method is not null
            ShouldComputeReceiver = HeuristicsSettings.ReceiverSelectionMode != -1 || HeuristicsSettings.computeReceiverValue != null;

            // reset heuristicsTree
            heuristicsTree = InitHeuristics(HeuristicsSettings.HeuSetsString);
            currentHeuristicsIndex = HeuristicsSettings.CurrentHeuristics;

            // reset environment
            //SetEnvCheckMethod();
            // EXP: experimental. Yet to be implemented
            SetSandbox(ExogenousSettings.SandBox);
        }

        // EXP: experimental. Yet to be implemented
        private void ResetSandboxRtree()
        {
            E_sb.E_sandboxCentroidsTree = new RTree();
            E_sb.E_sandboxCentroidsAO = new List<int>();
            // create List of centroid correspondance with their AO
            centroidsTree.Search(E_sb.E_sandbox.BoundingBox, (sender, args) =>
            {
                // recover the AssemblyObject centroid related to the found centroid
                // args.Id contains the AInd
                E_sb.E_sandboxCentroidsTree.Insert(AssemblyObjects[new GH_Path(args.Id), 0].ReferencePlane.Origin, args.Id);
                E_sb.E_sandboxCentroidsAO.Add(args.Id);
            });
        }

        /// <summary>
        /// Verify list of available/unreachable <see cref="AssemblyObject"/>s according to current environment and heuristics
        /// </summary>
        public void ResetAOsOccupancyStatus()
        {
            // . . . . . . .    1. move all AInds to available AInd list and clear unreachable AInd list

            ResetAvailableUnreachableObjects();

            // . . . . . . .    2. scan available objects

            ScanAvailableObjects();

            // . . . . . . .    3. [OPTIONAL] add to specific Sandbox lists if a valid Sandbox is present
            // EXP: experimental. Yet to be implemented
            //if (E_sb.E_sandbox.Volume > 0)
            //{
            //    E_sb.E_sandboxAvailableObjects.Clear();
            //    E_sb.E_sandboxUnreachableObjects.Clear();
            //    foreach (int avOb in availableObjectsAInds)
            //        if (E_sb.E_sandbox.Contains(AssemblyObjects[new GH_Path(avOb), 0].ReferencePlane.Origin)) E_sb.E_sandboxAvailableObjects.Add(avOb);
            //    //if (E_sandboxCentroidsAO.Contains(avOb)) E_sandboxAvailableObjects.Add(avOb);
            //    foreach (int unrOb in unreachableObjectsAInds)
            //        if (E_sb.E_sandbox.Contains(AssemblyObjects[new GH_Path(unrOb), 0].ReferencePlane.Origin)) E_sb.E_sandboxAvailableObjects.Add(unrOb);
            //    //if (E_sandboxCentroidsAO.Contains(unrOb)) E_sandboxUnreachableObjects.Add(unrOb);
            //}

        }

        private void ResetAvailableUnreachableObjects()
        {
            // move all unreachable indexes to available and clear unrechable list 
            foreach (int unreachAInd in unreachableObjectsAInds)
                if (!availableObjectsAInds.Contains(unreachAInd)) availableObjectsAInds.Add(unreachAInd);

            unreachableObjectsAInds.Clear();

            // clear availableReceiverValues list and set updated values
            availableReceiverValues.Clear();

            PopulateAvailableReceiverValues();
        }

        private void PopulateAvailableReceiverValues()
        {
            for (int i = 0; i < availableObjectsAInds.Count; i++)
                availableReceiverValues.Add(AssemblyObjects[new GH_Path(availableObjectsAInds[i]), 0].ReceiverValue);
        }

        private void ScanAvailableObjects()
        {
            // check for every available object and move from available to unreachable if:
            // 2.1. they do not pass environmental geometry check
            // 2.2. they do not pass rules checks:
            //  2.2.1. there aren't rules with their rType
            //  2.2.2. rules exists for their rType but no match for any of its free Handles
            //  2.2.3. rules exist for a free Handle but sender placement is precluded by other objects (AOs or environmental)

            for (int i = availableObjectsAInds.Count - 1; i >= 0; i--)
            {
                AssemblyObject avObject = AssemblyObjects[new GH_Path(availableObjectsAInds[i]), 0];

                // . . . . 2.1. environmental geometry check

                if (ExogenousSettings.environmentClash(avObject, ExogenousSettings.EnvironmentMeshes))
                {
                    MarkAsUnreachable(i);
                    continue;
                }

                // . . . . 2.2. rules check

                // check if current heuristics is fixed or Field-dependent
                if (HeuristicsSettings.HeuristicsMode == HeuristicModes.Field)
                    currentHeuristicsIndex = avObject.IWeight; // when starting an Assemblage this is still -1!!!
                // current heuristics tree path to search for {current heuristics; receiver type}
                GH_Path rulesPath = new GH_Path(currentHeuristicsIndex, avObject.Type);

                // . . . . . 2.2.1. check if there aren't rules with AO rType

                // if object type cannot be a receiver (it hasn't a path in the heuristics tree)
                // assign as unreachable and continue to the next available AO
                if (!heuristicsTree.PathExists(rulesPath))
                {
                    MarkAsUnreachable(i);
                    continue;
                }

                // . . . . . 2.2.2. rules exists for AO rType but no match for any of its free Handles
                // . . . . . 2.2.3. rules exist for a free Handle but sender placement is precluded by other objects (AOs or environmental)

                // test if unreachable after looping through all Handles
                bool unreachable = CheckHandlesAndRules(avObject, rulesPath);
                if (unreachable) MarkAsUnreachable(i);

            }
        }

        private bool CheckHandlesAndRules(AssemblyObject avObject, GH_Path rulesPath)
        {
            bool unreachable = true;
            // scan AO Handles against current heuristics rules
            for (int j = 0; j < avObject.Handles.Length; j++)
            {
                // continue if handle is not available
                if (avObject.Handles[j].Occupancy != 0) continue;

                foreach (Rule rule in heuristicsTree.Branch(rulesPath))
                    // Check if a Rule can be succesfully applied
                    // if there is at least a free handle with an available rule for it (rH is the receiving handle index)
                    // AND its related candidate fits without clashing (using discards for out variables)
                    if (rule.rH == j && IsCandidateValid(rule, avObject, out _, out _))
                    {
                        // activate flag and break loop
                        unreachable = false;
                        break;
                    }
                if (!unreachable) break;
            }
            return unreachable;
        }
        #endregion

        #region extract methods

        /// <summary>
        /// Extract available objects indices
        /// </summary>
        /// <returns>An array of AInd of available objects in the Assemblage</returns>
        public GH_Integer[] ExtractAvailableObjectsIndexes()
        {
            GH_Integer[] outIndexes = new GH_Integer[availableObjectsAInds.Count];

            if (availableObjectsAInds.Count < Constants.parallelLimit)
                for (int i = 0; i < availableObjectsAInds.Count; i++)
                    outIndexes[i] = new GH_Integer(availableObjectsAInds[i]);
            else
                Parallel.For(0, availableObjectsAInds.Count, i =>
                {
                    outIndexes[i] = new GH_Integer(availableObjectsAInds[i]);
                });
            return outIndexes;
        }

        /// <summary>
        /// Extract unreachable objects indices
        /// </summary>
        /// <returns>An array of AInd of unreachable objects in the Assemblage</returns>
        public GH_Integer[] ExtractUnreachableObjectsIndexes()
        {
            GH_Integer[] outIndexes = new GH_Integer[unreachableObjectsAInds.Count];

            if (unreachableObjectsAInds.Count < Constants.parallelLimit)
                for (int i = 0; i < unreachableObjectsAInds.Count; i++)
                    outIndexes[i] = new GH_Integer(unreachableObjectsAInds[i]);
            else
                Parallel.For(0, unreachableObjectsAInds.Count, i =>
                {
                    outIndexes[i] = new GH_Integer(unreachableObjectsAInds[i]);
                });
            return outIndexes;
        }

        /// <summary>
        /// Extract receivers' <see cref="AssemblyObject.AInd"/>
        /// </summary>
        /// <returns>A Data Tree of receivers' <see cref="AssemblyObject.AInd"/> - one for each <see cref="AssemblyObject"/> in the Assemblage</returns>
        /// <remarks>Returns a -1 index for starting AssemblyObjects</remarks>
        public DataTree<GH_Integer> ExtractReceiverAIndexes()
        {
            DataTree<GH_Integer> outAIndexes = new DataTree<GH_Integer>();

            for (int i = 0; i < ReceiverAIndexes.BranchCount; i++)
                for (int j = 0; j < ReceiverAIndexes.Branches[i].Count; j++)
                    outAIndexes.Add(new GH_Integer(ReceiverAIndexes.Branches[i][j]), ReceiverAIndexes.Paths[i]);

            return outAIndexes;
        }

        /// <summary>
        /// Extract used <see cref="Rule"/>s
        /// </summary>
        /// <returns>A Data Tree of the <see cref="Rule"/>s used for each <see cref="AssemblyObject"/> in the Assemblage</returns>
        /// <remarks>Returns an empty string for starting AssemblyObjects</remarks>
        public DataTree<GH_String> ExtractRules()
        {
            DataTree<GH_String> outRules = new DataTree<GH_String>();

            for (int i = 0; i < AssemblageRules.BranchCount; i++)
                for (int j = 0; j < AssemblageRules.Branches[i].Count; j++)
                    outRules.Add(new GH_String(AssemblageRules.Branches[i][j]), AssemblageRules.Paths[i]);

            return outRules;
        }

        /// <summary>
        /// Extract <see cref="Rule"/>s associated with each candidate
        /// </summary>
        /// <returns>The <see cref="Rule"/>s associated with each candidate</returns>
        public List<Rule> ExtractCandidatesRules()
        {
            List<Rule> rules = new List<Rule>();
            for (int i = 0; i < i_validRulesIndexes.Count; i++)
                rules.Add(i_receiverRules[i_validRulesIndexes[i]]);

            return rules;
        }

        #endregion

        #region debug methods

        #endregion

        /// <summary>
        /// Returns a string that represents the <see cref="Assemblage"/>
        /// </summary>
        /// <returns>A string that represents the <see cref="Assemblage"/></returns>
        public override string ToString()
        {
            return string.Format("Assemblage containing {0} AssemblyObject(s) of {1} different kind(s)", AssemblyObjects.DataCount, AOSet.Length);
        }
    }
}
