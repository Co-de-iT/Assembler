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
    /// </summary>
    public class Assemblage
    {
        /*
         assemblage external parameters:
         . previous assemblage (list of AssmblyObject to start from)
         . starting plane
         . starting component type
         . starting heuristics index (when more than one is provided)
         . environment objects
         . fields (scalar/vector/weights for rules)
         */
        /// <summary>
        /// The list of <see cref="AssemblyObject"/>s in the assemblage
        /// </summary>
        public List<AssemblyObject> assemblyObjects;
        /// <summary>
        /// The set of unique object types
        /// </summary>
        public AssemblyObject[] AOset;
        /// <summary>
        /// The objects Dictionary built from the set
        /// </summary>
        public Dictionary<string, int> objectsDictionary;
        /// <summary>
        /// The environment meshes
        /// </summary>
        public List<MeshEnvironment> environmentMeshes;
        /// <summary>
        /// Sandbox for focused assemblage growth 
        /// </summary>
        public Box E_sandbox;
        /// <summary>
        /// Index of currently used heuristics
        /// </summary>
        public int currentHeuristics;
        /// <summary>
        /// Heuristics as a Rule DataTree
        /// </summary>
        public DataTree<Rule> heuristicsTree;
        /// <summary>
        /// Test variable to see the temporary sender object(s) in output
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
        /// Field Threshold for scalar field methods
        /// </summary>
        public double fieldThreshold;
        /// <summary>
        /// Select receiver mode
        /// </summary>
        public int selectReceiverMode;
        /// <summary>
        /// Select Rule mode
        /// </summary>
        public int selectRuleMode;
        /// <summary>
        /// <list type="bullet">
        /// <item>0 - manual via <see cref="currentHeuristics"/> parameter</item>
        /// <item>1 - <see cref="Field"/> driven via iWeights</item>
        /// </list>
        /// </summary>
        public int heuristicsMode;
        ///// <summary>
        ///// Delegate type for collision method choice (between fast and accurate) -- NOT USED RIGHT NOW
        ///// </summary>
        ///// <param name="sO"></param>
        ///// <returns></returns>
        //public delegate bool CollisionMethod(AssemblyObject sO);
        /// <summary>
        /// Delegate type for environment check function choice (between collision and inclusion)
        /// </summary>
        /// <param name="sO"></param>
        /// <returns></returns>
        public delegate bool EnvCheckMethod(AssemblyObject sO);
        /// <summary>
        /// if True, forces candidates to have their own Z axis parallel to the World Z axis (fixed up)
        /// </summary>
        public bool checkWorldZLock;

        //private CollisionMethod collisionMethod;
        private EnvCheckMethod envCheckMethod;

        //internal variables (E_ stands for Experimental feature)
        internal int E_sequentialRuleIndex; // progressive index for rule selection in sequential mode
        internal RTree centroidsTree;
        internal RTree E_sandboxCentroidsTree;
        internal List<int> centroidsAO; // list of centroid/AssemblyObject correspondances
        internal List<int> E_sandboxCentroidsAO; // list of sandbox centroid/AssemblyObject correspondances
        internal List<int> availableObjects;
        internal List<int> unreachableObjects;
        internal List<int> E_sandboxAvailableObjects;
        internal List<int> E_sandboxUnreachableObjects;
        internal Field field;
        internal readonly HashSet<int> handleTypes; // set of all available handle types
        internal readonly Random rnd;

        /// <summary>
        /// debug variables
        /// </summary>
        // public int D_rInd, D_candCount, D_potRec;

        /// <summary>
        /// Extensive constructor for the Assemblage class
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="pAO"></param>
        /// <param name="field"></param>
        /// <param name="fieldThreshold"></param>
        /// <param name="heuristicsString"></param>
        /// <param name="startPlane"></param>
        /// <param name="startType"></param>
        /// <param name="environmentMeshes"></param>
        /// <param name="sandbox"></param>
        public Assemblage(List<AssemblyObject> AO, List<AssemblyObject> pAO, Field field, double fieldThreshold, List<string> heuristicsString, Plane startPlane, int startType, List<Mesh> environmentMeshes, Box sandbox)
        {

            rnd = new Random();
            assemblyObjects = new List<AssemblyObject>();
            centroidsTree = new RTree();
            centroidsAO = new List<int>();
            availableObjects = new List<int>();
            unreachableObjects = new List<int>();

            // fill the unique objects set
            // cast input to AssemblyObject type
            AOset = AO.ToArray();

            // build the dictionary
            objectsDictionary = Utilities.BuildDictionary(AOset, true);

            // fill handle types set
            handleTypes = Utilities.BuildHandlesHashSet(AOset);

            // initialize environment objects
            SetEnvironment(environmentMeshes);

            // initialize sandbox
            if (sandbox.IsValid)
            {
                this.E_sandbox = sandbox;
                Transform scale = Transform.Scale(this.E_sandbox.Center, 1.2);
                this.E_sandbox.Transform(scale);
                E_sandboxCentroidsTree = new RTree();
                E_sandboxCentroidsAO = new List<int>();
                E_sandboxAvailableObjects = new List<int>();
                E_sandboxUnreachableObjects = new List<int>();
            }
            else
            {
                sandbox = Box.Empty;
            }

            // initialize Field
            if (field != null) this.field = field;
            this.fieldThreshold = fieldThreshold;

            // initialize heuristics
            //currentHeuristics = 0; // this can be changed to use multilple heuristics (i.e. attractor-based etc.)
            heuristicsTree = InitHeuristics(heuristicsString);
            E_sequentialRuleIndex = 0; // progressive index for rule selection in sequential mode

            // initialize selection modes
            selectReceiverMode = 0;
            selectRuleMode = 0;

            // if there is no previous Assemblage
            if (pAO == null || pAO.Count == 0)
            {
                // start the assemblage with one object
                AssemblyObject startObject = Utilities.Clone(AOset[startType]);//new AssemblyObject(AOset[startType]);
                startObject.Transform(Transform.PlaneToPlane(startObject.referencePlane, startPlane));
                assemblyObjects.Add(startObject);
                centroidsTree.Insert(assemblyObjects[0].referencePlane.Origin, 0);
                // future implementation: if object has children or multiple centroids, insert all children centroids under the same AO index (0 in this case)
                centroidsAO.Add(0);
                availableObjects.Add(0);
                // if sandbox is valid and contains origin add it to its tree & add according value to the boolean mask
                if (sandbox.IsValid && sandbox.Contains(assemblyObjects[0].referencePlane.Origin))
                {
                    E_sandboxCentroidsTree.Insert(assemblyObjects[0].referencePlane.Origin, 0);
                    E_sandboxCentroidsAO.Add(centroidsAO[0]); // see note above for multiple centroids
                    E_sandboxAvailableObjects.Add(0);
                }
            }
            else // if there is a previous Assemblage
            {
                assemblyObjects.AddRange(AssignType(pAO));
                bool inSandBox;
                for (int i = 0; i < assemblyObjects.Count; i++)
                {
                    inSandBox = false;
                    // add object to the centroids tree
                    // future implementation: if object has children, insert all children centroids under the same AO index (i in this case)
                    centroidsTree.Insert(assemblyObjects[i].referencePlane.Origin, i);
                    centroidsAO.Add(i);
                    // if sandbox is valid and contains origin add it to its tree
                    if (sandbox.IsValid && sandbox.Contains(assemblyObjects[i].referencePlane.Origin))
                    {
                        inSandBox = true;
                        E_sandboxCentroidsTree.Insert(assemblyObjects[i].referencePlane.Origin, i);
                        E_sandboxCentroidsAO.Add(centroidsAO[i]); // see note above for multiple centroids
                    }
                    // find if it is available
                    foreach (Handle h in assemblyObjects[i].handles)
                        if (h.occupancy == 0 && handleTypes.Contains(h.type))
                        {
                            availableObjects.Add(i);
                            if (sandbox.IsValid && inSandBox)
                                E_sandboxAvailableObjects.Add(i);
                            break;
                        }
                }

            }
            candidateObjects = new List<AssemblyObject>();
            assemblageRules = new List<string>();
            receiverIndexes = new List<int>();

            //initialize collision/environment methods (Accurate method is default)
            //collisionMethod = CollisionCheck;
            envCheckMethod = EnvCollision;

            checkWorldZLock = false;
        }

        private List<AssemblyObject> AssignType(List<AssemblyObject> pAO)
        {
            // previous objects are checked against the dictionary
            // - if they are not part of the existing set, a new type is added

            List<AssemblyObject> pAOTyped = new List<AssemblyObject>();
            int newType = objectsDictionary.Count;
            for (int i = 0; i < pAO.Count; i++)
            {
                // if object name isn't present in dictionary
                if (!objectsDictionary.ContainsKey(pAO[i].name))
                {
                    // new type identified - add to dictionary
                    objectsDictionary.Add(pAO[i].name, newType);
                    newType++;
                }
                pAO[i].type = objectsDictionary[pAO[i].name];
                // add a copy of the original object to the list
                //pAOTyped.Add(pAO[i].DuplicateWithConnectivity());
                pAOTyped.Add(Utilities.CloneWithConnectivity(pAO[i]));
            }

            return pAOTyped;
        }

        internal DataTree<Rule> InitHeuristics(List<string> heu)
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
                    //rR = Convert.ToInt32(rRot[1]);
                    rRA = Convert.ToDouble(rRot[1]);
                    rR = AOset[rT].handles[rH].rDictionary[rRA]; // using rotations

                    heuT.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, w), new GH_Path(k, rT));
                }
            }
            return heuT;
        }

        /// <summary>
        /// Update method
        /// 
        /// Update is composed by these steps:
        /// . receiver selection (where do I add the next one?)
        /// . rule selection (what do I add and how?) and new object addition to the assemblage
        /// 
        /// The method can be customized with an override.
        /// </summary>
        public virtual void Update()
        {

            /*
             In future implementations, add the possibility to implement the opposite logic:

             . start from the sender type
             . look for all possible candidates among the existing assemblage free handles and relative rules
             . filter selection according to criteria and add winner

            Since it scans the entire assemblage, the implementation of the SandBox is a critical prerequisite.

            Another thought:

            . try to make an assemblage given a fixed list of AssemblyObject types and their respective count
            . try combinations until exhaustion of list, also multiple times (given n. of attempts or target condition)
            . save each attempt to disk (including list of used rules)
             */

            List<Rule> rRules = new List<Rule>();   // rules pertaining the receiver (once selected)
            List<int> validRules = new List<int>(); // indexes of filtered rules from rRules
            AssemblyObject newObject;
            int rInd = 0;

            while (availableObjects.Count > 0)
            {
                // . . . . . . .    1. receiver selection
                // . . . . . . .    
                // . . . . . . .    

                // this method contains the selection criteria for the receiver
                int availableIndex = SelectReceiver(availableObjects, selectReceiverMode);

                rInd = availableObjects[availableIndex];
                // this function sifts candidates filtering invalid results (i.e. collisions, environment)
                bool found = RetrieveCandidates(rInd, out rRules, out validRules, out candidateObjects);

                // debug stuff
                //D_rInd = rInd;
                //D_candCount = candidateObjects == null ? 0 : candidateObjects.Count;
                //D_potRec = availableObjects.Count;

                if (found)
                    break;
                else
                {
                    // if an object cannot receive candidates, it is marked as unreachable (neither fully occupied nor occluded, yet nothing can be added to it)
                    unreachableObjects.Add(rInd);
                    availableObjects.RemoveAt(availableIndex);
                }
            }

            // if there are available candidates
            // this condition is not redundant as the loop above might end by exhausting the list of available objects without finding a candidate
            if (candidateObjects.Count > 0)
            {
                // . . . . . . .    2. rule selection
                // . . . . . . .    
                // . . . . . . .    

                Rule rule = SelectRule(rInd, rRules, validRules, candidateObjects, selectRuleMode, out newObject);

                // add rule to sequence as string
                assemblageRules.Add(rule.ToString());

                // add receiver object index to list
                receiverIndexes.Add(rInd);

                // add new Object to the assemblage
                AddValidObject(newObject, rule, rInd);

                // check if the last object in the assemblage obstructs other handles in the surroundings
                // or its handles are obstructed by other objects
                Utilities.ObstructionCheckAssemblage(this, assemblyObjects.Count - 1);

            }

        }

        /// <summary>
        /// A virtual method for customizing receiver selection criteria
        /// </summary>
        /// <param name="availableObjects"></param>
        /// <returns>index of selected receiver</returns>
        public virtual int SelectReceiver(List<int> availableObjects)
        {
            return availableObjects[0];
        }

        /// <summary>
        /// Receiver Selection according to a set possible criteria: 
        /// <list type="bullet">
        /// <item>0 - random (default)</item>
        /// <item>1 - scalar field driven (fast) - <see cref="FieldScalarSearch(List{int}, double, bool)"/></item>
        /// <item>2 - scalar field driven (accurate) - <see cref="FieldScalarSearch(List{int}, double, bool)"/></item>
        /// <item>3 - minimum sum weight around candidate (aka receiver density) - <see cref="ReceiverDensitySearch(List{int})"/></item>
        /// <item>... (more to come)</item>
        /// <item>99 - sequential (still undeveloped and buggy)</item>
        /// </list>
        /// </summary>
        /// <param name="availableObjects">list of available <see cref="AssemblyObject"/>s in the Assemblage</param>
        /// <param name="selectReceiverMode">integer to set receiver mode selection</param>
        /// <returns>The index of the available <see cref="AssemblyObject"/> selected as a receiver</returns>
        public virtual int SelectReceiver(List<int> availableObjects, int selectReceiverMode)
        {
            int avInd;
            switch (selectReceiverMode)
            {
                case 0:
                    // random selection among available objects
                    avInd = (int)(rnd.NextDouble() * (availableObjects.Count));
                    break;
                case 1:
                    // scalar field search - fast (closest Field point)
                    avInd = FieldScalarSearch(availableObjects, fieldThreshold, false);
                    break;
                case 2:
                    // scalar field search - accurate (interpolated values)
                    avInd = FieldScalarSearch(availableObjects, fieldThreshold, true);
                    break;
                case 3:
                    // minimum sum weight around candidate
                    avInd = ReceiverDensitySearch(availableObjects);
                    break;

                case 99:
                    // "sequential" mode - return last available object in the list
                    avInd = availableObjects.Count - 1;
                    break;
                // add more criteria here (return an avInd)
                // density driven
                // component weight driven
                // ....

                default:
                    // random selection among available objects
                    avInd = (int)(rnd.NextDouble() * (availableObjects.Count));
                    break;
            }

            return avInd;
        }

        /// <summary>
        /// A virtual method for customizing rule selection criteria
        /// </summary>
        /// <param name="rRules">List of <see cref="Rule"/>s admitted by the receiver</param>
        /// <param name="validRules">Indices of valid <see cref="Rule"/>s (candidates that do not collide with existing assemblage and/or environment obstacles)</param>
        /// <param name="candidates">List of candidate <see cref="AssemblyObject"/>s</param>
        /// <param name="newObject">Selected new <see cref="AssemblyObject"/> to add according to the selected Rule</param>
        /// <returns>The selected Rule</returns>
        public virtual Rule SelectRule(List<Rule> rRules, List<int> validRules, List<AssemblyObject> candidates, out AssemblyObject newObject)
        {
            // winnerIndex is set to 0 at the moment, but this method is barely a template
            int winnerIndex = 0;

            Rule rule = rRules[validRules[winnerIndex]];
            // new Object is found
            newObject = candidates[winnerIndex];

            return rule;
        }

        /// <summary>
        /// Rule selection according to 3 possible criteria: random, scalar field search, vector field search
        /// </summary>
        /// <param name="receiverIndex">Index of the receiver <see cref="AssemblyObject"/></param>
        /// <param name="receiverRules">List of <see cref="Rule"/>s admitted by the receiver</param>
        /// <param name="validRules">Indices of valid <see cref="Rule"/>s (candidates that do not collide with existing assemblage and/or environment obstacles)</param>
        /// <param name="candidates">List of candidates <see cref="AssemblyObject"/>s</param>
        /// <param name="mode">integer for selection mode</param>
        /// <param name="newObject">new AssemblyObject to add to the assemblage</param>
        /// <returns>selected rule</returns>
        public virtual Rule SelectRule(int receiverIndex, List<Rule> receiverRules, List<int> validRules, List<AssemblyObject> candidates, int mode, out AssemblyObject newObject)
        {
            int winnerIndex;

            switch (mode)
            {
                case 0:
                    // random selection - chooses one candidate at random
                    winnerIndex = (int)(rnd.NextDouble() * (candidates.Count));
                    break;
                case 1:
                    // scalar field fast with threshold - chooses candidate whose centroid closest scalar field value is closer to the threshold
                    winnerIndex = FieldScalarSearch(candidates, fieldThreshold, false);
                    break;
                case 2:
                    // scalar field accurate with threshold - chooses candidate whose centroid interpolated scalar field value is closer to the threshold
                    winnerIndex = FieldScalarSearch(candidates, fieldThreshold, true);
                    break;
                case 3:
                    // vector field fast - chooses candidate whose direction has minimum angle with closest vector field point
                    winnerIndex = FieldVectorSearch(candidates, true, false);
                    break;
                case 4:
                    // vector field accurate - chooses candidate whose direction has minimum angle with interpolated vector field point
                    winnerIndex = FieldVectorSearch(candidates, true, true);
                    break;
                case 5:
                    // density search 1 - chooses candidate with minimal bounding box volume with receiver
                    winnerIndex = MinBBVolumeSearch(candidates, receiverIndex);
                    break;
                case 6:
                    // density search 2 - chooses candidate with minimal bounding box diagonal with receiver
                    winnerIndex = MinBBDiagSearch(candidates, receiverIndex);
                    break;
                case 7:
                    // Weighted Random Choice among valid rules
                    List<int> iWeights = new List<int>();
                    List<int> indexes = new List<int>();
                    for (int i = 0; i < validRules.Count; i++)
                    {
                        iWeights.Add(receiverRules[validRules[i]].iWeight);
                        indexes.Add(i);
                    }
                    winnerIndex = WeightedRandomChoice(indexes, iWeights);
                    break;
                case 99:
                    int count = 0;
                    while (!validRules.Contains(E_sequentialRuleIndex) && count < 100)
                    {
                        E_sequentialRuleIndex = (E_sequentialRuleIndex++) % receiverRules.Count;
                        count++;
                    }
                    winnerIndex = validRules.IndexOf(E_sequentialRuleIndex);
                    break;
                // . add more criteria here to find winnerIndex
                //

                default:
                    // random selection
                    winnerIndex = (int)(rnd.NextDouble() * (candidates.Count));
                    break;
            }

            Rule rule = receiverRules[validRules[winnerIndex]];
            // new Object is found
            newObject = candidates[winnerIndex];

            return rule;
        }

        /// <summary>
        /// Performs a Weighted Random Choice given a List of weights
        /// </summary>
        /// <param name="weights"></param>
        /// <returns>index of the selected weight</returns>
        public int WeightedRandomChoiceIndex(List<int> weights)
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
        /// Performs a Weighted Random Choice on a List of data and corresponding weights
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <param name="weights"></param>
        /// <returns>the selected value</returns>
        public T WeightedRandomChoice<T>(List<T> values, List<int> weights)
        {
            return values[WeightedRandomChoiceIndex(weights)];
        }

        /// <summary>
        /// Retrieve Candidates based on receiver index
        /// </summary>
        /// <param name="receiverIndex">Index of the receiver <see cref="AssemblyObject"/></param>
        /// <param name="receiverRules">List of corresponding <see cref="Rule"/>s</param>
        /// <param name="validRules">Indices of valid <see cref="Rule"/>s (candidates that do not collide with existing assemblage and/or environment obstacles)</param>
        /// <param name="candidates">List of candidates <see cref="AssemblyObject"/>s</param>
        /// <returns>True if at least one suitable candidate has been found, False otherwise</returns>
        public bool RetrieveCandidates(int receiverIndex, out List<Rule> receiverRules, out List<int> validRules, out List<AssemblyObject> candidates)
        {
            // . find receiver object type
            int rT = assemblyObjects[receiverIndex].type;

            // select current heuristics - check if heuristic mode is set to field driven
            // in that case, sample the closest field value and set heuristics accordingly
            if (heuristicsMode == 1)
                currentHeuristics = field.GetClosestiWeights(assemblyObjects[receiverIndex].referencePlane.Origin)[0];

            // . sanity check on rules
            // it is possible, when using a custom set of rules, that an object is only used as sender
            // or it is not included in the selected heuristics. In such cases, there will be no associated rules
            // if it's picked at random as a potential receiver, so we return empties

            if (!heuristicsTree.PathExists(currentHeuristics, rT))
            {
                candidates = new List<AssemblyObject>();
                validRules = new List<int>();
                receiverRules = new List<Rule>();
                return false;
            }

            // if a path exists.....
            // . retrieve all rules for receiving object and properly define return variables
            validRules = new List<int>();
            candidates = new List<AssemblyObject>();
            AssemblyObject newObject;
            receiverRules = heuristicsTree.Branch(currentHeuristics, rT);

            // orient all candidates around receiving object and keep track of valid indices
            // parse through all rules and filter valid ones
            for (int i = 0; i < receiverRules.Count; i++)
            {
                // if receiver handle isn't free skip to next rule
                if (assemblyObjects[receiverIndex].handles[receiverRules[i].rH].occupancy != 0) continue;

                // make a copy of corresponding sender type from catalog
                newObject = Utilities.Clone(AOset[receiverRules[i].sT]);// new AssemblyObject(AOset[receiverRules[i].sT]);

                // create Transformation
                Transform orient = Transform.PlaneToPlane(AOset[receiverRules[i].sT].handles[receiverRules[i].sH].sender,
                    assemblyObjects[receiverIndex].handles[receiverRules[i].rH].receivers[receiverRules[i].rR]);

                // transform sender object
                newObject.Transform(orient);

                // verify environment and object collisions
                if (!envCheckMethod(newObject))
                    //if (!collisionMethod(newObject))
                    if (!Utilities.CollisionCheckAssemblage(this, newObject))
                    //if (!Utilities.CollisionCheckAssemblageParallel(this, newObject))
                    {
                        // if absolute Z lock is true...
                        if (checkWorldZLock)
                        {
                            // ...perform that check too
                            if (Utilities.AbsoluteZCheck(newObject))
                            {
                                validRules.Add(i);
                                candidates.Add(newObject);
                            }
                        }
                        // otherwise add directly
                        else
                        {
                            validRules.Add(i);
                            candidates.Add(newObject);

                        }
                    }
            }

            return candidates.Count > 0;
        }

        public void AddValidObject(AssemblyObject newObject, Rule rule, int rInd)
        {
            // add centroid to assemblage centroids tree
            // future implementation: if object has children, insert all children centroids under the same AO index (assemblage.Count in this case)
            centroidsTree.Insert(newObject.referencePlane.Origin, assemblyObjects.Count);
            centroidsAO.Add(assemblyObjects.Count);

            // update sender handle status (occupancy, neighbourObject, neighbourHandle, weight)
            // preserve initial weight
            double newHWeight = newObject.handles[rule.sH].weight;
            newObject.UpdateHandle(rule.sH, 1, rInd, rule.rH, assemblyObjects[rInd].handles[rule.rH].weight);

            // update receiver handle status (occupancy, neighbourObject, neighbourHandle, weight)
            assemblyObjects[rInd].UpdateHandle(rule.rH, 1, assemblyObjects.Count, rule.sH, newHWeight);

            // add new object to assemblage and available objects indexes
            availableObjects.Add(assemblyObjects.Count);
            assemblyObjects.Add(newObject);

            // if receiving object is fully occupied (all handles either connected or occluded) remove it from the available objects
            if (assemblyObjects[rInd].handles.Where(x => x.occupancy != 0).Sum(x => 1) == assemblyObjects[rInd].handles.Length)
                availableObjects.Remove(rInd);
        }

        private int MinBBVolumeSearch(List<AssemblyObject> candidates, int rInd)
        {
            int winner = -1;

            BoundingBox bBox;
            double bVol, minVol = double.MaxValue;
            int minInd = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                bBox = assemblyObjects[rInd].collisionMesh.GetBoundingBox(false);
                bBox.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                bVol = bBox.Volume;
                if (bVol < minVol)
                {
                    minVol = bVol;
                    minInd = i;
                }
            }

            winner = minInd;

            return winner;
        }

        private int MinBBDiagSearch(List<AssemblyObject> candidates, int rInd)
        {
            int winner = -1;

            BoundingBox bBox;
            double bDiag, minDiag = double.MaxValue;
            int minInd = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                bBox = assemblyObjects[rInd].collisionMesh.GetBoundingBox(false);
                bBox.Union(candidates[i].collisionMesh.GetBoundingBox(false));
                bDiag = bBox.Diagonal.Length;
                if (bDiag < minDiag)
                {
                    minDiag = bDiag;
                    minInd = i;
                }
            }

            winner = minInd;

            return winner;
        }

        private int ReceiverDensitySearch(List<int> candidates)
        {
            int winner = -1;
            int maxInd = 0;
            double density, maxDensity = double.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                // search for neighbour objects in radius
                density = 0;
                centroidsTree.Search(new Sphere(assemblyObjects[candidates[i]].referencePlane.Origin,
                   assemblyObjects[candidates[i]].collisionRadius), (s, e) =>
                   {
                       density += assemblyObjects[centroidsAO[e.Id]].weight;
                   });
                if (density > maxDensity)
                {
                    maxDensity = density;
                    maxInd = i;
                }
            }

            winner = maxInd;

            return winner;
        }

        /// <summary>
        /// Select among candidates indexes by Scalar Field criteria
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        private int FieldScalarSearch(List<int> candidates, double threshold, bool accurate)
        {
            int winner = -1;

            double diff, minDiff = double.MaxValue;

            if (accurate)
                for (int i = 0; i < candidates.Count; i++)
                {
                    diff = Math.Abs(threshold - field.GetInterpolatedScalar(assemblyObjects[candidates[i]].referencePlane.Origin));
                    if (diff < minDiff)
                    {
                        winner = i;
                        minDiff = diff;
                    }
                }
            else
                for (int i = 0; i < candidates.Count; i++)
                {
                    diff = Math.Abs(threshold - field.GetClosestScalar(assemblyObjects[candidates[i]].referencePlane.Origin));
                    if (diff < minDiff)
                    {
                        winner = i;
                        minDiff = diff;
                    }
                }

            return winner;
        }

        /// <summary>
        /// Select among candidates AssemblyObjects by Scalar Field criteria
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public int FieldScalarSearch(List<AssemblyObject> candidates, double threshold, bool accurate)
        {
            int winner = -1;

            double diff, minDiff = double.MaxValue;

            if (accurate)
                for (int i = 0; i < candidates.Count; i++)
                {
                    diff = Math.Abs(threshold - field.GetInterpolatedScalar(candidates[i].referencePlane.Origin));
                    if (diff < minDiff)
                    {
                        winner = i;
                        minDiff = diff;
                    }
                }
            else
                for (int i = 0; i < candidates.Count; i++)
                {
                    diff = Math.Abs(threshold - field.GetClosestScalar(candidates[i].referencePlane.Origin));
                    if (diff < minDiff)
                    {
                        winner = i;
                        minDiff = diff;
                    }
                }

            return winner;
        }

        /// <summary>
        /// Select among candidates AssemblyObjects by Vector Field criteria
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="bidirectional"></param>
        /// <returns></returns>
        public int FieldVectorSearch(List<AssemblyObject> candidates, bool bidirectional, bool accurate)
        {
            int winner = -1;

            double ang, minAng = double.MaxValue;
            if (accurate)
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!bidirectional)
                        ang = Vector3d.VectorAngle(candidates[i].direction, field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                    else
                        ang = 1 - Math.Abs(candidates[i].direction * field.GetInterpolatedVector(candidates[i].referencePlane.Origin));
                    if (ang < minAng)
                    {
                        winner = i;
                        minAng = ang;
                    }
                }
            else
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!bidirectional)
                        ang = Vector3d.VectorAngle(candidates[i].direction, field.GetClosestVector(candidates[i].referencePlane.Origin));
                    else
                        ang = 1 - Math.Abs(candidates[i].direction * field.GetClosestVector(candidates[i].referencePlane.Origin));
                    if (ang < minAng)
                    {
                        winner = i;
                        minAng = ang;
                    }
                }

            return winner;
        }

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


        private bool EnvCollision(AssemblyObject AO)
        {
            bool result = false;

            foreach (MeshEnvironment mE in environmentMeshes)
            {
                result = result || mE.IsPointInside(AO.referencePlane.Origin, AO.collisionRadius);// true if point inside obstacle (or outside container)
                result = result || mE.CollisionCheck(AO.collisionMesh); // true if collision happens
            }

            return result;
        }

        private bool EnvInclusion(AssemblyObject AO)
        {
            bool result = false;

            foreach (MeshEnvironment mE in environmentMeshes)
                result = result || mE.IsPointInside(AO.referencePlane.Origin, AO.collisionRadius);// true if point inside obstacle (or outside container)
            return result;
        }

        private void SetEnvironment(List<Mesh> meshes)
        {
            environmentMeshes = meshes.Select(m => new MeshEnvironment(m)).ToList();
        }

        private void SetField(Field field)
        {
            this.field = field;
        }

        /// <summary>
        /// Sets Assemblage Heuristics
        /// </summary>
        /// <param name="heuristicsString"></param>
        public void SetHeuristics(List<string> heuristicsString)
        {
            heuristicsTree = InitHeuristics(heuristicsString);
        }

        /// <summary>
        /// Resets Exogenous parameters
        /// </summary>
        /// <param name="environMentMeshes"></param>
        /// <param name="field"></param>
        /// <param name="fieldThreshold"></param>
        /// <param name="SandBox"></param>
        public void ResetExogenous(List<Mesh> environMentMeshes, Field field, double fieldThreshold, Box SandBox)
        {
            SetEnvironment(environMentMeshes);
            SetField(field);
            this.fieldThreshold = fieldThreshold;
            SetSandbox(SandBox);

            // reset available/unreachable objects
            ResetAvailableObjects();
        }

        /// <summary>
        /// Sets Sandbox geometry
        /// </summary>
        /// <param name="sandbox"></param>
        public void SetSandbox(Box sandbox)
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
        /// <param name="environmentMode"></param>
        public void SetEnvCheckMethod(int environmentMode)
        {
            switch (environmentMode)
            {
                case 0:
                    envCheckMethod = (AssemblyObject sO) => { return false; }; // anonymous function (avoids env check entirely)
                    break;
                case 1:
                    envCheckMethod = EnvCollision;
                    break;
                case 2:
                    envCheckMethod = EnvInclusion;
                    break;
                default:
                    envCheckMethod = EnvCollision;
                    break;
            }

        }

        private void ResetSandboxRtree()
        {
            E_sandboxCentroidsTree = new RTree();
            // create List of centroid correspondance with their AO

            centroidsTree.Search(E_sandbox.BoundingBox, (object sender, RTreeEventArgs e) =>
            {
                // recover the AssemblyObject centroid related to the found centroid
                E_sandboxCentroidsTree.Insert(assemblyObjects[centroidsAO[e.Id]].referencePlane.Origin, e.Id);
            });

            //for (int i = 0; i < assemblage.Count; i++)
            //    if (sandbox.Contains(assemblage[i].referencePlane.Origin)) sandboxTree.Insert(assemblage[i].referencePlane.Origin, i);
        }

        /// <summary>
        /// Verify list of available/unreachable objects according to current environment and heuristics
        /// </summary>
        public void ResetAvailableObjects()
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
                if (heuristicsMode == 1)
                    currentHeuristics = field.GetClosestiWeights(assemblyObjects[availableObjects[i]].referencePlane.Origin)[0];

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
                if (envCheckMethod(assemblyObjects[availableObjects[i]]))
                {
                    unreachableObjects.Add(availableObjects[i]);
                    availableObjects.RemoveAt(i);
                }
            }

        }

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

        #region debug_functions

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
        /// <summary>
        /// Verify list of available/unreachable objects according to current heuristics - OLD version of ResetAvailableObjects
        /// </summary>
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
