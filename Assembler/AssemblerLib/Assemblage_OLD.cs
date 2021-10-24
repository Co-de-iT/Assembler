using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

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
        /// The list of AssemblyObjects in the assemblage
        /// </summary>
        public List<AssemblyObject> assemblage;
        /// <summary>
        /// The set of unique object types
        /// </summary>
        public AssemblyObject[] AOset;
        /// <summary>
        /// The component Dictionary built from the set
        /// </summary>
        public Dictionary<string, int> componentDictionary;
        /// <summary>
        /// The environment meshes
        /// </summary>
        public List<MeshEnvironment> environmentMeshes;
        /// <summary>
        /// Sandbox for focused assemblage growth 
        /// </summary>
        public Box sandbox;
        /// <summary>
        /// Index of currently used heuristics
        /// </summary>
        public int currentHeuristics;
        /// <summary>
        /// Heuristics as a Rule DataTree
        /// </summary>
        public DataTree<Rule> heuristics;
        /// <summary>
        /// Test variable to see the temporary sender object(s) in output
        /// </summary>
        public List<AssemblyObject> candidates;
        /// <summary>
        /// Heuristics in String format - the Rule DataTree is constructed from these
        /// </summary>
        public List<string> assemblageRules;
        /// <summary>
        /// Field Threshold for scalar field methods
        /// </summary>
        public double fieldThreshold;
        /// <summary>
        /// Select receiver mode - 
        /// </summary>
        public int selectReceiverMode;
        /// <summary>
        /// Select Rule mode
        /// </summary>
        public int selectRuleMode;
        /// <summary>
        /// Heuristics mode - 0 = manual, 1 = field driven
        /// </summary>
        public int heuristicsMode;
        /// <summary>
        /// Environment mode - 0 = ignore environment objects, 1 = collision mode, 2 = inclusion mode
        /// </summary>
        public int envMode = 0;

        private int seqIndex;
        private RTree centroidsTree;
        private RTree sandboxTree;
        private List<int> availableObjects;
        private List<int> sandboxAvailableObjects;
        private Field field;
        readonly HashSet<int> handleTypes; // set of all available handle types
        readonly Random rnd;

        /// <summary>
        /// debug variables
        /// </summary>
        public int D_rInd, D_candCount, D_potRec;

        /// <summary>
        /// Extensive constructor for the IterativeAssemblage class
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
            assemblage = new List<AssemblyObject>();
            centroidsTree = new RTree();
            availableObjects = new List<int>();

            // fill the unique objects set
            // cast input to AssemblyObject type
            AOset = AO.ToArray();

            // build the dictionary
            componentDictionary = Utilities.BuildDictionary(AOset);

            // fill handle types set
            handleTypes = Utilities.BuildHandlesHashSet(AOset);

            // initialize environment objects
            SetEnvironment(environmentMeshes);

            // initialize sandbox
            if (sandbox.IsValid)
            {
                this.sandbox = sandbox;
                Transform scale = Transform.Scale(this.sandbox.Center, 1.2);
                this.sandbox.Transform(scale);
                sandboxTree = new RTree();
                sandboxAvailableObjects = new List<int>();
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
            heuristics = InitHeuristics(heuristicsString);
            seqIndex = 0; // progressive index for rule selection in sequential mode

            // initialize selection modes
            selectReceiverMode = 0;
            selectRuleMode = 0;

            // if there is no previous Assemblage
            if (pAO == null || pAO.Count == 0)
            {
                // start the assemblage with one object
                AssemblyObject startObject = new AssemblyObject(AOset[startType]);
                startObject.Transform(Transform.PlaneToPlane(startObject.referencePlane, startPlane));
                assemblage.Add(startObject);
                centroidsTree.Insert(assemblage[0].referencePlane.Origin, 0);
                availableObjects.Add(0);
                // if sandbox is valid and contains origin add it to its tree & add according value to the boolean mask
                if (sandbox.IsValid)
                {
                    bool inSandBox = sandbox.Contains(assemblage[0].referencePlane.Origin);
                    if (inSandBox)
                        sandboxTree.Insert(assemblage[0].referencePlane.Origin, 0);
                    sandboxAvailableObjects.Add(0);
                }
            }
            else // if there is a previous Assemblage
            {
                assemblage.AddRange(pAO);
                bool inSandBox;
                for (int i = 0; i < assemblage.Count; i++)
                {
                    inSandBox = false;
                    // add object to the centroids tree
                    centroidsTree.Insert(assemblage[i].referencePlane.Origin, i);
                    // if sandbox is valid and contains origin add it to its tree
                    if (sandbox.IsValid)
                    {
                        inSandBox = sandbox.Contains(assemblage[i].referencePlane.Origin);
                        if (inSandBox)
                            sandboxTree.Insert(assemblage[i].referencePlane.Origin, i);
                    }
                    // find if it is available
                    foreach (Handle h in assemblage[i].handles)
                        if (h.occupancy == 0 && handleTypes.Contains(h.type))
                        {
                            availableObjects.Add(i);
                            if (sandbox.IsValid && inSandBox)
                                sandboxAvailableObjects.Add(i);
                            break;
                        }
                }

            }
            candidates = new List<AssemblyObject>();
            assemblageRules = new List<string>();
        }

        /// <summary>
        /// Initialize Heuristics - generates Heuristics Tree
        /// </summary>
        /// <param name="heu"></param>
        /// <returns></returns>
        public DataTree<Rule> InitHeuristics(List<string> heu)
        {
            DataTree<Rule> heuT = new DataTree<Rule>();
            for (int k = 0; k < heu.Count; k++)
            {
                //          split by list of rules (,)
                string[] rComp = heu[k].Split(new[] { ',' });

                int rT, rH, rR, sT, sH;
                int w;
                for (int i = 0; i < rComp.Length; i++)
                {
                    string[] rule = rComp[i].Split(new[] { '<', '%' });
                    string[] rec = rule[0].Split(new[] { '|' });
                    string[] sen = rule[1].Split(new[] { '|' });
                    // sender and receiver component types
                    sT = componentDictionary[sen[0]];
                    rT = componentDictionary[rec[0]];
                    // sender handle index
                    sH = Convert.ToInt32(sen[1]);
                    // weight
                    w = Convert.ToInt32(rule[2]);
                    string[] rRot = rec[1].Split(new[] { '=' });
                    // receiver handle index and rotation
                    rH = Convert.ToInt32(rRot[0]);
                    rR = Convert.ToInt32(rRot[1]);

                    heuT.Add(new Rule(rT, rH, rR, sT, sH, w), new GH_Path(k, rT));
                }
            }
            return heuT;
        }

        /// <summary>
        /// Update method - the method can be customized
        /// </summary>
        public virtual void Update()
        {

            List<Rule> rRules = new List<Rule>();   // rules pertaining the receiver (once selected)
            List<int> validRules = new List<int>(); // indexes of filtered rules from rRules
            AssemblyObject newObject;
            int rInd = 0;

            while (availableObjects.Count > 0)
            {
                // . . . . . . .    1. receiver selection
                // . . . . . . .    
                // . . . . . . .    

                // this method contains the selection criteria
                int avInd = SelectReceiver(availableObjects, selectReceiverMode);

                rInd = availableObjects[avInd];
                // this function filters candidates filtering invalid results (i.e. collisions, environment)
                bool found = RetrieveCandidates(rInd, out candidates, out rRules, out validRules);

                // debug stuff
                D_rInd = rInd;
                D_candCount = candidates.Count;
                D_potRec = availableObjects.Count;

                if (found)
                    break;
                else
                    availableObjects.RemoveAt(avInd);
            }

            // if there are available candidates
            if (candidates.Count > 0)
            {
                // . . . . . . .    2. rule selection
                // . . . . . . .    
                // . . . . . . .    

                Rule rule = SelectRule(rRules, validRules, candidates, selectRuleMode, out newObject);

                // add rule to sequence as string
                assemblageRules.Add(rule.ToString());

                // add new Object to the assemblage
                AddValidObject(newObject, rule, rInd);

                // check if the last object in the assemblage obstructs other handles in the surroundings
                ObstructionCheck();

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
        /// Receiver Selection according to 3 possible criteria: random, scalar field driven, sequential (still undeveloped and buggy)
        /// </summary>
        /// <param name="availableObjects"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public virtual int SelectReceiver(List<int> availableObjects, int mode)
        {
            int avInd;
            switch (mode)
            {
                case 0:
                    // random selection among available objects
                    avInd = (int)(rnd.NextDouble() * (availableObjects.Count));
                    break;
                case 1:
                    // scalar field search
                    avInd = FieldScalarSearch(availableObjects, fieldThreshold);
                    break;

                case 99:
                    // "sequential" mode - return last added object
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
        /// <param name="rRules"></param>
        /// <param name="validRules"></param>
        /// <param name="candidates"></param>
        /// <param name="newObject">Selected new Object to add according to selected Rule</param>
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
        /// <param name="rRules"></param>
        /// <param name="validRules"></param>
        /// <param name="candidates"></param>
        /// <param name="mode"></param>
        /// <param name="newObject">new AssemblyObject to add to the assemblage</param>
        /// <returns>selected rule</returns>
        public virtual Rule SelectRule(List<Rule> rRules, List<int> validRules, List<AssemblyObject> candidates, int mode, out AssemblyObject newObject)
        {
            int winnerIndex;

            switch (mode)
            {
                case 0:
                    // random selection - chooses one candidate at random
                    winnerIndex = (int)(rnd.NextDouble() * (candidates.Count));
                    break;
                case 1:
                    // scalar field with threshold - chooses candidate whose centroid closest scalar field value is closer to the threshold
                    winnerIndex = FieldScalarSearch(candidates, fieldThreshold);
                    break;
                case 2:
                    // vector field - chooses candidate whose direction has minimum angle with closest vector field point
                    winnerIndex = FieldVectorSearch(candidates, true);
                    break;
                case 99:
                    int count = 0;
                    while (!validRules.Contains(seqIndex) && count < 100)
                    {
                        seqIndex = (seqIndex++) % rRules.Count;
                        count++;
                    }
                    winnerIndex = validRules.IndexOf(seqIndex);
                    break;
                // . add more criteria here to find winnerIndex
                //

                default:
                    // random selection
                    winnerIndex = (int)(rnd.NextDouble() * (candidates.Count));
                    break;
            }

            Rule rule = rRules[validRules[winnerIndex]];
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
        /// Retrieve candidates for assemblage given the receiver index
        /// </summary>
        /// <param name="rInd">receiver AssemblyObject index</param>
        /// <param name="candidates">List of AssemblyObject candidates</param>
        /// <param name="rRules">List of all receiver-compatible rules</param>
        /// <param name="validRules">List of indexes for valid rules</param>
        /// <returns></returns>
        public bool RetrieveCandidates(int rInd, out List<AssemblyObject> candidates, out List<Rule> rRules, out List<int> validRules)
        {
            // . find receiver object type
            int rT = assemblage[rInd].type;

            // select current heuristics - check if heuristic mode is set to field driven
            if (heuristicsMode == 1)
                currentHeuristics = field.GetClosestiWeights(assemblage[rInd].referencePlane.Origin)[0];

            // . sanity check on rules
            // it is possible, when using a custom set of rules, that an object is only used as sender
            // in this case, there will be no rules if it's picked at random as a potential receiver
            // so we return nulls

            if (!heuristics.PathExists(currentHeuristics, rT))
            {
                candidates = null;
                validRules = null;
                rRules = null;
                return false;
            }

            // if a path exists.....
            // . retrieve all rules for receiving object and properly define return variables
            validRules = new List<int>();
            candidates = new List<AssemblyObject>();
            AssemblyObject newObject;
            rRules = heuristics.Branch(currentHeuristics, rT);


            // orient all candidates around receiving object and keep track of valid indices
            // parse through all rules and filter valid ones
            for (int i = 0; i < rRules.Count; i++)
            {
                // if receiver handle isn't free skip to next rule
                if (assemblage[rInd].handles[rRules[i].rH].occupancy != 0) continue;

                // make a copy of corresponding sender type from catalog
                newObject = new AssemblyObject(AOset[rRules[i].sT]);

                // create Transformation
                Transform orient = Transform.PlaneToPlane(AOset[rRules[i].sT].handles[rRules[i].sH].s,
                    assemblage[rInd].handles[rRules[i].rH].r[rRules[i].rR]);

                // transform sender object
                newObject.Transform(orient);

                //check for environment and other objects' collision
                bool envCheck = true;
                switch (envMode)
                {
                    case 0:
                        break;
                    case 1:
                        envCheck = !EnvCollision(newObject);
                        break;
                    case 2:
                        envCheck = !EnvInclusion(newObject);
                        break;
                }
                if (envCheck)
                    if (!CollisionCheck(newObject))
                    {
                        validRules.Add(i);
                        candidates.Add(newObject);
                    }
            }

            return candidates.Count > 0;
        }

        void AddValidObject(AssemblyObject newObject, Rule rule, int rInd)
        {

            // add centroid to assemblage centroids tree
            centroidsTree.Insert(newObject.referencePlane.Origin, assemblage.Count);

            // update sender handle status method (handleIndex, occupancy, neighbourObject, neighbourHandle, weight)
            double newHWeight = newObject.handles[rule.sH].weight;
            newObject.UpdateHandle(rule.sH, 1, rInd, rule.rH, assemblage[rInd].handles[rule.rH].weight);
            // update sender handle status (occupancy, neighbourObject, neighbourHandle, weight)
            //newObject.handles[rule.sH].occupancy = 1;
            //newObject.handles[rule.sH].neighbourObject = rInd;
            //newObject.handles[rule.sH].neighbourHandle = rule.rH;
            //newObject.handles[rule.sH].weight += assemblage[rInd].handles[rule.rH].weight;

            // update receiver handle (occupancy, neighbourObject, neighbourHandle, weight)
            assemblage[rInd].UpdateHandle(rule.rH, 1, assemblage.Count, rule.sH, newHWeight);
            //assemblage[rInd].handles[rule.rH].occupancy = 1;
            //assemblage[rInd].handles[rule.rH].neighbourObject = assemblage.Count;
            //assemblage[rInd].handles[rule.rH].neighbourHandle = rule.sH;
            //assemblage[rInd].handles[rule.rH].weight += newHWeight;

            // add new object to assemblage and available objects indexes
            availableObjects.Add(assemblage.Count);
            assemblage.Add(newObject);

            // if receiving object is fully occupied (all handles either connected or occluded) remove it from the available objects
            if (assemblage[rInd].handles.Where(x => x.occupancy != 0).Sum(x => 1) == assemblage[rInd].handles.Length)
                availableObjects.Remove(rInd);
        }

        /// <summary>
        /// Select among candidates indexes by Scalar Field criteria
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public int FieldScalarSearch(List<int> candidates, double threshold)
        {
            int winner = -1;

            double diff, minDiff = double.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                diff = Math.Abs(threshold - field.GetClosestScalar(assemblage[candidates[i]].referencePlane.Origin));
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
        public int FieldScalarSearch(List<AssemblyObject> candidates, double threshold)
        {
            int winner = -1;

            double diff, minDiff = double.MaxValue;

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
        public int FieldVectorSearch(List<AssemblyObject> candidates, bool bidirectional)
        {
            int winner = -1;

            double ang, minAng = double.MaxValue;
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

        void ObstructionCheck()
        {
            int last = assemblage.Count - 1;
            AssemblyObject sO = assemblage[last];

            // find neighbours in Assemblage
            List<int> neighList = new List<int>();
            // collision radius is a field of AssemblyObjects
            centroidsTree.Search(new Sphere(sO.referencePlane.Origin, sO.collisionRadius), (object sender, RTreeEventArgs e) =>
            {
                if (e.Id != last) neighList.Add(e.Id);
            });

            // check for no neighbours
            if (neighList.Count == 0)
                return;

            // check for possible obstructions to free handles in all neighbours
            foreach (int index in neighList)
            {
                for (int j = 0; j < assemblage[index].handles.Length; j++)
                    // if the handle is available
                    if (assemblage[index].handles[j].occupancy == 0)
                    {
                        // shoot a line from the handle
                        Line ray = new Line(assemblage[index].handles[j].s.Origin - (assemblage[index].handles[j].s.ZAxis * 0.1), assemblage[index].handles[j].s.ZAxis * 1.5);
                        int[] fIDs;
                        // if it intercepts the last added object
                        if (Intersection.MeshLine(sO.collisionMesh, ray, out fIDs).Length != 0)
                        {
                            // check for accidental handle connection?
                            /*
                             parse through sender object handles and see if distance is below threshold (almost zero)
                             if handles are of the same type create connection
                             */
                            // change handle occupancy to -1 (occluded) and add Object index to occlude handle
                            assemblage[index].handles[j].occupancy = -1;
                            assemblage[index].handles[j].neighbourObject = last;
                            // update Object OccludedNeighbours status
                            assemblage[last].occludedNeighbours.Add(new int[] { index, j });
                        }
                    }
            }

        }

        /// <summary>
        /// Collision Check in the assemblage for a given AssemblyObject
        /// </summary>
        /// <param name="sO"></param>
        /// <returns></returns>
        public bool CollisionCheck(AssemblyObject sO)
        {

            // find neighbours in Assemblage (remove receving object?)
            List<int> neighList = new List<int>();
            // collision radius is a field of AssemblyObjects
            centroidsTree.Search(new Sphere(sO.referencePlane.Origin, sO.collisionRadius), (object sender, RTreeEventArgs e) =>
            {
                neighList.Add(e.Id);
            });

            // check for no neighbours
            if (neighList.Count == 0)
                return false;

            // check for collisions + distance between centroids under threshold
            foreach (int index in neighList)
            {
                if (Intersection.MeshMeshFast(sO.offsetMesh, assemblage[index].collisionMesh).Length > 0)
                    return true;
                if (sO.referencePlane.Origin.DistanceToSquared(assemblage[index].referencePlane.Origin) < 0.01)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Environment Meshes collision check
        /// </summary>
        /// <param name="sC"></param>
        /// <returns></returns>
        public bool EnvCollision(AssemblyObject sC)
        {
            bool result = false;

            foreach (MeshEnvironment mE in environmentMeshes)
                result = result || mE.CollisionCheck(sC.collisionMesh);

            return result;
        }

        /// <summary>
        /// Environment Meshes inclusion check
        /// </summary>
        /// <param name="sC"></param>
        /// <returns></returns>
        public bool EnvInclusion(AssemblyObject sC)
        {
            bool result = false;

            foreach (MeshEnvironment mE in environmentMeshes)
                //result = result || IsPointInside(mE.mesh, sC.referencePlane.Origin, sC.collisionRadius);
                // when new version of library is compiled    
                result = result || mE.IsPointInside(sC.referencePlane.Origin, sC.collisionRadius);
            return result;
        }

        /// <summary>
        /// Sets environment Meshes
        /// </summary>
        /// <param name="meshes"></param>
        public void SetEnvironment(List<Mesh> meshes)
        {
            environmentMeshes = meshes.Select(m => new MeshEnvironment(m)).ToList();
        }

        /// <summary>
        /// Sets Assemblage Field
        /// </summary>
        /// <param name="field"></param>
        public void SetField(Field field)
        {
            this.field = field;
        }

        /// <summary>
        /// Sets Assemblage Heuristics
        /// </summary>
        /// <param name="heuristicsString"></param>
        public void SetHeuristics(List<string> heuristicsString)
        {
            heuristics = InitHeuristics(heuristicsString);
        }

        void ResetSandboxRtree()
        {
            sandboxTree = new RTree();

            for (int i = 0; i < assemblage.Count; i++)
                if (sandbox.Contains(assemblage[i].referencePlane.Origin)) sandboxTree.Insert(assemblage[i].referencePlane.Origin, i);
        }

        /// <summary>
        /// Sets Sandbox geometry
        /// </summary>
        /// <param name="sandbox"></param>
        public void SetSandbox(Box sandbox)
        {
            if (sandbox.IsValid)
            {
                this.sandbox = sandbox;
                Transform scale = Transform.Scale(this.sandbox.Center, 1.2);
                this.sandbox.Transform(scale);
                ResetSandboxRtree();
            }

        }

        #region debug_functions

        // all debug functions start with a "D_"

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public GH_Integer[] D_ExtractAvailableObjects()
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
    }
}
