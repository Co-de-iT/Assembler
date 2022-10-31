using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
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
        /// The sequential DataTree of <see cref="AssemblyObject"/>s in the Assemblage, each Branch Path is the object AInd
        /// </summary>
        public DataTree<AssemblyObject> AssemblyObjects
        { get; internal set; }
        /// <summary>
        /// The set of unique <see cref="AssemblyObject"/> kinds
        /// </summary>
        public AssemblyObject[] AOSet
        { get; internal set; }
        /// <summary>
        /// Heuristics settings (rules + selection criteria)
        /// </summary>
        public HeuristicsSettings HeuristicsSettings
        { get; internal set; }
        /// <summary>
        /// Exogenous settings (external influences)
        /// </summary>
        public ExogenousSettings ExogenousSettings
        { get; internal set; }
        /// <summary>
        /// Collision radius for collision checks with neighbour AssemblyObjects
        /// based on the largest object in AOSet (largest Bounding Box diagonal)
        /// </summary>
        public double CollisionRadius
        { get; internal set; }
        //public int RulesCount { get => heuristicsTree.BranchCount; }
        /// <summary>
        /// Candidate sender objects at each iteration
        /// </summary>
        public List<AssemblyObject> CandidateObjects
        { get; private set; }
        /// <summary>
        /// Data Tree of Heuristics used during the assemblage
        /// </summary>
        public DataTree<string> AssemblageRules
        { get; internal set; }
        /// <summary>
        /// Data Tree of Receivers AInd indexes used in the assemblage
        /// </summary>
        public DataTree<int> ReceiverAIndexes
        { get; internal set; }
        /// <summary>
        /// Gets or Sets the check for absolute Z direction lock.
        /// if True, only candidates whose <see cref="AssemblyObject.referencePlane"/> Z axis is parallel to the World Z axis (fixed up direction) are selected
        /// </summary>
        public bool CheckWorldZLock
        { get; set; }
        /// <summary>
        /// Gets or Sets the check for Supports
        /// if True, AssemblyObjects with <see cref="Support"/>s will be added if they pass support check
        /// </summary>
        /// <remarks><see cref="Support"/>s NOT IMPLEMENTED YET</remarks>
        public bool UseSupports
        { get; set; }

        #endregion properties

        #region delegates
        // . . . delegates for sender-receiver computation and selection

        /// <summary>
        /// Delegate type for environment container behavior (collision or inclusion)
        /// </summary>
        /// <param name="sO"></param>
        /// <returns>True if an <see cref="AssemblyObject"/> is invalid (clash detected or other invalidating condition), False if valid</returns>
        public delegate bool EnvironmentCheckMethod(AssemblyObject sO);
        /// <summary>
        /// Delegate variable for environment check mode
        /// </summary>
        public EnvironmentCheckMethod environmentCheck;
        /// <summary>
        /// Delegate type for computing candidates (sender) values
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="receiverIndex"></param>
        /// <returns>array of values associated with the candidates</returns>
        public delegate T[] ComputeCandidatesValuesMethod<T>(List<AssemblyObject> candidates);
        /// <summary>
        /// Delegate variable for computing sender values
        /// </summary>
        public ComputeCandidatesValuesMethod<double> computeSendersValues;
        /// <summary>
        /// Delegate type for computing a single receiver value
        /// </summary>
        /// <param name="receiver"></param>
        /// <returns>value computed for the receiver object</returns>
        public delegate T ComputeReceiverMethod<T>(AssemblyObject receiver);
        /// <summary>
        /// Delegate variable for computing receiver value
        /// </summary>
        public ComputeReceiverMethod<double> computeReceiverValue;
        /// <summary>
        /// Delegate method for choosing winner index from sender values
        /// </summary>
        /// <param name="values">a collection of values, as array or list</param>
        /// <returns>index of winner candidate</returns>
        public delegate int SelectWinnerMethod<T>(T[] values);
        /// <summary>
        /// Delegate variable for selecting a sender from candidates (based on their values)
        /// </summary>
        public SelectWinnerMethod<double> selectSender;
        /// <summary>
        /// Delegate variable for selecting a receiver from available ones (based on their values)
        /// </summary>
        public SelectWinnerMethod<double> selectReceiver;

        #endregion delegates

        // . . . internal fields (E_ stands for Experimental feature) - used across the .dll library

        /// <summary>
        /// Heuristics as a <see cref="Rule"/> DataTree
        /// </summary>
        internal DataTree<Rule> heuristicsTree;
        /// <summary>
        /// Index of currently used heuristics
        /// </summary>
        internal int currentHeuristics;
        /// <summary>
        /// The (name, type) Dictionary built from the AOSet
        /// </summary>
        internal Dictionary<string, int> AOSetDictionary;
        /// <summary>
        /// RTree of AssemblyObjects centroids
        /// </summary>
        internal RTree centroidsTree;
        /// <summary>
        /// list of AssemblyObject AInd for each centroid in the RTree - obsolete?
        /// </summary>
        internal List<int> centroidsAO;
        /// <summary>
        /// List of available objects AInd
        /// </summary>
        internal List<int> availableObjects;
        /// <summary>
        /// Stores receiver values for available objects (for faster retrieval)
        /// </summary>
        internal List<double> availableReceiverValues;
        /// <summary>
        /// List of unreachable objects AInd
        /// </summary>
        internal List<int> unreachableObjects;

        /// <summary>
        /// Sandbox for focused assemblage growth - EXPERIMENTAL - NOT IMPLEMENTED YET
        /// </summary>
        internal Box E_sandbox;
        internal RTree E_sandboxCentroidsTree;
        internal List<int> E_sandboxCentroidsAO; // list of sandbox centroid/AssemblyObject correspondances
        internal List<int> E_sandboxAvailableObjects;
        internal List<int> E_sandboxUnreachableObjects;

        //  . . . private fields

        /// <summary>
        /// stores next AIndex to assign
        /// </summary>
        private int nextAInd;
        private int E_sequentialRuleIndex; // progressive index for rule selection in sequential mode
        private readonly Random rnd;

        // . . . iteration fields (updated at each Assemblage iteration)

        /// <summary>
        /// stores selected sender sequential index (for candidates list) at each Assemblage iteration
        /// </summary>
        private int i_senderSeqInd;
        /// <summary>
        /// stores selected receiver AInd at each Assemblage iteration
        /// </summary>
        private int i_receiverAInd;
        /// <summary>
        /// stores receiver branch sequential index in <see cref="AssemblyObjects"/> at each Assemblage iteration to speed up search 
        /// </summary>
        private int i_receiverSeqBranchInd;
        /// <summary>
        /// stores <see cref="Rule"/>s pertaining the selected receiver at each Assemblage iteration
        /// </summary>
        private List<Rule> i_receiverRules;
        /// <summary>
        /// stores indexes of filtered valid rules from <see cref="i_receiverRules"/> at each Assemblage iteration
        /// </summary>
        private List<int> i_validRulesIndexes;
        /// <summary>
        /// Keeps arrays of neighbours AInd for each valid candidate at each Assemblage iteration
        /// passes the winning candidate array to Obstruction check (avoid RTree search twice)
        /// </summary>
        private List<int[]> i_candidatesNeighAInd;

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
        /// <param name="AOSet"></param>
        /// <param name="pAO"></param>
        /// <param name="startPlane"></param>
        /// <param name="startType"></param>
        /// <param name="HeuristicsSettings"></param>
        /// <param name="ExogenousSettings"></param>
        public Assemblage(List<AssemblyObject> AOSet, List<AssemblyObject> pAO, Plane startPlane, int startType, HeuristicsSettings HeuristicsSettings, ExogenousSettings ExogenousSettings)
        {

            // initialize AssemblyObjects variables
            AssemblyObjects = new DataTree<AssemblyObject>();
            centroidsTree = new RTree();
            centroidsAO = new List<int>();
            availableObjects = new List<int>();
            availableReceiverValues = new List<double>();
            unreachableObjects = new List<int>();
            // initialize other variables
            CandidateObjects = new List<AssemblyObject>();
            AssemblageRules = new DataTree<string>();
            ReceiverAIndexes = new DataTree<int>();
            E_sequentialRuleIndex = 0; // progressive index for rule selection in sequential mode (EXPERIMENTAL)
            rnd = new Random();

            // initialize AOSet and AOSetDictionary
            this.AOSet = AOSet.ToArray();
            // build the dictionary (needed by AssignType)
            AOSetDictionary = Utilities.BuildDictionary(this.AOSet);

            // if there is a previous AssemblyObjects list popoulate, else start with one object
            if (pAO != null && pAO.Count > 0) PopulateAssemblage(pAO);
            else StartAssemblage(startType, startPlane);

            // compute collision radius
            CollisionRadius = ComputeCollisionRadius(this.AOSet);
        }

        private double ComputeCollisionRadius(AssemblyObject[] AOset)
        {
            double cR = 0;
            double diag;

            for (int i = 0; i < AOset.Length; i++)
            {
                diag = AOset[i].collisionMesh.GetBoundingBox(false).Diagonal.Length;
                if (diag > cR) cR = diag;
            }

            return cR * 2.5;
        }

        private void StartAssemblage(int startType, Plane startPlane)
        {
            // start the assemblage with one object
            AssemblyObject startObject = Utilities.Clone(AOSet[startType]);
            startObject.Transform(Transform.PlaneToPlane(startObject.referencePlane, startPlane));
            startObject.AInd = 0;
            startObject.receiverValue = 0;
            nextAInd = 1;
            AssemblyObjects.Add(startObject, new GH_Path(startObject.AInd));
            centroidsTree.Insert(AssemblyObjects[new GH_Path(startObject.AInd), 0].referencePlane.Origin, startObject.AInd);
            // future implementation: if object has children or multiple centroids, insert all children centroids under the same AInd (0 in this case)
            centroidsAO.Add(startObject.AInd);
            // just for initialization (environment checks will come later)
            availableObjects.Add(startObject.AInd);
            // just for initialization (compute methods haven't been defined yet)
            availableReceiverValues.Add(startObject.receiverValue);
        }

        private void PopulateAssemblage(List<AssemblyObject> pAO)
        {
            // reassign type, update AOSet & AOSetDictionary
            AssemblyObjects = AssignType(pAO);

            // add previous objects to the centroids tree and check their occupancy/availability status
            for (int i = 0; i < AssemblyObjects.BranchCount; i++)
            {
                // add object to the centroids tree
                // future implementation: if object has children, insert all children centroids under the same AInd
                centroidsTree.Insert(AssemblyObjects.Branches[i][0].referencePlane.Origin, AssemblyObjects.Branches[i][0].AInd);
                centroidsAO.Add(AssemblyObjects.Branches[i][0].AInd);

                // ResetAvailableObjects() needs lists of availables and unreachables
                // initialize availableObjects list
                foreach (Handle h in AssemblyObjects.Branches[i][0].handles)
                    if (h.occupancy == 0)
                    {
                        availableObjects.Add(AssemblyObjects.Branches[i][0].AInd);
                        availableReceiverValues.Add(0); // just for initialization (these are computed later)
                        break;
                    }
            }
        }

        #endregion

        #region setup methods
        /// <summary>
        /// Assigns types to previous AssemblyObjects, updating <see cref="AOSet"/> and <see cref="AOSetDictionary"/>
        /// </summary>
        /// <param name="pAO">List of previous Objects in input</param>
        /// <returns>Data Tree of type-updated AssemblyObjects, with their AInd as branch Path</returns>
        private DataTree<AssemblyObject> AssignType(List<AssemblyObject> pAO)
        {
            nextAInd = 0;
            // previous objects are checked against the dictionary
            // if they are not part of the existing set, a new type is added

            // checks if AssemblyObjects need to be reindexed (ex. two or more with same AInd)
            bool reIndex = false;
            int ind = pAO[0].AInd;
            for (int i = 1; i < pAO.Count; i++)
                if (pAO[i].AInd == ind)
                {
                    reIndex = true;
                    break;
                }

            List<AssemblyObject> newTypes = new List<AssemblyObject>();
            DataTree<AssemblyObject> pAOTyped = new DataTree<AssemblyObject>();
            int newTypeIndex = AOSetDictionary.Count;
            for (int i = 0; i < pAO.Count; i++)
            {
                // if object name isn't already in the dictionary - new type identified
                if (!AOSetDictionary.ContainsKey(pAO[i].name))
                {
                    // add new type to the dictionary
                    AOSetDictionary.Add(pAO[i].name, newTypeIndex);
                    // add new type object to the dictionary candidates
                    AssemblyObject AOnewType = Utilities.Clone(pAO[i]);
                    AOnewType.type = newTypeIndex;
                    newTypes.Add(AOnewType);
                    newTypeIndex++;
                }
                // if reIndex or AssemblyObjects do not belong to an assemblage (i.e. user-made starting input list)
                if (reIndex || pAO[i].AInd == -1)
                    pAO[i].AInd = nextAInd;
                // update next AInd
                if (pAO[i].AInd >= nextAInd) nextAInd = pAO[i].AInd + 1;
                // reassign type
                pAO[i].type = AOSetDictionary[pAO[i].name];
                // add a copy of the original object to the AssemblyObject Data Tree
                pAOTyped.Add(Utilities.CloneWithConnectivity(pAO[i]), new GH_Path(pAO[i].AInd));
            }

            // update AOSet with new types
            if (newTypes.Count > 0)
            {
                List<AssemblyObject> AOsetList = AOSet.ToList();
                AOsetList.AddRange(newTypes);
                AOSet = AOsetList.ToArray();
            }

            return pAOTyped;
        }

        private DataTree<Rule> InitHeuristics(List<string> heu)
        {
            // rules data tree has a path of {k;rT} where k is the heuristics set and rT the receiving type
            DataTree<Rule> heuT = new DataTree<Rule>();
            for (int k = 0; k < heu.Count; k++)
            {
                //             split by list of rules (,)
                string[] rComp = heu[k].Split(new[] { ',' });

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
                    rR = AOSet[rT].handles[rH].rDictionary[rRA];

                    heuT.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, w), new GH_Path(k, rT));
                }
            }
            return heuT;
        }

        /// <summary>
        /// Sets Sandbox geometry
        /// </summary>
        /// <param name="sandbox"></param>
        private void SetSandbox(Box sandbox)
        {
            if (sandbox.IsValid)
            {
                E_sandbox = sandbox;
                Transform scale = Transform.Scale(E_sandbox.Center, 1.2);
                E_sandbox.Transform(scale);
                E_sandboxAvailableObjects = new List<int>();
                E_sandboxUnreachableObjects = new List<int>();
                ResetSandboxRtree();
            }
            else
            {
                E_sandbox = Box.Empty;
            }
        }

        /// <summary>
        /// Sets Environment Check Method to use
        /// </summary>
        private void SetEnvCheckMethod()
        {
            switch (ExogenousSettings.environmentMode)
            {
                case -1:
                    // custom mode - method assigned in scripted component
                    break;
                case 0:
                    environmentCheck = (AssemblyObject sO) => { return false; };
                    break;
                case 1:
                    environmentCheck = EnvClashCollision;
                    break;
                case 2:
                    environmentCheck = EnvClashInclusion;
                    break;
                default:
                    environmentCheck = EnvClashInclusion;
                    break;
            }

        }

        /// <summary>
        /// sets appropriate delegates for computing receiver values and selection according to the chosen criteria
        /// </summary>
        /// <param name="receiverSelectionMode"></param>
        private void SetReceiverSelectionMode(int receiverSelectionMode)
        {
            // set receiver compute and selection delegates
            switch (receiverSelectionMode)
            {
                case -1:
                    // custom mode - methods assigned in scripted component
                    break;
                case 0:
                    // random selection among available objects
                    computeReceiverValue = ComputeRZero;
                    selectReceiver = SelectRandomIndex;
                    break;
                case 1:
                    // scalar field search - closest Field point
                    computeReceiverValue = ComputeRScalarField;
                    selectReceiver = SelectMinIndex;
                    break;
                case 2:
                    // scalar field search - interpolated values
                    computeReceiverValue = ComputeRScalarFieldInterpolated;
                    selectReceiver = SelectMinIndex;
                    break;
                case 3:
                    // minimum sum weight around candidate
                    computeReceiverValue = ComputeRWeightDensity;
                    selectReceiver = SelectMaxIndex;
                    break;

                // add more criteria here (must return an avInd)
                // density driven
                // component weight driven
                // ....

                case 99:
                    // "sequential" mode - return last available object in the list
                    computeReceiverValue = (ao) => 0; // anonymous function that always returns 0
                    selectReceiver = (a) => { return availableObjects.Count - 1; }; // anonymous function that returns last available object AInd
                    break;

                default: goto case 0;
            }
        }


        /// <summary>
        /// sets appropriate delegates for computing sender candidates values and selection according to the chosen criteria
        /// </summary>
        /// <param name="ruleSelectionMode"></param>
        private void SetSenderSelectionMode(int ruleSelectionMode)
        {
            // set sender candidates (rules) compute and selection delegates
            switch (ruleSelectionMode)
            {
                case -1:
                    // custom mode - methods assigned in scripted component
                    break;
                case 0:
                    // random selection - chooses one candidate at random
                    computeSendersValues = ComputeZero;
                    selectSender = SelectRandomIndex;
                    break;
                case 1:
                    // scalar field nearest with threshold - chooses candidate whose centroid closest scalar field value is closer to the threshold
                    computeSendersValues = ComputeScalarField;
                    selectSender = SelectMinIndex;
                    break;
                case 2:
                    // scalar field interpolated with threshold - chooses candidate whose centroid interpolated scalar field value is closer to the threshold
                    computeSendersValues = ComputeScalarFieldInterpolated;
                    selectSender = SelectMinIndex;
                    break;
                case 3:
                    // vector field nearest - chooses candidate whose direction has minimum angle with closest vector field point
                    computeSendersValues = ComputeVectorField;
                    selectSender = SelectMinIndex;
                    break;
                case 4:
                    // vector field interpolated - chooses candidate whose direction has minimum angle with interpolated vector field point
                    computeSendersValues = ComputeVectorFieldInterpolated;
                    selectSender = SelectMinIndex;
                    break;
                case 5:
                    // vector field bidirectional nearest - chooses candidate whose direction has minimum angle with closest vector field point (bidirectional)
                    computeSendersValues = ComputeVectorFieldBidirectional;
                    selectSender = SelectMinIndex;
                    break;
                case 6:
                    // vector field bidirectional interpolated - chooses candidate whose direction has minimum angle with interpolated vector field point (bidirectional)
                    computeSendersValues = ComputeVectorFieldBidirectionalInterpolated;
                    selectSender = SelectMinIndex;
                    break;
                case 7:
                    // density search 1 - chooses candidate with minimal bounding box volume with receiver
                    computeSendersValues = ComputeBBVolume;
                    selectSender = SelectMinIndex;
                    break;
                case 8:
                    // density search 2 - chooses candidate with minimal bounding box diagonal with receiver
                    computeSendersValues = ComputeBBDiagonal;
                    selectSender = SelectMinIndex;
                    break;
                case 9:
                    // Weighted Random Choice among valid rules
                    computeSendersValues = ComputeWRC;
                    selectSender = SelectWRCIndex;
                    break;
                // . add more criteria here
                // ...
                //
                case 99:
                    // sequential Rule - tries to apply the heuristics set rules in sequence (buggy)
                    // anonymous function - the computation is not necessary
                    computeSendersValues = (candidates) => { return candidates.Select(ri => 0.0).ToArray(); };
                    selectSender = SelectNextRuleIndex;
                    break;

                default: goto case 0;
            }
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
        ///    AssemblyObject newObject;
        ///    // 1. receiver selection and candidates retrieval attempt
        ///    // if there are no available candidates return
        ///    if (!TryGetReceiverAndCandidates()) return;
        ///    // 2. rule selection
        ///    Rule rule = SelectRule(CandidateObjects, out newObject);
        ///    // 3. new object addition
        ///    AddValidObject(newObject, rule);
        ///    // 4. Assemblage status update
        ///    Utilities.ObstructionCheckAssemblage(this, newObject.AInd, i_candidatesNeighAInd[i_senderSeqInd]);
        ///}
        /// </code>
        /// </example>
        public virtual void Update()
        {
            // . . . . . . .    0. reset iteration variables

            ResetIterationVariables();

            AssemblyObject newObject;

            // . . . . . . .    1. receiver selection and candidates retrieval attempt

            // if there are no available candidates return
            if (!TryGetReceiverAndCandidates()) return;


            // . . . . . . .    2. rule selection

            Rule rule = SelectRule(CandidateObjects, out newObject);


            // . . . . . . .    3. new object addition

            AddValidObject(newObject, rule);


            // . . . . . . .    4. Assemblage status update

            // check for obstructions and/or secondary handle connections
            // check if newly added object obstructs other handles in the surroundings
            // or its handles are obstructed in turn by other objects
            Utilities.ObstructionCheckAssemblage(this, newObject.AInd, i_candidatesNeighAInd[i_senderSeqInd]);
        }

        /// <summary>
        /// Resets iteration-dependent variables in the Assemblage
        /// </summary>
        public void ResetIterationVariables()
        {
            i_senderSeqInd = 0;
            i_receiverAInd = 0;
            i_receiverSeqBranchInd = 0;
            i_receiverRules = new List<Rule>();
            i_validRulesIndexes = new List<int>();
            i_candidatesNeighAInd = new List<int[]>();
        }

        /// <summary>
        /// A virtual method that tries to retrieve a valid receiver and compile related list of candidates
        /// </summary>
        /// <returns>True if a valid receiver is found, False otherwise</returns>
        /// <remarks>The method is virtual, so it can be customized with an override</remarks>
        public virtual bool TryGetReceiverAndCandidates()
        {
            while (availableObjects.Count > 0)
            {
                // . . . . . . .    1. receiver selection

                // select receiver from list of available values
                // sequential receiver index in the available objects AInd list
                int availableSeqIndex = selectReceiver(availableReceiverValues.ToArray());

                // convert sequential index into AInd and find its related branch Path
                i_receiverAInd = availableObjects[availableSeqIndex];
                i_receiverSeqBranchInd = AssemblyObjects.Paths.IndexOf(new GH_Path(i_receiverAInd));

                // check if the related AssemblyObject is outside the Field
                if (IsReceiverOutsideField())
                {
                    MarkAsUnreachable(availableSeqIndex);
                    continue;
                }

                // . . . . . . .    1.1 candidates retrieval attempt

                // this function sifts candidates filtering invalid results (i.e. collisions, environment)
                // compiles list of: validRulesIndexes, candidateObjects, candidatesNeighAInd

                if (RetrieveCandidates())
                    break;
                else
                {
                    // bookkeeping
                    // if an object cannot receive candidates, it is marked as unreachable (neither fully occupied nor occluded, yet nothing can be added to it)
                    MarkAsUnreachable(availableSeqIndex);
                }
            }

            return CandidateObjects.Count > 0;
        }

        private void MarkAsUnreachable(int availableSeqIndex)
        {
            unreachableObjects.Add(i_receiverAInd);
            availableObjects.RemoveAt(availableSeqIndex);
            availableReceiverValues.RemoveAt(availableSeqIndex);
        }

        /// <summary>
        /// Tests whether the currently picked receiving <see cref="AssemblyObject"/> is outside the Field
        /// </summary>
        /// <param name="availableSeqIndex"></param>
        /// <returns></returns>
        private bool IsReceiverOutsideField()
        {
            // if Field is null and receiver Selection mode is not 1 or 2 return False
            if (ExogenousSettings.field == null || HeuristicsSettings.receiverSelectionMode < 1 || HeuristicsSettings.receiverSelectionMode > 2)
                return false;

            Point3d origin = AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].referencePlane.Origin;
            int closestPointInd = ExogenousSettings.field.GetClosestIndex(origin);
            Point3d fieldClosestPoint = ExogenousSettings.field.GetPoints()[closestPointInd];
            double distanceSquared = origin.DistanceToSquared(fieldClosestPoint);

            return distanceSquared > ExogenousSettings.field.MaxDistSquare;
        }

        /// <summary>
        /// Retrieve the <see cref="CandidateObjects"/> for the current iteration
        /// </summary>
        /// <returns>True if at least one suitable candidate has been found, False otherwise</returns>
        /// <remarks>This function sifts candidates filtering invalid results (i.e. collisions, environment), eventually
        /// compiling lists of: i_validRulesIndexes, <see cref="CandidateObjects"/>, i_candidatesNeighAInd </remarks>
        public bool RetrieveCandidates()
        {
            // . find receiver object type
            int receiverType = AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].type;
            CandidateObjects = new List<AssemblyObject>();
            i_candidatesNeighAInd.Clear();
            i_validRulesIndexes = new List<int>();

            // select current heuristics - check if heuristic mode is set to field driven
            // in that case, use the receiver's iWeight (where the heuristics index is stored)
            if (HeuristicsSettings.heuristicsMode == 1)
                currentHeuristics = AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].iWeight;

            // . sanity check on rules
            // it is possible, when using a custom set of rules, that an object is only used as sender
            // or it is not included in the selected heuristics. In such cases, there will be no associated rules
            // in case it is picked at random as a potential receiver, so we return empties and false

            if (!heuristicsTree.PathExists(currentHeuristics, receiverType))
            {
                i_receiverRules = new List<Rule>();
                return false;
            }

            // if a path exists.....
            // . retrieve all rules for receiving object and properly define return variables
            AssemblyObject newObject;
            i_receiverRules = heuristicsTree.Branch(currentHeuristics, receiverType);
            int[] neighbourIndexes;
            // orient all candidates around receiving object and keep track of valid indices
            // parse through all rules and filter valid ones
            for (int i = 0; i < i_receiverRules.Count; i++)
            {
                // if receiver handle isn't free skip to next rule
                if (AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].handles[i_receiverRules[i].rH].occupancy != 0) continue;

                // make a copy of corresponding sender type from catalog
                newObject = Utilities.Clone(AOSet[i_receiverRules[i].sT]);

                // create Transformation
                Transform orient = Transform.PlaneToPlane(AOSet[i_receiverRules[i].sT].handles[i_receiverRules[i].sH].sender,
                    AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].handles[i_receiverRules[i].rH].receivers[i_receiverRules[i].rR]);

                // transform sender object
                newObject.Transform(orient);

                // verify Z lock
                // if absolute Z lock is true for the current object...
                if (CheckWorldZLock && newObject.worldZLock)
                    // ...perform that check too - if test is not passed continue to next object
                    if (!Utilities.AbsoluteZCheck(newObject)) continue;

                // verify environment clash
                // if the object clashes with the environment continue to next object
                if (environmentCheck(newObject)) continue;

                // verify clash with existing assemblage
                // if the object clashes with surrounding objects continue to next object
                if (Utilities.CollisionCheckInAssemblage(this, newObject, out neighbourIndexes)) continue;

                // if checks were passed add new objects to candidates and
                // corresponding rule index to valid list
                i_validRulesIndexes.Add(i);
                CandidateObjects.Add(newObject);
                // neighbourIndexes are saved for Obstruction check
                i_candidatesNeighAInd.Add(neighbourIndexes);
            }

            return CandidateObjects.Count > 0;
        }

        /// <summary>
        /// A virtual method for <see cref="Rule"/> selection - default version uses internally predefined criteria
        /// </summary>
        /// <param name="candidates">List of candidates <see cref="AssemblyObject"/>s</param>
        /// <param name="newObject">new <see cref="AssemblyObject"/> to add to the <see cref="Assemblage"/></param>
        /// <returns>The selected <see cref="Rule"/></returns>
        /// <remarks>The method is virtual, so it can be customized with an override</remarks>
        public virtual Rule SelectRule(List<AssemblyObject> candidates, out AssemblyObject newObject)
        {
            // sequential index of winner candidate
            int winnerIndex;
            double[] sendersvalues = computeSendersValues(candidates);
            winnerIndex = selectSender(sendersvalues);
            i_senderSeqInd = winnerIndex;
            // new Object is found
            newObject = candidates[winnerIndex];
            // record its sender value before returning
            newObject.senderValue = sendersvalues[winnerIndex];
            return i_receiverRules[i_validRulesIndexes[winnerIndex]];
        }

        /// <summary>
        /// Adds a valid <see cref="AssemblyObject"/> to the <see cref="Assemblage"/>, updating connectivity
        /// </summary>
        /// <param name="newObject"></param>
        /// <param name="rule"></param>
        /// 
        public virtual void AddValidObject(AssemblyObject newObject, Rule rule)
        {
            // assign index (in future implementations, check for index uniqueness or transform in Hash)
            newObject.AInd = nextAInd;
            nextAInd++;
            GH_Path newObjectPath = new GH_Path(newObject.AInd);

            // add rule to sequence as string
            AssemblageRules.Add(rule.ToString(), newObjectPath);

            // add receiver object AInd to list
            ReceiverAIndexes.Add(i_receiverAInd, newObjectPath);

            // . . . . . UPDATE HANDLES
            // update sender + receiver handle status (handle index, occupancy, neighbourObject, neighbourHandle, weight)
            Utilities.UpdateHandlesOnConnection(newObject, rule.sH, AssemblyObjects.Branch(i_receiverSeqBranchInd)[0], rule.rH);

            // compute newObject receiver value
            newObject.receiverValue = computeReceiverValue(newObject);
            if (HeuristicsSettings.heuristicsMode == 1)
                newObject.iWeight = ComputeRiWeight(newObject);

            // add centroid to assemblage centroids tree
            // future implementation: if object has children, insert all children centroids under the same AO index (assemblage.Count in this case)
            centroidsTree.Insert(newObject.referencePlane.Origin, newObject.AInd);// assemblyObjects.Count);
            centroidsAO.Add(newObject.AInd);// assemblyObjects.Count);

            // add new object to available objects indexes and its receiver value to the list
            availableObjects.Add(newObject.AInd);// assemblyObjects.Count);
            availableReceiverValues.Add(newObject.receiverValue);

            // add new object to assemblage, its AInd to the index map
            AssemblyObjects.Add(newObject, newObjectPath);

            // if receiving object is fully occupied (all handles either connected or occluded) remove it from the available objects
            if (AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].handles.Where(x => x.occupancy != 0).Sum(x => 1) == AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].handles.Length)
            {
                availableReceiverValues.RemoveAt(availableObjects.IndexOf(i_receiverAInd));
                availableObjects.Remove(i_receiverAInd);
            }
        }

        #endregion

        #region compute receiver methods

        private double ComputeRZero(AssemblyObject receiver) => 0.0;

        private double ComputeRRandom(AssemblyObject receiver) => rnd.NextDouble();

        private double ComputeRScalarField(AssemblyObject receiver)
        {
            return Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetClosestScalar(receiver.referencePlane.Origin));
        }

        private double ComputeRScalarFieldInterpolated(AssemblyObject receiver)
        {
            return Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetInterpolatedScalar(receiver.referencePlane.Origin));
        }
        /// <summary>
        /// Computes absolute difference between scalar <see cref="Field"/> value and threshold from each free Handle 
        /// </summary>
        /// <param name="receiver"></param>
        /// <returns>the minimum absolute difference from the threshold</returns>
        private double ComputeRScalarFieldHandles(AssemblyObject receiver)
        {
            double scalarValue = double.MaxValue;
            double handleValue;
            foreach (Handle h in receiver.handles)
            {
                if (h.occupancy != 0) continue;
                handleValue = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetClosestScalar(h.sender.Origin));
                if (handleValue < scalarValue) scalarValue = handleValue;
            }
            return scalarValue;
        }
        /// <summary>
        /// Computes sum of <see cref="AssemblyObject"/> weights in a search sphere, updating neighbours accordingly
        /// </summary>
        /// <param name="receiver"></param>
        /// <returns>the weights sum</returns>
        private double ComputeRWeightDensity(AssemblyObject receiver)
        {
            // search for neighbour objects in radius
            double density = 0;
            centroidsTree.Search(new Sphere(receiver.referencePlane.Origin, CollisionRadius), (s, args) =>
            {
                GH_Path neighPath = new GH_Path(centroidsAO[args.Id]);
                density += AssemblyObjects[neighPath, 0].weight;
                // update neighbour object receiver value with current weight
                AssemblyObjects[neighPath, 0].receiverValue += receiver.weight;
            });

            return density;
        }

        private int ComputeRiWeight(AssemblyObject receiver)
        {
            return ExogenousSettings.field.GetClosestiWeights(receiver.referencePlane.Origin)[0];
        }

        private void ComputeReceivers()
        {
            if (AssemblyObjects.BranchCount < 1000)
                for (int i = 0; i < AssemblyObjects.BranchCount; i++)
                    AssemblyObjects.Branches[i][0].receiverValue = computeReceiverValue(AssemblyObjects.Branches[i][0]);
            else
            {
                Parallel.For(0, AssemblyObjects.BranchCount, i =>
                {
                    AssemblyObjects.Branches[i][0].receiverValue = computeReceiverValue(AssemblyObjects.Branches[i][0]);
                });
            }
        }

        private void ComputeReceiversiWeights()
        {
            if (AssemblyObjects.BranchCount < 1000)
                for (int i = 0; i < AssemblyObjects.BranchCount; i++)
                    AssemblyObjects.Branches[i][0].iWeight = ComputeRiWeight(AssemblyObjects.Branches[i][0]);
            else
            {
                Parallel.For(0, AssemblyObjects.BranchCount, i =>
                {
                    AssemblyObjects.Branches[i][0].iWeight = ComputeRiWeight(AssemblyObjects.Branches[i][0]);
                });
            }
        }

        #endregion

        #region compute candidates methods
        private double[] ComputeZero(List<AssemblyObject> candidates) => candidates.Select(c => 0.0).ToArray();
        private double[] ComputeRandom(List<AssemblyObject> candidates) => candidates.Select(c => rnd.NextDouble()).ToArray();
        private double[] ComputeBBVolume(List<AssemblyObject> candidates)
        {
            BoundingBox bBox;

            double[] BBvolumes = new double[candidates.Count];

            // compute BBvolume for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    bBox = AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].collisionMesh.GetBoundingBox(false);
                    bBox.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBvolumes[i] = bBox.Volume;
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    BoundingBox bBoxpar = AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].collisionMesh.GetBoundingBox(false);
                    bBoxpar.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBvolumes[i] = bBoxpar.Volume;
                });

            return BBvolumes;
        }

        private double[] ComputeBBDiagonal(List<AssemblyObject> candidates)
        {
            BoundingBox bBox;

            double[] BBdiagonals = new double[candidates.Count];

            // compute BBvolume for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    bBox = AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].collisionMesh.GetBoundingBox(false);
                    bBox.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBdiagonals[i] = bBox.Diagonal.Length;
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    BoundingBox bBoxpar = AssemblyObjects.Branch(i_receiverSeqBranchInd)[0].collisionMesh.GetBoundingBox(false);
                    bBoxpar.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBdiagonals[i] = bBoxpar.Diagonal.Length;
                });

            return BBdiagonals;
        }

        private double[] ComputeScalarField(List<AssemblyObject> candidates)
        {
            double[] scalarValues = new double[candidates.Count];

            // compute scalarvalue for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    // try, instead of Math.Abs(), the following:
                    //version 1
                    //i = x < 0 ? -x : x;
                    //version 2 (bitwise operations)
                    //i = (x ^ (x >> 31)) - (x >> 31);
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetClosestScalar(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetClosestScalar(candidates[i].referencePlane.Origin));
                });

            return scalarValues;
        }

        private double[] ComputeScalarFieldInterpolated(List<AssemblyObject> candidates)
        {
            double[] scalarValues = new double[candidates.Count];

            // compute scalarvalue for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetInterpolatedScalar(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetInterpolatedScalar(candidates[i].referencePlane.Origin));
                });

            return scalarValues;
        }

        private double[] ComputeVectorField(List<AssemblyObject> candidates)
        {
            double[] vectorValues = new double[candidates.Count];

            // compute Vector angle value for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeVectorFieldBidirectional(List<AssemblyObject> candidates)
        {
            double[] vectorValues = new double[candidates.Count];

            // compute bidirectional Vector angle value for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeVectorFieldInterpolated(List<AssemblyObject> candidates)
        {
            double[] vectorValues = new double[candidates.Count];

            // compute Vector angle value for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeVectorFieldBidirectionalInterpolated(List<AssemblyObject> candidates)
        {
            double[] vectorValues = new double[candidates.Count];

            // compute bidirectional Vector angle value for all candidates
            if (candidates.Count < 100)
                for (int i = 0; i < candidates.Count; i++)
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeWRC(List<AssemblyObject> candidates)
        {
            double[] wrcWeights = new double[candidates.Count];

            for (int i = 0; i < i_validRulesIndexes.Count; i++)
                wrcWeights[i] = i_receiverRules[i_validRulesIndexes[i]].iWeight;

            return wrcWeights;
        }

        /// <summary>
        /// Performs a Weighted Random Choice given an array of weights
        /// </summary>
        /// <param name="weights"></param>
        /// <returns>index of the selected weight</returns>
        private int WeightedRandomChoiceIndex(int[] weights)
        {

            int totWeights = weights.Sum(w => w);

            int chosenInd = rnd.Next(totWeights);
            int valueInd = 0;

            while (chosenInd >= 0)
            {
                chosenInd -= weights[valueInd];
                valueInd++;
            }

            valueInd -= 1;

            return valueInd;
        }

        /// <summary>
        /// Performs a Weighted Random Choice on a data array and corresponding weights
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <param name="weights"></param>
        /// <returns>the selected value</returns>
        private T WeightedRandomChoice<T>(T[] values, int[] weights)
        {
            return values[WeightedRandomChoiceIndex(weights)];
        }

        #endregion

        #region select value methods

        private int SelectRandomIndex(double[] values) => (int)(rnd.NextDouble() * values.Length);

        private int SelectMinIndex(double[] values)
        {
            double min = double.MaxValue;
            int minindex = -1;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] < min)
                {
                    min = values[i];
                    minindex = i;
                }
            }

            return minindex;
        }

        private int SelectMaxIndex(double[] values)
        {
            double max = double.MinValue;
            int maxindex = -1;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                    maxindex = i;
                }
            }

            return maxindex;
        }

        private int SelectWRCIndex(double[] values)
        {
            // Weighted Random Choice among valid rules
            int[] iWeights = values.Select(v => (int)v).ToArray();
            int[] indexes = new int[values.Length];
            for (int i = 0; i < values.Length; i++)
                indexes[i] = i;

            return WeightedRandomChoice(indexes, iWeights);
        }

        private int SelectNextRuleIndex(double[] values)
        {
            int count = 0;
            while (!i_validRulesIndexes.Contains(E_sequentialRuleIndex) && count < 100)
            {
                E_sequentialRuleIndex = (E_sequentialRuleIndex++) % i_receiverRules.Count;
                count++;
            }
            return i_validRulesIndexes.IndexOf(E_sequentialRuleIndex);
        }

        #endregion

        #region environment methods

        /// <summary>
        /// Checks environment compatibility of an AssemblyObject
        /// </summary>
        /// <param name="AO"></param>
        /// <returns>true if an object is not compatible with the <see cref="MeshEnvironment"/>s</returns>
        /// <remarks>An eventual Container is checked using collision mode</remarks>
        private bool EnvClashCollision(AssemblyObject AO)
        {
            foreach (MeshEnvironment mEnv in ExogenousSettings.environmentMeshes)
            {

                switch (mEnv.type)
                {
                    case MeshEnvironment.Type.Void: // controls only centroid in/out
                        if (mEnv.IsPointInvalid(AO.referencePlane.Origin)) return true;
                        break;
                    case MeshEnvironment.Type.Solid:
                        if (mEnv.CollisionCheck(AO.collisionMesh)) return true;
                        goto case MeshEnvironment.Type.Void;
                    case MeshEnvironment.Type.Container:
                        goto case MeshEnvironment.Type.Solid;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks environment compatibility of an AssemblyObject
        /// </summary>
        /// <param name="AO">AssemblyObject to verify</param>
        /// <returns>true if an object is not compatible with the <see cref="MeshEnvironment"/>s</returns>
        /// <remarks>An eventual Container is checked using inclusion mode (default behaviour)</remarks>

        private bool EnvClashInclusion(AssemblyObject AO)
        {
            foreach (MeshEnvironment mEnv in ExogenousSettings.environmentMeshes)
            {

                switch (mEnv.type)
                {
                    case MeshEnvironment.Type.Void: // controls only centroid in/out
                        if (mEnv.IsPointInvalid(AO.referencePlane.Origin)) return true;
                        break;
                    case MeshEnvironment.Type.Solid:
                        if (mEnv.CollisionCheck(AO.collisionMesh)) return true;
                        goto case MeshEnvironment.Type.Void;
                    case MeshEnvironment.Type.Container:
                        goto case MeshEnvironment.Type.Void;
                }
            }

            return false;
        }

        #endregion

        #region reset methods

        /// <summary>
        /// Reset Heuristics and Exogenous Settings
        /// </summary>
        /// <param name="Heu">Heuristics Setting</param>
        /// <param name="Exo">Exogenous Settings</param>
        public void ResetSettings(HeuristicsSettings Heu, ExogenousSettings Exo)
        {
            HeuristicsSettings = Heu;
            ExogenousSettings = Exo;

            // reset heuristicsTree
            heuristicsTree = InitHeuristics(HeuristicsSettings.heuSetsString);
            // comment this in case currentHeuristics is taken directly from HeuristicsSettings
            currentHeuristics = HeuristicsSettings.currentHeuristics;

            // environment
            SetEnvCheckMethod();
            SetSandbox(ExogenousSettings.sandBox);

            // selection modes
            SetReceiverSelectionMode(HeuristicsSettings.receiverSelectionMode);
            SetSenderSelectionMode(HeuristicsSettings.ruleSelectionMode);

            // compute receiver values
            ComputeReceivers();
            // if heuristics is Field driven compute iWeights
            if (HeuristicsSettings.heuristicsMode == 1)
                ComputeReceiversiWeights();

            // reset available/unreachable objects
            ResetAvailableObjects();
        }

        private void ResetSandboxRtree()
        {
            E_sandboxCentroidsTree = new RTree();
            E_sandboxCentroidsAO = new List<int>();
            // create List of centroid correspondance with their AO
            centroidsTree.Search(E_sandbox.BoundingBox, (sender, args) =>
            {
                // recover the AssemblyObject centroid related to the found centroid
                // args.Id contains the AInd
                E_sandboxCentroidsTree.Insert(AssemblyObjects[new GH_Path(args.Id), 0].referencePlane.Origin, args.Id);
                E_sandboxCentroidsAO.Add(args.Id);
            });
        }

        /// <summary>
        /// Verify list of available/unreachable objects according to current environment and heuristics
        /// </summary>
        private void ResetAvailableObjects()
        {
            /*
             LOGIC: saturated objects will remain as such, so no need to check them
             . consider all unreachable initially as available
             . perform checks to find new unreachables and remove from available and their receiver values lists
             */

            // move all unreachable indexes to available and clear unrechable list 
            foreach (int unreachInd in unreachableObjects)
                if (!availableObjects.Contains(unreachInd)) availableObjects.Add(unreachInd);

            unreachableObjects.Clear();

            // reset availableReceiverValues list
            availableReceiverValues.Clear();
            for (int i = 0; i < availableObjects.Count; i++)
            {
                availableReceiverValues.Add(AssemblyObjects[new GH_Path(availableObjects[i]), 0].receiverValue);
            }

            // reset according to environment meshes
            ResetAvailableObjectsEnvironment();

            // check for every available object and move from available to unavailable if:
            // - there isn't a rule with their rType
            // - a rule exists for their rType but no match for any of its free handles 

            List<int> newUnreachables = new List<int>();
            GH_Path path;


            // . . . scan available objects
            for (int i = availableObjects.Count - 1; i >= 0; i--)
            {

                AssemblyObject avObject = AssemblyObjects[new GH_Path(availableObjects[i]), 0];

                // check if current heuristics is fixed or field-dependent
                if (HeuristicsSettings.heuristicsMode == 1)
                    currentHeuristics = avObject.iWeight;


                // current heuristics path to search for {current heuristics; receiver type}
                path = new GH_Path(currentHeuristics, avObject.type);

                // if object type is not in the heuristics assign as unreachable and remove from available
                if (!heuristicsTree.PathExists(path))
                {
                    newUnreachables.Add(availableObjects[i]);
                    availableObjects.RemoveAt(i);
                    availableReceiverValues.RemoveAt(i);
                }
                else // if a path exists as receiver, check if there are rules for its free handles
                {
                    bool unreachable = true;
                    // scan its handles against current heuristics rules
                    for (int j = 0; j < avObject.handles.Length; j++)
                    {
                        // continue if handle is connected or occluded
                        if (avObject.handles[j].occupancy != 0) continue;

                        foreach (Rule rule in heuristicsTree.Branch(path))
                            // if there is at least a free handle with an available rule for it (rH is the receiving handle index)
                            if (rule.rH == j)
                            {
                                // activate flag and break loop
                                unreachable = false;
                                break;
                            }
                    }

                    // test if unreachable after looping through all handles
                    if (unreachable)
                    // assign as unreachable and remove from available
                    {
                        newUnreachables.Add(availableObjects[i]);
                        availableObjects.RemoveAt(i);
                        availableReceiverValues.RemoveAt(i);
                    }
                }
            }

            // add new unreachables to list
            unreachableObjects.AddRange(newUnreachables);

            // add to specific Sandbox lists if a valid Sandbox is present
            if (E_sandbox.IsValid)
            {
                E_sandboxAvailableObjects.Clear();
                E_sandboxUnreachableObjects.Clear();
                foreach (int avOb in availableObjects)
                    if (E_sandbox.Contains(AssemblyObjects[new GH_Path(avOb), 0].referencePlane.Origin)) E_sandboxAvailableObjects.Add(avOb);
                //if (E_sandboxCentroidsAO.Contains(avOb)) E_sandboxAvailableObjects.Add(avOb);
                foreach (int unrOb in unreachableObjects)
                    if (E_sandbox.Contains(AssemblyObjects[new GH_Path(unrOb), 0].referencePlane.Origin)) E_sandboxAvailableObjects.Add(unrOb);
                //if (E_sandboxCentroidsAO.Contains(unrOb)) E_sandboxUnreachableObjects.Add(unrOb);
            }

        }

        private void ResetAvailableObjectsEnvironment()
        {

            // verify available objects against new environment meshes to update unreachable list
            for (int i = availableObjects.Count - 1; i >= 0; i--)
            {
                if (environmentCheck(AssemblyObjects[new GH_Path(availableObjects[i]), 0]))
                {
                    unreachableObjects.Add(availableObjects[i]);
                    availableObjects.RemoveAt(i);
                    availableReceiverValues.RemoveAt(i);
                }
            }

        }
        #endregion

        #region extract methods

        /// <summary>
        /// Extract available objects indices
        /// </summary>
        /// <returns>An array of AInd of available objects in the Assemblage</returns>
        public GH_Integer[] ExtractAvailableObjects()
        {
            GH_Integer[] outIndexes = new GH_Integer[availableObjects.Count];

            if (availableObjects.Count < 1000)
                for (int i = 0; i < availableObjects.Count; i++)
                    outIndexes[i] = new GH_Integer(availableObjects[i]);
            else
                Parallel.For(0, availableObjects.Count, i =>
                {
                    outIndexes[i] = new GH_Integer(availableObjects[i]);
                });
            return outIndexes;
        }

        /// <summary>
        /// Extract unreachable objects indices
        /// </summary>
        /// <returns>An array of AInd of unreachable objects in the Assemblage</returns>
        public GH_Integer[] ExtractUnreachableObjects()
        {
            GH_Integer[] outIndexes = new GH_Integer[unreachableObjects.Count];

            if (unreachableObjects.Count < 1000)
                for (int i = 0; i < unreachableObjects.Count; i++)
                    outIndexes[i] = new GH_Integer(unreachableObjects[i]);
            else
                Parallel.For(0, unreachableObjects.Count, i =>
                {
                    outIndexes[i] = new GH_Integer(unreachableObjects[i]);
                });
            return outIndexes;
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
