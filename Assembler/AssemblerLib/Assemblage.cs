using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssemblerLib
{
    /// <summary>
    /// Assemblage class - A Class that manages Assemblages
    /// refactor Assemblage class to embed settings structs as part of it (variables used directly from the structs)
    /// </summary>
    public class Assemblage
    {
        /*
         assemblage external parameters:
         . previous assemblage (list of AssemblyObject to start from)
         . starting plane
         . starting component type
         . starting heuristics index (when more than one is provided)
         . environment objects
         . fields (scalar/vector/weights for rules)
         */

        #region fields & properties
        /// <summary>
        /// The list of <see cref="AssemblyObject"/>s in the assemblage
        /// </summary>
        public List<AssemblyObject> assemblyObjects;
        /// <summary>
        /// The set of unique <see cref="AssemblyObject"/> types
        /// </summary>
        public AssemblyObject[] AOSet;
        /// <summary>
        /// The objects Dictionary built from the set
        /// </summary>
        public Dictionary<string, int> objectsDictionary;
        /// <summary>
        /// Heuristics settings (rules + selection criteria)
        /// </summary>
        public HeuristicsSettings HeuristicsSettings;
        /// <summary>
        /// Exogenous settings (external influences)
        /// </summary>
        public ExogenousSettings ExogenousSettings;
        /// <summary>
        /// Sandbox for focused assemblage growth - EXPERIMENTAL - NOT IMPLEMENTED YET
        /// </summary>
        public Box E_sandbox;
        /// <summary>
        /// Index of currently used heuristics
        /// </summary>
        public int currentHeuristics;
        /// <summary>
        /// Heuristics as a <see cref="Rule"/> DataTree
        /// </summary>
        internal DataTree<Rule> heuristicsTree;
        public int RulesCount { get => heuristicsTree.BranchCount; }
        /// <summary>
        /// Candidate sender objects at each iteration
        /// </summary>
        public List<AssemblyObject> candidateObjects;
        /// <summary>
        /// List of Heuristics used during the assemblage
        /// </summary>
        public List<string> assemblageRules;
        /// <summary>
        /// List of Receivers indexes used in the assemblage
        /// </summary>
        public List<int> receiverIndexes;
        /// <summary>
        /// stores <see cref="Rule"/>s pertaining the selected receiver at each Assemblage iteration
        /// </summary>
        public List<Rule> receiverRules;
        /// <summary>
        /// stores indexes of filtered valid rules from <see cref="receiverRules"/> at each Assemblage iteration
        /// </summary>
        public List<int> validRulesIndexes;
        /// <summary>
        /// stores selected receiver index at each Assemblage iteration
        /// </summary>
        public int receiverIndex;

        /// <summary>
        /// Delegate type for environment contaier behavior (collision or inclusion)
        /// </summary>
        /// <param name="sO"></param>
        /// <returns></returns>
        public delegate bool EnvironmentCheckMethod(AssemblyObject sO);
        private EnvironmentCheckMethod environmentCheck;
        /// <summary>
        /// Delegate type for computing candidates (sender) values
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="receiverIndex"></param>
        /// <returns>array of values associated with the candidates</returns>
        public delegate double[] ComputeCandidatesValuesMethod(AssemblyObject[] candidates);
        private ComputeCandidatesValuesMethod computeSendersValues;
        /// <summary>
        /// Delegate type for computing a single receiver value
        /// </summary>
        /// <param name="receiver"></param>
        /// <returns></returns>
        public delegate double ComputeReceiverMethod(AssemblyObject receiver);
        private ComputeReceiverMethod computeReceiverValue;
        /// <summary>
        /// Delegate method for choosing winner index from sender values
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public delegate int SelectWinnerMethod(double[] values);
        private SelectWinnerMethod selectSender;
        private SelectWinnerMethod selectReceiver;

        /// <summary>
        /// if True, forces candidates to have their own Z axis parallel to the World Z axis (fixed up direction)
        /// </summary>
        public bool checkWorldZLock;

        //internal variables (E_ stands for Experimental feature) - used across the .dll library
        internal RTree centroidsTree;
        internal List<int> centroidsAO; // list of centroid/AssemblyObject index correspondances
        internal List<int> availableObjects;
        internal List<int> unreachableObjects;

        internal RTree E_sandboxCentroidsTree;
        internal List<int> E_sandboxCentroidsAO; // list of sandbox centroid/AssemblyObject correspondances
        internal List<int> E_sandboxAvailableObjects;
        internal List<int> E_sandboxUnreachableObjects;
        internal int E_sequentialRuleIndex; // progressive index for rule selection in sequential mode

        internal HashSet<int> handleTypes; // set of all available handle types
        internal readonly Random rnd;

        #endregion

        #region constructors
        public Assemblage()
        {

        }

        /// <summary>
        /// Construct an Assemblage from essential parameters
        /// </summary>
        /// <param name="AOs"></param>
        /// <param name="pAO"></param>
        /// <param name="startPlane"></param>
        /// <param name="startType"></param>
        /// <param name="HeuristicsSettings"></param>
        /// <param name="ExogenousSettings"></param>
        public Assemblage(List<AssemblyObject> AOs, List<AssemblyObject> pAO, Plane startPlane, int startType, HeuristicsSettings HeuristicsSettings, ExogenousSettings ExogenousSettings)
        {
            rnd = new Random();

            // initialize AssemblyObjects variables
            assemblyObjects = new List<AssemblyObject>();
            centroidsTree = new RTree();
            centroidsAO = new List<int>();
            availableObjects = new List<int>();
            unreachableObjects = new List<int>();

            this.HeuristicsSettings = HeuristicsSettings;
            this.ExogenousSettings = ExogenousSettings;

            //initialize environment method
            SetEnvCheckMethod();


            // initialize sandbox
            if (this.ExogenousSettings.sandBox.IsValid)
            {
                this.E_sandbox = this.ExogenousSettings.sandBox;
                Transform scale = Transform.Scale(this.E_sandbox.Center, 1.2);
                this.E_sandbox.Transform(scale);
                E_sandboxCentroidsTree = new RTree();
                E_sandboxCentroidsAO = new List<int>();
                E_sandboxAvailableObjects = new List<int>();
                E_sandboxUnreachableObjects = new List<int>();
            }
            else
            {
                this.ExogenousSettings.sandBox = Box.Empty;
            }

            // fill the unique objects set
            // cast input to AssemblyObject array
            AOSet = AOs.ToArray();

            // build the dictionary
            objectsDictionary = Utilities.BuildDictionary(AOSet, true);

            // fill handle types HashSet
            handleTypes = Utilities.BuildHandlesHashSet(AOSet);

            // if there is a previous Assemblage
            if (pAO != null && pAO.Count > 0)
            {
                PopulateAssemblage(pAO);
            }
            else
            {
                // start the assemblage with one object
                StartAssemblage(startType, startPlane);
            }

            // initialize heuristics
            // heuristics MUST be initialized after filling the AOdictionary with ALL types,
            // even those from the previous assemblage
            heuristicsTree = InitHeuristics(this.HeuristicsSettings.heuristicsString);
            currentHeuristics = this.HeuristicsSettings.currentHeuristics;

            // initialize selection modes
            E_sequentialRuleIndex = 0; // progressive index for rule selection in sequential mode (EXPERIMENTAL)
            SetReceiverSelectionMode(this.HeuristicsSettings.receiverSelectionMode);
            SetSenderSelectionMode(this.HeuristicsSettings.ruleSelectionMode);

            // compute receiver values
            ComputeReceivers();
            if (this.HeuristicsSettings.heuristicsMode == 1)
                ComputeReceiversiWeights();

            // initialize other variables
            candidateObjects = new List<AssemblyObject>();
            assemblageRules = new List<string>();
            receiverIndexes = new List<int>();

            // initialize check World Z-Lock
            checkWorldZLock = false;

            // reset available/unreachable objects
            ResetAvailableObjects();
        }

        private void StartAssemblage(int startType, Plane startPlane)
        {
            // start the assemblage with one object
            AssemblyObject startObject = Utilities.Clone(AOSet[startType]);
            startObject.Transform(Transform.PlaneToPlane(startObject.referencePlane, startPlane));
            startObject.AInd = 0;
            assemblyObjects.Add(startObject);
            centroidsTree.Insert(assemblyObjects[0].referencePlane.Origin, 0);
            // future implementation: if object has children or multiple centroids, insert all children centroids under the same AO index (0 in this case)
            centroidsAO.Add(0);
            availableObjects.Add(0);
            // if sandbox is valid and contains origin add it to its tree & add according value to the boolean mask
            if (E_sandbox.IsValid && E_sandbox.Contains(assemblyObjects[0].referencePlane.Origin))
            {
                E_sandboxCentroidsTree.Insert(assemblyObjects[0].referencePlane.Origin, 0);
                E_sandboxCentroidsAO.Add(centroidsAO[0]); // see note above for multiple centroids
                E_sandboxAvailableObjects.Add(0);
            }
        }

        private void PopulateAssemblage(List<AssemblyObject> pAO)
        {
            // assign types to previous objects
            List<AssemblyObject> newTypes;
            assemblyObjects.AddRange(AssignType(pAO, out newTypes));
            // update AOset and handleTypes HashSet with new types
            if (newTypes.Count > 0)
            {
                List<AssemblyObject> AOsetList = AOSet.ToList();
                AOsetList.AddRange(newTypes);
                AOSet = AOsetList.ToArray();
                handleTypes = Utilities.BuildHandlesHashSet(AOSet);
            }

            // add previous objects to the centroids tree and check their occupancy/availability status
            bool inSandBox;
            for (int i = 0; i < assemblyObjects.Count; i++)
            {
                inSandBox = false;
                // add object to the centroids tree
                // future implementation: if object has children, insert all children centroids under the same AO index (i in this case)
                centroidsTree.Insert(assemblyObjects[i].referencePlane.Origin, i);
                centroidsAO.Add(i);
                // if sandbox is valid and contains origin add it to its tree
                if (E_sandbox.IsValid && E_sandbox.Contains(assemblyObjects[i].referencePlane.Origin))
                {
                    inSandBox = true;
                    E_sandboxCentroidsTree.Insert(assemblyObjects[i].referencePlane.Origin, i);
                    E_sandboxCentroidsAO.Add(centroidsAO[i]); // see note above for multiple centroids
                }
                // find if the AssemblyObject is available
                foreach (Handle h in assemblyObjects[i].handles)
                    if (h.occupancy == 0 && handleTypes.Contains(h.type))
                    {
                        availableObjects.Add(i);
                        if (E_sandbox.IsValid && inSandBox)
                            E_sandboxAvailableObjects.Add(i);
                        break;
                    }
            }
        }

        #endregion

        #region setup methods

        private List<AssemblyObject> AssignType(List<AssemblyObject> pAO, out List<AssemblyObject> newTypes)
        {
            // previous objects are checked against the dictionary
            // - if they are not part of the existing set, a new type is added

            newTypes = new List<AssemblyObject>();
            List<AssemblyObject> pAOTyped = new List<AssemblyObject>();
            int newType = objectsDictionary.Count;
            for (int i = 0; i < pAO.Count; i++)
            {
                // if object name isn't already in the dictionary - new type identified
                if (!objectsDictionary.ContainsKey(pAO[i].name))
                {
                    // add new type to the dictionary
                    objectsDictionary.Add(pAO[i].name, newType);
                    // add new type object to the dictionary candidates
                    AssemblyObject AOnewType = Utilities.Clone(pAO[i]);
                    AOnewType.type = newType;
                    newTypes.Add(AOnewType);
                    newType++;
                }
                pAO[i].type = objectsDictionary[pAO[i].name];
                // add a copy of the original object to the list
                pAOTyped.Add(Utilities.CloneWithConnectivity(pAO[i]));
            }

            return pAOTyped;
        }

        private DataTree<Rule> InitHeuristics(List<string> heu)
        {
            // rules data tree has a path of {k;rT} where k is the heuristics set and rT the receiving type
            DataTree<Rule> heuT = new DataTree<Rule>();
            for (int k = 0; k < heu.Count; k++)
            {
                //          split by list of rules (,)
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
                    sT = objectsDictionary[sen[0]];
                    rT = objectsDictionary[rec[0]];
                    // sender handle index
                    sH = Convert.ToInt32(sen[1]);
                    // weight
                    w = Convert.ToInt32(rule[2]);
                    string[] rRot = rec[1].Split(new[] { '=' });
                    // receiver handle index and rotation
                    rH = Convert.ToInt32(rRot[0]);
                    rRA = Convert.ToDouble(rRot[1]);
                    rR = AOSet[rT].handles[rH].rDictionary[rRA]; // using rotations

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
                this.E_sandbox = sandbox;
                Transform scale = Transform.Scale(this.E_sandbox.Center, 1.2);
                this.E_sandbox.Transform(scale);
                ResetSandboxRtree();
            }

        }

        /// <summary>
        /// Sets Environment Check Method to use
        /// </summary>
        private void SetEnvCheckMethod()
        {
            switch (ExogenousSettings.environmentMode)
            {
                case 0:
                    environmentCheck = (AssemblyObject sO) => { return false; }; // anonymous function (avoids env check entirely)
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
        /// <param name="ruleSelectionMode"></param>
        private void SetReceiverSelectionMode(int receiverSelectionMode)
        {
            // set receiver compute and selection delegates
            switch (receiverSelectionMode)
            {
                case 0:
                    // random selection among available objects
                    computeReceiverValue = ComputeRZero; // ComputeRRandom;
                    selectReceiver = SelectRandomIndex;
                    break;
                case 1:
                    // scalar field search - fast (closest Field point)
                    computeReceiverValue = ComputeRScalarField;
                    selectReceiver = SelectMinIndex;
                    break;
                case 2:
                    // scalar field search - (interpolated values)
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
                    selectReceiver = (a) => { return availableObjects.Count - 1; }; // anonymous function that returns last available object index
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
                case 0:
                    // random selection - chooses one candidate at random
                    computeSendersValues = ComputeZero;
                    selectSender = SelectRandomIndex;
                    break;
                case 1:
                    // scalar field closest with threshold - chooses candidate whose centroid closest scalar field value is closer to the threshold
                    computeSendersValues = ComputeScalarField;
                    selectSender = SelectMinIndex;
                    break;
                case 2:
                    // scalar field accurate with threshold - chooses candidate whose centroid interpolated scalar field value is closer to the threshold
                    computeSendersValues = ComputeScalarFieldInterpolated;
                    selectSender = SelectMinIndex;
                    break;
                case 3:
                    // vector field closest - chooses candidate whose direction has minimum angle with closest vector field point (bidirectional)
                    computeSendersValues = ComputeVectorFieldBidirectional;
                    selectSender = SelectMinIndex;
                    break;
                case 4:
                    // vector field accurate - chooses candidate whose direction has minimum angle with interpolated vector field point (bidirectional)
                    computeSendersValues = ComputeVectorFieldBidirectionalInterpolated;
                    selectSender = SelectMinIndex;
                    break;
                case 5:
                    // density search 1 - chooses candidate with minimal bounding box volume with receiver
                    computeSendersValues = ComputeBBVolume;
                    selectSender = SelectMinIndex;
                    break;
                case 6:
                    // density search 2 - chooses candidate with minimal bounding box diagonal with receiver
                    computeSendersValues = ComputeBBDiagonal;
                    selectSender = SelectMinIndex;
                    break;
                case 7:
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
        /// <item>receiver selection (where do I add the next item?)</item>
        /// <item>rule selection (which one and how?)</item>
        /// <item>new object addition to the assemblage</item>
        /// <item>assemblage status update</item>
        /// </list>
        /// </summary>
        /// <remarks>The method is virtual, so it can be customized with an override</remarks>
        public virtual void Update()
        {

            /*
             In future implementations, add the possibility to implement the opposite logic:

             . start from the sender type
             . look for all possible candidates among the existing assemblage free handles and relative rules
             . filter selection according to criteria and add winner

            To implement this logic, the heuristic tree should be queried by receiver type, its free handle index and sender type:
            this should return all rules matching sender type
            Since it scans the entire assemblage, the implementation of the SandBox might be a critical prerequisite.

            Another thought:

            . try to make an assemblage given a fixed list of AssemblyObject types and their respective count
            . try combinations until exhaustion of list, also multiple times (given n. of attempts or target condition)
            . save each attempt to disk (including list of used rules)
             */

            // reset receiver Rules and valid indexes 
            receiverRules = new List<Rule>();
            validRulesIndexes = new List<int>();
            AssemblyObject newObject;
            receiverIndex = 0;

            while (availableObjects.Count > 0)
            {
                // . . . . . . .    1. receiver selection
                // . . . . . . .    
                // . . . . . . .    

                // this method contains the selection criteria for the receiver
                int availableIndex = SelectReceiver();
                receiverIndex = availableObjects[availableIndex];

                // this function sifts candidates filtering invalid results (i.e. collisions, environment)
                bool found = RetrieveCandidates();

                if (found)
                    break;
                else
                {
                    // if an object cannot receive candidates, it is marked as unreachable (neither fully occupied nor occluded, yet nothing can be added to it)
                    unreachableObjects.Add(receiverIndex);
                    availableObjects.RemoveAt(availableIndex);
                }
            }

            // if there are no available candidates return
            // this condition is not redundant as the loop above might end by exhausting the list of available objects without finding a candidate
            if (candidateObjects.Count == 0) return;

            // . . . . . . .    2. rule selection
            // . . . . . . .    
            // . . . . . . .    

            Rule rule = SelectRule(candidateObjects, out newObject);

            // add rule to sequence as string
            assemblageRules.Add(rule.ToString());

            // add receiver object index to list
            receiverIndexes.Add(receiverIndex);

            // . . . . . . .    3. new object addition
            // . . . . . . .    
            // . . . . . . .   
            AddValidObject(newObject, rule);

            // . . . . . . .    4. Assemblage status update
            // . . . . . . .    
            // . . . . . . .   
            // check for obstructions and/or secondary handle connections
            // check if newly added object obstructs other handles in the surroundings
            // or its handles are obstructed in turn by other objects
            Utilities.ObstructionCheckAssemblage(this, newObject.AInd);//assemblyObjects.Count - 1);
        }

        /// <summary>
        /// Receiver Selection
        /// </summary>
        /// <returns>The index of the available <see cref="AssemblyObject"/> selected as a receiver</returns>
        public virtual int SelectReceiver()
        {
            double[] receiverValues = new double[availableObjects.Count];

            for (int i = 0; i < availableObjects.Count; i++)
                receiverValues[i] = assemblyObjects[availableObjects[i]].receiverValue;

            return selectReceiver(receiverValues);
        }

        /// <summary>
        /// Retrieve Candidates based on receiver index
        /// </summary>
        /// <param name="receiverRules">List of corresponding <see cref="Rule"/>s</param>
        /// <param name="validRulesIndexes">Indices of valid <see cref="Rule"/>s (candidates that do not collide with existing assemblage and/or environment obstacles)</param>
        /// <param name="candidateObjects">List of candidates <see cref="AssemblyObject"/>s - each candidate corresponds with a valid <see cref="Rule"/></param>
        /// 
        /// <returns>True if at least one suitable candidate has been found, False otherwise</returns>
        public bool RetrieveCandidates()//out List<Rule> receiverRules, out List<int> validRulesIndexes, out List<AssemblyObject> candidateObjects)
        {
            // . find receiver object type
            int receiverType = assemblyObjects[receiverIndex].type;
            candidateObjects = new List<AssemblyObject>();
            validRulesIndexes = new List<int>();

            // select current heuristics - check if heuristic mode is set to field driven
            // in that case, use the receiver's iWeight (where the heuristics index is stored)
            if (HeuristicsSettings.heuristicsMode == 1)
                currentHeuristics = assemblyObjects[receiverIndex].iWeight;
            //currentHeuristics = ExogenousSettings.field.GetClosestiWeights(assemblyObjects[receiverIndex].referencePlane.Origin)[0];

            // . sanity check on rules
            // it is possible, when using a custom set of rules, that an object is only used as sender
            // or it is not included in the selected heuristics. In such cases, there will be no associated rules
            // in case it is picked at random as a potential receiver, so we return empties and false

            if (!heuristicsTree.PathExists(currentHeuristics, receiverType))
            {
                receiverRules = new List<Rule>();
                return false;
            }

            // if a path exists.....
            // . retrieve all rules for receiving object and properly define return variables
            AssemblyObject newObject;
            receiverRules = heuristicsTree.Branch(currentHeuristics, receiverType);

            // orient all candidates around receiving object and keep track of valid indices
            // parse through all rules and filter valid ones
            for (int i = 0; i < receiverRules.Count; i++)
            {
                // if receiver handle isn't free skip to next rule
                if (assemblyObjects[receiverIndex].handles[receiverRules[i].rH].occupancy != 0) continue;

                // make a copy of corresponding sender type from catalog
                newObject = Utilities.Clone(AOSet[receiverRules[i].sT]);

                // create Transformation
                Transform orient = Transform.PlaneToPlane(AOSet[receiverRules[i].sT].handles[receiverRules[i].sH].sender,
                    assemblyObjects[receiverIndex].handles[receiverRules[i].rH].receivers[receiverRules[i].rR]);

                // transform sender object
                newObject.Transform(orient);

                // verify Z lock
                // if absolute Z lock is true for the current object...
                if (checkWorldZLock && newObject.worldZLock)
                    // ...perform that check too - if test is not passed continue to next object
                    if (!Utilities.AbsoluteZCheck(newObject)) continue;

                // verify environment clash
                // if the object clashes with the environment continue to next object
                if (environmentCheck(newObject)) continue;

                // verify clash with existing assemblage
                // if the object clashes with surrounding objects continue to next object
                if (Utilities.CollisionCheckAssemblage(this, newObject)) continue;
                //if (Utilities.CollisionCheckAssemblageParallel(this, newObject)) continue;

                // if checks were passed add new objects to candidates and
                // corresponding rule index to valid list
                validRulesIndexes.Add(i);
                candidateObjects.Add(newObject);

            }

            return candidateObjects.Count > 0;
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
            int winnerIndex;
            double[] sendersvalues = computeSendersValues(candidates.ToArray());
            winnerIndex = selectSender(sendersvalues);

            // new Object is found
            newObject = candidates[winnerIndex];
            // record its sender value before returning
            newObject.senderValue = sendersvalues[winnerIndex];
            return receiverRules[validRulesIndexes[winnerIndex]];
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
            newObject.AInd = assemblyObjects.Count;

            // update sender handle status (occupancy, neighbourObject, neighbourHandle, weight)
            // preserve initial weight
            double newHWeight = newObject.handles[rule.sH].weight;
            newObject.UpdateHandle(rule.sH, 1, receiverIndex, rule.rH, assemblyObjects[receiverIndex].handles[rule.rH].weight);

            // update receiver handle status (occupancy, neighbourObject, neighbourHandle, weight)
            assemblyObjects[receiverIndex].UpdateHandle(rule.rH, 1, newObject.AInd, rule.sH, newHWeight);
            //assemblyObjects[receiverIndex].UpdateHandle(rule.rH, 1, assemblyObjects.Count, rule.sH, newHWeight);

            // compute newObject receiver value
            newObject.receiverValue = computeReceiverValue(newObject);
            if (HeuristicsSettings.heuristicsMode == 1)
                newObject.iWeight = ComputeRiWeight(newObject);

            // add centroid to assemblage centroids tree
            // future implementation: if object has children, insert all children centroids under the same AO index (assemblage.Count in this case)
            centroidsTree.Insert(newObject.referencePlane.Origin, newObject.AInd);// assemblyObjects.Count);
            centroidsAO.Add(newObject.AInd);// assemblyObjects.Count);

            // add new object to assemblage and available objects indexes
            availableObjects.Add(newObject.AInd);// assemblyObjects.Count);
            assemblyObjects.Add(newObject);

            // if receiving object is fully occupied (all handles either connected or occluded) remove it from the available objects
            if (assemblyObjects[receiverIndex].handles.Where(x => x.occupancy != 0).Sum(x => 1) == assemblyObjects[receiverIndex].handles.Length)
                availableObjects.Remove(receiverIndex);
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
        /// Computes sum of <see cref="AssemblyObject"/> weights in a search sphere, updating neighbours accordingly
        /// </summary>
        /// <param name="receiver"></param>
        /// <returns>the weights sum</returns>
        private double ComputeRWeightDensity(AssemblyObject receiver)
        {
            // search for neighbour objects in radius
            double density = 0;
            centroidsTree.Search(new Sphere(receiver.referencePlane.Origin, receiver.collisionRadius), (s, args) =>
            {
                density += assemblyObjects[centroidsAO[args.Id]].weight;
                // update neighbour object receiver value with current weight
                assemblyObjects[centroidsAO[args.Id]].receiverValue += receiver.weight;
            });

            return density;
        }

        private int ComputeRiWeight(AssemblyObject receiver)
        {
            return ExogenousSettings.field.GetClosestiWeights(receiver.referencePlane.Origin)[0];
        }

        private void ComputeReceivers()
        {
            if (assemblyObjects.Count < 1000)
                for (int i = 0; i < assemblyObjects.Count; i++)
                    assemblyObjects[i].receiverValue = computeReceiverValue(assemblyObjects[i]);
            else
            {
                Parallel.For(0, assemblyObjects.Count, i =>
                {
                    assemblyObjects[i].receiverValue = computeReceiverValue(assemblyObjects[i]);
                });
            }
        }

        private void ComputeReceiversiWeights()
        {
            if (assemblyObjects.Count < 1000)
                for (int i = 0; i < assemblyObjects.Count; i++)
                    assemblyObjects[i].iWeight = ComputeRiWeight(assemblyObjects[i]);
            else
            {
                Parallel.For(0, assemblyObjects.Count, i =>
                {
                    assemblyObjects[i].iWeight = ComputeRiWeight(assemblyObjects[i]);
                });
            }
        }

        #endregion

        #region compute candidates methods
        private double[] ComputeZero(AssemblyObject[] candidates) => candidates.Select(c => 0.0).ToArray();
        private double[] ComputeRandom(AssemblyObject[] candidates) => candidates.Select(c => rnd.NextDouble()).ToArray();

        private double[] ComputeBBVolume(AssemblyObject[] candidates)
        {
            BoundingBox bBox;

            double[] BBvolumes = new double[candidates.Length];

            // compute BBvolume for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    bBox = assemblyObjects[receiverIndex].collisionMesh.GetBoundingBox(false);
                    bBox.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBvolumes[i] = bBox.Volume;
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    BoundingBox bBoxpar = assemblyObjects[receiverIndex].collisionMesh.GetBoundingBox(false);
                    bBoxpar.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBvolumes[i] = bBoxpar.Volume;
                });

            return BBvolumes;
        }

        private double[] ComputeBBDiagonal(AssemblyObject[] candidates)
        {
            BoundingBox bBox;

            double[] BBdiagonals = new double[candidates.Length];

            // compute BBvolume for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    bBox = assemblyObjects[receiverIndex].collisionMesh.GetBoundingBox(false);
                    bBox.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBdiagonals[i] = bBox.Diagonal.Length;
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    BoundingBox bBoxpar = assemblyObjects[receiverIndex].collisionMesh.GetBoundingBox(false);
                    bBoxpar.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                    BBdiagonals[i] = bBoxpar.Diagonal.Length;
                });

            return BBdiagonals;
        }

        private double[] ComputeScalarField(AssemblyObject[] candidates)
        {
            double[] scalarValues = new double[candidates.Length];

            // compute scalarvalue for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    // try, instead of Math.Abs(), the following:
                    //version 1
                    //i = x < 0 ? -x : x;
                    //version 2 (bitwise operations)
                    //i = (x ^ (x >> 31)) - (x >> 31);
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetClosestScalar(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetClosestScalar(candidates[i].referencePlane.Origin));
                });

            return scalarValues;
        }

        private double[] ComputeScalarFieldInterpolated(AssemblyObject[] candidates)
        {
            double[] scalarValues = new double[candidates.Length];

            // compute scalarvalue for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetInterpolatedScalar(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    scalarValues[i] = Math.Abs(ExogenousSettings.fieldScalarThreshold - ExogenousSettings.field.GetInterpolatedScalar(candidates[i].referencePlane.Origin));
                });

            return scalarValues;
        }

        private double[] ComputeVectorField(AssemblyObject[] candidates)
        {
            double[] vectorValues = new double[candidates.Length];

            // compute Vector angle value for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeVectorFieldBidirectional(AssemblyObject[] candidates)
        {
            double[] vectorValues = new double[candidates.Length];

            // compute bidirectional Vector angle value for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetClosestVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeVectorFieldInterpolated(AssemblyObject[] candidates)
        {
            double[] vectorValues = new double[candidates.Length];

            // compute Vector angle value for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    vectorValues[i] = Vector3d.VectorAngle(candidates[i].direction, ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeVectorFieldBidirectionalInterpolated(AssemblyObject[] candidates)
        {
            double[] vectorValues = new double[candidates.Length];

            // compute bidirectional Vector angle value for all candidates
            if (candidates.Length < 100)
                for (int i = 0; i < candidates.Length; i++)
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                }
            else
                Parallel.For(0, candidates.Length, i =>
                {
                    vectorValues[i] = 1 - Math.Abs(candidates[i].direction * ExogenousSettings.field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                });

            return vectorValues;
        }

        private double[] ComputeWRC(AssemblyObject[] candidates)
        {
            double[] wrcWeights = new double[candidates.Length];

            for (int i = 0; i < validRulesIndexes.Count; i++)
                wrcWeights[i] = receiverRules[validRulesIndexes[i]].iWeight;

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
            while (!validRulesIndexes.Contains(E_sequentialRuleIndex) && count < 100)
            {
                E_sequentialRuleIndex = (E_sequentialRuleIndex++) % receiverRules.Count;
                count++;
            }
            return validRulesIndexes.IndexOf(E_sequentialRuleIndex);
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
        /// <param name="AO"></param>
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
        /// Reset Heuristics Settings and Exogenous Settings
        /// </summary>
        /// <param name="Heu">Heuristics Setting</param>
        /// <param name="Exo">Exogenous Settings</param>
        public void ResetSettings(HeuristicsSettings Heu, ExogenousSettings Exo)
        {
            HeuristicsSettings = Heu;
            ExogenousSettings = Exo;

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
            if (HeuristicsSettings.heuristicsMode == 1)
                ComputeReceiversiWeights();

            // reset available/unreachable objects
            ResetAvailableObjects();
        }

        private void ResetSandboxRtree()
        {
            E_sandboxCentroidsTree = new RTree();
            // create List of centroid correspondance with their AO

            centroidsTree.Search(E_sandbox.BoundingBox, (sender, args) =>
            {
                // recover the AssemblyObject centroid related to the found centroid
                E_sandboxCentroidsTree.Insert(assemblyObjects[centroidsAO[args.Id]].referencePlane.Origin, args.Id);
            });

            //for (int i = 0; i < assemblage.Count; i++)
            //    if (sandbox.Contains(assemblage[i].referencePlane.Origin)) sandboxTree.Insert(assemblage[i].referencePlane.Origin, i);
        }

        /// <summary>
        /// Verify list of available/unreachable objects according to current environment and heuristics
        /// </summary>
        private void ResetAvailableObjects()
        {
            // reset according to environment meshes
            ResetAvailableObjectsEnvironment();

            // check for every available object and move from available to unavailable if:
            // - there isn't a rule with their rType
            // - a rule exists for their rType but no match for any of its free handles 

            //List<int> newAvailables = new List<int>();
            List<int> newUnreachables = new List<int>();
            GH_Path path;


            // . . . scan available objects
            for (int i = availableObjects.Count - 1; i >= 0; i--)
            {

                // check if current heuristics is fixed or field-dependent
                if (HeuristicsSettings.heuristicsMode == 1)
                    currentHeuristics = assemblyObjects[availableObjects[i]].iWeight;
                //currentHeuristics = ExogenousSettings.field.GetClosestiWeights(assemblyObjects[availableObjects[i]].referencePlane.Origin)[0];

                // current heuristics path to search for {current heuristics; receiver type}
                path = new GH_Path(currentHeuristics, assemblyObjects[availableObjects[i]].type);

                // if object type is not in the heuristics assign as unreachable and remove from available
                if (!heuristicsTree.PathExists(path))
                {
                    newUnreachables.Add(availableObjects[i]);
                    availableObjects.RemoveAt(i);
                }
                else // if a path exists as receiver, check if there are rules for its free handles
                {
                    bool unreachable = true;
                    // scan its handles against current heuristics rules
                    for (int j = 0; j < assemblyObjects[availableObjects[i]].handles.Length; j++)
                    {
                        // continue if handle is connected or occluded
                        if (assemblyObjects[availableObjects[i]].handles[j].occupancy != 0) continue;

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
                    }

                }
            }

            // add new unreachables to list
            unreachableObjects.AddRange(newUnreachables);
        }


        private void ResetAvailableObjectsEnvironment()
        {
            // move all unreachable indexes to available and clear unrechable list 
            foreach (int unreachInd in unreachableObjects)
                if (!availableObjects.Contains(unreachInd))
                    availableObjects.Add(unreachInd);
            unreachableObjects.Clear();

            // verify available objects against new environment meshes to update unreachable list
            for (int i = availableObjects.Count - 1; i >= 0; i--)
            {
                if (environmentCheck(assemblyObjects[availableObjects[i]]))
                {
                    unreachableObjects.Add(availableObjects[i]);
                    availableObjects.RemoveAt(i);
                }
            }

        }
        #endregion

        #region extract methods

        /// <summary>
        /// Extract available objects indices
        /// </summary>
        /// <returns>An array of indices of available objects in the Assemblage</returns>
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
        /// <returns>An array of indices of unreachable objects in the Assemblage</returns>
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

        // all debug functions start with a "D_"

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //public List<GH_Integer> D_ExtractType()
        //{

        //    return assemblage.Select(a => new GH_Integer(a.type)).ToList();
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //public DataTree<GH_Vector> D_ExtractCentroidsDirection()
        //{
        //    DataTree<GH_Vector> outCD = new DataTree<GH_Vector>();

        //    for (int i = 0; i < assemblage.Count; i++)
        //    {
        //        outCD.Add(new GH_Vector((Vector3d)assemblage[i].referencePlane.Origin), new GH_Path(i));
        //        outCD.Add(new GH_Vector(assemblage[i].direction), new GH_Path(i));
        //    }

        //    return outCD;
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <returns></returns>
        //public DataTree<GH_Plane> D_ExtractHandles(bool sender)
        //{
        //    DataTree<GH_Plane> outH = new DataTree<GH_Plane>();

        //    if (sender)
        //        for (int i = 0; i < assemblage.Count; i++)
        //            outH.AddRange(assemblage[i].handles.Select(x => new GH_Plane(x.s)), new GH_Path(i));
        //    else
        //        for (int i = 0; i < assemblage.Count; i++)
        //            for (int j = 0; j < assemblage[i].handles.Length; j++)
        //                outH.AddRange(assemblage[i].handles[j].r.Select(x => new GH_Plane(x)), new GH_Path(i));

        //    return outH;
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //public DataTree<GH_Boolean> D_ExtractHandleStatus()
        //{
        //    DataTree<GH_Boolean> outHS = new DataTree<GH_Boolean>();

        //    for (int i = 0; i < assemblage.Count; i++)
        //        outHS.AddRange(assemblage[i].handles.Select(x => new GH_Boolean(x.occupancy != -1)), new GH_Path(i));

        //    return outHS;
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //public DataTree<GH_Integer> D_ExtractNeighbours()
        //{
        //    DataTree<GH_Integer> outHS = new DataTree<GH_Integer>();

        //    for (int i = 0; i < assemblage.Count; i++)
        //        outHS.AddRange(assemblage[i].handles.Select(x => new GH_Integer(x.neighbourObject)), new GH_Path(i));

        //    return outHS;
        //}

        #endregion

        #region old_code

        ///// <summary>
        ///// Extensive constructor for the Assemblage class (deprecated)
        ///// </summary>
        ///// <param name="AOs"></param>
        ///// <param name="pAO"></param>
        ///// <param name="startPlane"></param>
        ///// <param name="startType"></param>
        ///// <param name="heuristicsString"></param>
        ///// <param name="field"></param>
        ///// <param name="fieldThreshold"></param>
        ///// <param name="environmentMeshes"></param>
        ///// <param name="sandbox"></param>
        //public Assemblage(List<AssemblyObject> AOs, List<AssemblyObject> pAO, Plane startPlane, int startType, List<string> heuristicsString, Field field, double fieldThreshold, List<Mesh> environmentMeshes, Box sandbox)
        //{
        //    rnd = new Random();
        //    assemblyObjects = new List<AssemblyObject>();
        //    centroidsTree = new RTree();
        //    centroidsAO = new List<int>();
        //    availableObjects = new List<int>();
        //    unreachableObjects = new List<int>();
        //    // initialize environment objects
        //    SetEnvironment(environmentMeshes);
        //    // initialize sandbox
        //    if (sandbox.IsValid)
        //    {
        //        this.E_sandbox = sandbox;
        //        Transform scale = Transform.Scale(this.E_sandbox.Center, 1.2);
        //        this.E_sandbox.Transform(scale);
        //        E_sandboxCentroidsTree = new RTree();
        //        E_sandboxCentroidsAO = new List<int>();
        //        E_sandboxAvailableObjects = new List<int>();
        //        E_sandboxUnreachableObjects = new List<int>();
        //    }
        //    else
        //    {
        //        E_sandbox = Box.Empty;
        //    }
        //    // initialize Field
        //    if (field != null) this.field = field;
        //    this.fieldThreshold = fieldThreshold;
        //    // fill the unique objects set
        //    // cast input to AssemblyObject type
        //    AOSet = AOs.ToArray();
        //    // build the dictionary
        //    objectsDictionary = Utilities.BuildDictionary(AOSet, true);
        //    // fill handle types HashSet
        //    handleTypes = Utilities.BuildHandlesHashSet(AOSet);
        //    // if there is a previous Assemblage
        //    if (pAO != null && pAO.Count > 0)
        //    {
        //        // assign types to previous objects
        //        List<AssemblyObject> newTypes;
        //        assemblyObjects.AddRange(AssignType(pAO, out newTypes));
        //        // update AOset and handleTypes HashSet with new types
        //        if (newTypes.Count > 0)
        //        {
        //            List<AssemblyObject> AOsetList = AOSet.ToList();
        //            AOsetList.AddRange(newTypes);
        //            AOSet = AOsetList.ToArray();
        //            handleTypes = Utilities.BuildHandlesHashSet(AOSet);
        //        }
        //        // add previous objects to the centroids tree and check their occupancy/availability status
        //        bool inSandBox;
        //        for (int i = 0; i < assemblyObjects.Count; i++)
        //        {
        //            inSandBox = false;
        //            // add object to the centroids tree
        //            // future implementation: if object has children, insert all children centroids under the same AO index (i in this case)
        //            centroidsTree.Insert(assemblyObjects[i].referencePlane.Origin, i);
        //            centroidsAO.Add(i);
        //            // if sandbox is valid and contains origin add it to its tree
        //            if (E_sandbox.IsValid && E_sandbox.Contains(assemblyObjects[i].referencePlane.Origin))
        //            {
        //                inSandBox = true;
        //                E_sandboxCentroidsTree.Insert(assemblyObjects[i].referencePlane.Origin, i);
        //                E_sandboxCentroidsAO.Add(centroidsAO[i]); // see note above for multiple centroids
        //            }
        //            // find if the AssemblyObject is available
        //            foreach (Handle h in assemblyObjects[i].handles)
        //                if (h.occupancy == 0 && handleTypes.Contains(h.type))
        //                {
        //                    availableObjects.Add(i);
        //                    if (E_sandbox.IsValid && inSandBox)
        //                        E_sandboxAvailableObjects.Add(i);
        //                    break;
        //                }
        //        }
        //    }
        //    else
        //    {
        //        // start the assemblage with one object
        //        AssemblyObject startObject = Utilities.Clone(AOSet[startType]);
        //        startObject.Transform(Transform.PlaneToPlane(startObject.referencePlane, startPlane));
        //        assemblyObjects.Add(startObject);
        //        centroidsTree.Insert(assemblyObjects[0].referencePlane.Origin, 0);
        //        // future implementation: if object has children or multiple centroids, insert all children centroids under the same AO index (0 in this case)
        //        centroidsAO.Add(0);
        //        availableObjects.Add(0);
        //        // if sandbox is valid and contains origin add it to its tree & add according value to the boolean mask
        //        if (E_sandbox.IsValid && E_sandbox.Contains(assemblyObjects[0].referencePlane.Origin))
        //        {
        //            E_sandboxCentroidsTree.Insert(assemblyObjects[0].referencePlane.Origin, 0);
        //            E_sandboxCentroidsAO.Add(centroidsAO[0]); // see note above for multiple centroids
        //            E_sandboxAvailableObjects.Add(0);
        //        }
        //    }
        //    // initialize heuristics
        //    // heuristics MUST be initialized after filling the AOdictionary with ALL types,
        //    // even those from the previous assemblage
        //    heuristicsTree = InitHeuristics(heuristicsString);
        //    E_sequentialRuleIndex = 0; // progressive index for rule selection in sequential mode (EXPERIMENTAL)
        //    // initialize selection modes
        //    selectReceiverMode = 0;
        //    selectRuleMode = 0;
        //    // initialize other variables
        //    candidateObjects = new List<AssemblyObject>();
        //    assemblageRules = new List<string>();
        //    receiverIndexes = new List<int>();
        //    //initialize environment method
        //    envCheckMethod = EnvCollision;
        //    // check World Z-Lock
        //    checkWorldZLock = false;
        //}




        // old SelectRule code:
        //double[] values;
        //switch (mode)
        //{
        //    case 0:
        //        // random selection - chooses one candidate at random
        //        winnerIndex = (int)(rnd.NextDouble() * (candidates.Count));
        //        break;
        //    case 1:
        //        // scalar field closest with threshold - chooses candidate whose centroid closest scalar field value is closer to the threshold
        //        values = ComputeScalarField(candidates.ToArray());
        //        winnerIndex = SelectMinIndex(values);
        //        //winnerIndex = FieldScalarSearch(candidates);
        //        break;
        //    case 2:
        //        // scalar field accurate with threshold - chooses candidate whose centroid interpolated scalar field value is closer to the threshold
        //        values = ComputeScalarFieldInterpolated(candidates.ToArray());
        //        winnerIndex = SelectMinIndex(values);
        //        //winnerIndex = FieldScalarSearchAccurate(candidates);
        //        break;
        //    case 3:
        //        // vector field closest - chooses candidate whose direction has minimum angle with closest vector field point (bidirectional)
        //        values = ComputeVectorFieldBidirectional(candidates.ToArray());
        //        winnerIndex = SelectMinIndex(values);
        //        //winnerIndex = FieldVectorSearchBidirectional(candidates);
        //        break;
        //    case 4:
        //        // vector field accurate - chooses candidate whose direction has minimum angle with interpolated vector field point (bidirectional)
        //        values = ComputeVectorFieldBidirectionalInterpolated(candidates.ToArray());
        //        winnerIndex = SelectMinIndex(values);
        //        //winnerIndex = FieldVectorSearchBidirectionalAccurate(candidates);
        //        break;
        //    case 5:
        //        // density search 1 - chooses candidate with minimal bounding box volume with receiver
        //        values = ComputeBBVolume(candidates.ToArray());
        //        winnerIndex = SelectMinIndex(values);
        //        //winnerIndex = MinBBVolumeSearch(candidates, receiverIndex);
        //        break;
        //    case 6:
        //        // density search 2 - chooses candidate with minimal bounding box diagonal with receiver
        //        values = ComputeBBDiagonal(candidates.ToArray());
        //        winnerIndex = SelectMinIndex(values);
        //        //winnerIndex = MinBBDiagSearch(candidates, receiverIndex);
        //        break;
        //    case 7:
        //        // Weighted Random Choice among valid rules
        //        int[] iWeights = new int[validRulesIndexes.Count];
        //        int[] indexes = new int[validRulesIndexes.Count];
        //        for (int i = 0; i < validRulesIndexes.Count; i++)
        //        {
        //            iWeights[i] = receiverRules[validRulesIndexes[i]].iWeight;
        //            indexes[i] = i;
        //        }
        //        winnerIndex = WeightedRandomChoice(indexes, iWeights);
        //        break;
        //    case 99:
        //        // sequential Rule - tries to apply the heuristics set rules in sequence (buggy)
        //        int count = 0;
        //        while (!validRulesIndexes.Contains(E_sequentialRuleIndex) && count < 100)
        //        {
        //            E_sequentialRuleIndex = (E_sequentialRuleIndex++) % receiverRules.Count;
        //            count++;
        //        }
        //        winnerIndex = validRulesIndexes.IndexOf(E_sequentialRuleIndex);
        //        break;
        //    // . add more criteria here to find winnerIndex
        //    //
        //    default:
        //        // random selection
        //        winnerIndex = (int)(rnd.NextDouble() * (candidates.Count));
        //        break;
        //}


        //private bool EnvCollision(AssemblyObject AO)
        //{
        //    //bool result = false;

        //    foreach (MeshEnvironment mE in ExogenousSettings.environmentMeshes)
        //    {
        //        // if collision happes return true
        //        if (mE.CollisionCheck(AO.collisionMesh)) return true;
        //        // if the object is fully inside an obstacle or outside a container return true
        //        if (mE.IsPointInvalid(AO.referencePlane.Origin)) return true;
        //        //result = result || mE.IsPointInvalid(AO.referencePlane.Origin);//, AO.collisionRadius);// true if point is invalid (fully inside obstacle or outside container)
        //        //result = result || mE.CollisionCheck(AO.collisionMesh); // true if collision happens
        //    }

        //    return false;
        //    //return result;
        //}

        //private bool EnvInclusion(AssemblyObject AO)
        //{
        //    //bool result = false;

        //    foreach (MeshEnvironment mE in ExogenousSettings.environmentMeshes)
        //    {
        //        // if the object is fully inside an obstacle or outside a container return true
        //        if (mE.IsPointInvalid(AO.referencePlane.Origin)) return true;
        //        //result = result || mE.IsPointInvalid(AO.referencePlane.Origin);//, AO.collisionRadius);// true if point invalid (inside obstacle or outside container)
        //    }

        //    return false;
        //    //return result;
        //}

        ///// <summary>
        ///// Resets Exogenous parameters (deprecated)
        ///// </summary>
        ///// <param name="environmentMeshes"></param>
        ///// <param name="field"></param>
        ///// <param name="fieldThreshold"></param>
        ///// <param name="SandBox"></param>
        ///// <remarks>will be replaced by ResetSettings below</remarks>
        //public void ResetExogenous(List<MeshEnvironment> environmentMeshes, Field field, double fieldThreshold, Box SandBox)
        //{
        //    //SetEnvironment(environmentMeshes);
        //    //SetField(field);
        //    this.environmentMeshes = environmentMeshes;
        //    this.field = field;
        //    this.fieldThreshold = fieldThreshold;
        //    SetSandbox(SandBox);

        //    // reset available/unreachable objects
        //    ResetAvailableObjects();
        //}

        // will be replaced by the version with Heu and Exo
        //public void ResetSettings(List<MeshEnvironment> environmentMeshes, Field field, double fieldThreshold, int heuristicsMode, int currentHeuristics, int environmentMode, Box SandBox)
        //{
        //    this.heuristicsMode = heuristicsMode;
        //    this.currentHeuristics = currentHeuristics % RulesCount;
        //    this.environmentMeshes = environmentMeshes;
        //    this.field = field;
        //    this.fieldThreshold = fieldThreshold;

        //    SetEnvCheckMethod(environmentMode);
        //    SetSandbox(SandBox);

        //    // reset available/unreachable objects
        //    ResetAvailableObjects();
        //}

        //private bool ObstructionCheck()
        //{
        //    int last = assemblage.Count - 1;
        //    AssemblyObject sO = assemblage[last];
        //    bool obstruct = false;
        //    Line ray;
        //    int[] faceIDs;

        //    // find neighbours in Assemblage
        //    List<int> neighList = new List<int>();
        //    // collision radius is a field of AssemblyObjects
        //    centroidsTree.Search(new Sphere(sO.referencePlane.Origin, sO.collisionRadius), (object sender, RTreeEventArgs e) =>
        //    {
        //        // check and recover the AssemblyObject index related to the found centroid
        //        if (centroidsAO[e.Id] != last) neighList.Add(centroidsAO[e.Id]);
        //    });

        //    // if there are no neighbours return
        //    if (neighList.Count == 0)
        //        return obstruct;

        //    // check two-way: 
        //    // 1. object handles connected or obstructed by neighbours
        //    // 2. neighbour handles obstructed by object

        //    // scan neighbours
        //    foreach (int index in neighList)
        //    {
        //        // scan neighbour's handles
        //        for (int j = 0; j < assemblage[index].handles.Length; j++)
        //        {
        //            // if the handle is not available continue
        //            if (assemblage[index].handles[j].occupancy != 0) continue;

        //            // check for accidental handle connection
        //            bool connect = false;
        //            // scan sO handles
        //            for (int k = 0; k < sO.handles.Length; k++)
        //            {
        //                // if sO handle is not available continue
        //                if (sO.handles[k].occupancy != 0) continue;
        //                // if handles are of the same type...
        //                if (sO.handles[k].type == assemblage[index].handles[j].type)
        //                    // ...and their distance is below absolute tolerance...
        //                    if (assemblage[index].handles[j].s.Origin.DistanceToSquared(sO.handles[k].s.Origin) < tolSquared)// Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
        //                    {
        //                        // ...update handles
        //                        double sOHWeight = sO.handles[k].weight;
        //                        sO.UpdateHandle(k, 1, index, j, assemblage[index].handles[j].weight);
        //                        assemblage[index].UpdateHandle(j, 1, last, k, sOHWeight);
        //                        connect = true;
        //                        break;
        //                    }
        //            }
        //            // if a connection happened, go to next handle in neighbour
        //            if (connect) continue;

        //            // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
        //            // shoot a line from the handle
        //            ray = new Line(assemblage[index].handles[j].s.Origin - (assemblage[index].handles[j].s.ZAxis * 0.1), assemblage[index].handles[j].s.ZAxis * 1.5);

        //            // if it intercepts the last added object
        //            if (Intersection.MeshLine(sO.collisionMesh, ray, out faceIDs).Length != 0)
        //            {
        //                // change handle occupancy to -1 (occluded) and add Object index to occluded handle neighbourObject
        //                assemblage[index].handles[j].occupancy = -1;
        //                assemblage[index].handles[j].neighbourObject = last;
        //                // update Object OccludedNeighbours status
        //                assemblage[last].occludedNeighbours.Add(new int[] { index, j });
        //                // change obstruct variable status
        //                obstruct = true;
        //            }

        //        }

        //        // CHECK OBSTRUCTION OF sO HANDLES BY NEIGHBOUR
        //        for (int k = 0; k < sO.handles.Length; k++)
        //        {
        //            // if sO handle is not available continue
        //            if (sO.handles[k].occupancy != 0) continue;

        //            // shoot a line from the handle
        //            ray = new Line(sO.handles[k].s.Origin - (sO.handles[k].s.ZAxis * 0.1), sO.handles[k].s.ZAxis * 1.5);

        //            // if it intercepts the neighbour object
        //            if (Intersection.MeshLine(assemblage[index].collisionMesh, ray, out faceIDs).Length != 0)
        //            {
        //                // change handle occupancy to -1 (occluded) and add neighbour Object index to occluded handle neighbourObject
        //                sO.handles[k].occupancy = -1;
        //                sO.handles[k].neighbourObject = index;
        //                // update neighbourObject OccludedNeighbours status
        //                assemblage[index].occludedNeighbours.Add(new int[] { last, k });
        //                // change obstruct variable status
        //                obstruct = true;
        //            }
        //        }
        //    }
        //    return obstruct;
        //}

        // MOVED TO UTILITIES
        ///// <summary>
        ///// Collision Check in the assemblage for a given AssemblyObject
        ///// </summary>
        ///// <param name="sO"></param>
        ///// <returns></returns>
        //private bool CollisionCheck(AssemblyObject sO)
        //{
        //    // get first vertex as Point3d for inclusion check
        //    Point3d neighFirstVertex, sOfirstVertex = sO.offsetMesh.Vertices[0];

        //    // find neighbours in Assemblage 
        //    List<int> neighList = new List<int>();
        //    // collision radius is a field of AssemblyObjects
        //    centroidsTree.Search(new Sphere(sO.referencePlane.Origin, sO.collisionRadius), (object sender, RTreeEventArgs e) =>
        //    {
        //        // recover the AssemblyObject index related to the found centroid
        //        neighList.Add(centroidsAO[e.Id]);
        //    });

        //    // check for no neighbours
        //    if (neighList.Count == 0)
        //        return false;

        //    // check for collisions + inclusion (sender in receiver, receiver in sender)
        //    foreach (int index in neighList)
        //    {
        //        if (Intersection.MeshMeshFast(sO.offsetMesh, assemblyObjects[index].collisionMesh).Length > 0)
        //            return true;
        //        // check if sender object is inside neighbour
        //        if (assemblyObjects[index].collisionMesh.IsPointInside(sOfirstVertex, Utilities.RhinoAbsoluteTolerance, true))
        //            return true;
        //        // check if neighbour is inside sender object
        //        // get neighbour first vertex & check if it's inside
        //        neighFirstVertex = assemblyObjects[index].offsetMesh.Vertices[0];
        //        if (sO.collisionMesh.IsPointInside(neighFirstVertex, Utilities.RhinoAbsoluteTolerance, true))
        //            return true;
        //    }
        //    return false;
        //}

        ///// <summary>
        ///// OBSOLETE - Collision Check in the assemblage for a given AssemblyObject - FAST method (no inclusion check)
        ///// </summary>
        ///// <param name="sO"></param>
        ///// <returns></returns>
        //private bool CollisionCheckFast(AssemblyObject sO)
        //{
        //    // find neighbours in Assemblage 
        //    List<int> neighList = new List<int>();
        //    // collision radius is a field of AssemblyObjects
        //    centroidsTree.Search(new Sphere(sO.referencePlane.Origin, sO.collisionRadius), (object sender, RTreeEventArgs e) =>
        //    {
        //        // recover the AssemblyObject index related to the found centroid
        //        neighList.Add(centroidsAO[e.Id]);
        //    });

        //    // check for no neighbours
        //    if (neighList.Count == 0)
        //        return false;

        //    // check for collisions
        //    foreach (int index in neighList)
        //    {
        //        if (Intersection.MeshMeshFast(sO.offsetMesh, assemblyObjects[index].collisionMesh).Length > 0)
        //            return true;
        //    }
        //    return false;
        //}

        ///// <summary>
        ///// Verify list of available/unreachable objects according to current heuristics - OLD version of ResetAvailableObjects
        ///// </summary>
        //public void ResetAvailableHeuristics()
        //{
        //    // check for every available and unavailable object
        //    // - if there is a rule with their rType and the handles - they go from unavailable to available
        //    // - if there isn't a rule with their rType they go from available to unavailable

        //    List<int> newAvailables = new List<int>();
        //    List<int> newUnreachables = new List<int>();
        //    GH_Path path;


        //    // . . . scan available objects
        //    for (int i = availableObjects.Count - 1; i >= 0; i--)
        //    {
        //        // current heuristics path to search for {current heuristics; receiver type}
        //        path = new GH_Path(currentHeuristics, assemblyObjects[availableObjects[i]].type);

        //        // if object type is not in the heuristics assign as unreachable and remove from available
        //        if (!heuristicsTree.PathExists(path))
        //        {
        //            newUnreachables.Add(availableObjects[i]);
        //            availableObjects.RemoveAt(i);
        //        }
        //        else // if a path exists as receiver, check if there are rules for its free handles
        //        {
        //            bool unreachable = true;
        //            // scan its handles against current heuristics rules
        //            for (int j = 0; j < assemblyObjects[availableObjects[i]].handles.Length; j++)
        //            {
        //                foreach (Rule rule in heuristicsTree.Branch(path))
        //                {
        //                    // continue if handle is connected or occluded
        //                    if (assemblyObjects[availableObjects[i]].handles[j].occupancy != 0) continue;
        //                    // if there is at least a free handle with an available rule for it (rH is the receiving handle index)
        //                    if (rule.rH == j)
        //                    {
        //                        // activate flag and break loop
        //                        unreachable = false;
        //                        break;
        //                    }
        //                }

        //                if (unreachable)
        //                // assign as unreachable and remove from available
        //                {
        //                    newUnreachables.Add(availableObjects[i]);
        //                    availableObjects.RemoveAt(i);
        //                    break;
        //                }
        //            }
        //        }
        //    }


        //    // . . . scan unreachable objects
        //    for (int i = unreachableObjects.Count - 1; i >= 0; i--)
        //    {
        //        // current heuristics path to search for {current heuristics; receiver type}
        //        path = new GH_Path(currentHeuristics, assemblyObjects[unreachableObjects[i]].type);

        //        // if object type is in the heuristics scan its handles
        //        if (heuristicsTree.PathExists(path))
        //        {
        //            bool available = false;
        //            for (int j = 0; j < assemblyObjects[unreachableObjects[i]].handles.Length; j++)
        //            {
        //                foreach (Rule rule in heuristicsTree.Branch(path))
        //                {
        //                    // continue if handle is connected or occluded
        //                    if (assemblyObjects[unreachableObjects[i]].handles[j].occupancy != 0) continue;

        //                    // if there is at least a free handle with an available rule for it (rH is the receiving handle index)
        //                    if (rule.rH == j)
        //                    {
        //                        // activate flag and break loop
        //                        available = true;
        //                        break;
        //                    }
        //                }

        //                if (available)
        //                // assign as available and remove from unreachable
        //                {
        //                    newAvailables.Add(unreachableObjects[i]);
        //                    unreachableObjects.RemoveAt(i);
        //                    break;
        //                }
        //            }
        //        }
        //    }

        //    // add new lists to respecitve lists
        //    availableObjects.AddRange(newAvailables);
        //    unreachableObjects.AddRange(newUnreachables);

        //}
        #endregion
    }
}
