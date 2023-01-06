using Grasshopper;
using Grasshopper.GUI.Gradient;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace AssemblerLib
{
    /// <summary>
    /// **DEPRECATED** - A static Utilities class grouping some useful fields and methods
    /// </summary>
    /// <remarks>**DEPRECATED** - use the <see cref="Utils"/> namespace and classes instead</remarks>
    [Obsolete("Deprecated. Use AssemblerLib.Utils namespace and classes instead")]
    public static class Utilities
    {
        internal const double ObstructionToleranceMultiplier = 5.0;
        internal const double ObstructionRayLength = 1.5;
        internal const double SafeScaleMultiplier = 1.2;
        /// <summary>
        /// Tolerance from Rhino file
        /// </summary>
        internal static readonly double RhinoAbsoluteTolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
        /// <summary>
        /// Tolerance squared - for fast neighbour search
        /// </summary>
        internal static readonly double RhinoAbsoluteToleranceSquared = Math.Pow(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 2);
        /// <summary>
        /// Offset for Obstruction check ray
        /// </summary>
        internal static readonly double ObstructionRayOffset = ObstructionToleranceMultiplier * RhinoAbsoluteTolerance;

        public static readonly GH_Gradient historyGradient = new GH_Gradient(new double[] { 0.0, 0.5, 0.9, 1.0 },
            new Color[] { Color.Black, Color.FromArgb(56, 136, 150), Color.FromArgb(186, 224, 224), Color.White });
        public static readonly GH_Gradient zHeightGradient = new GH_Gradient(new double[] { 0.0, 0.33, 0.66, 1.0 },
            new Color[] { Color.Black, Color.FromArgb(150, 66, 114), Color.FromArgb(224, 186, 187), Color.White });
        public static readonly GH_Gradient densityGradient = new GH_Gradient(new double[] { 0.0, 0.5, 1.0 },
            new Color[] { Color.White, Color.SlateGray, Color.DarkSlateGray });
        public static readonly GH_Gradient receiverValuesGradient = new GH_Gradient(new double[] { 0.0, 0.5, 1.0 },
            new Color[] { Color.White, Color.Red, Color.DarkRed });
        public static readonly GH_Gradient discoGradient = new GH_Gradient(new double[] { 0.0, 1.0 },
            new Color[] { Color.FromArgb(255, 0, 255), Color.FromArgb(0, 255, 255) });

        /// <summary>
        /// <see cref="AssemblyObject"/> Type palette, with up to 24 Colors
        /// </summary>
        /// <remarks>Which are already WAY TOO MANY!!!</remarks>
        public static readonly Color[] AOTypePalette = new Color[] {
        Color.FromArgb(192,57,43), Color.FromArgb(100,100,100), Color.FromArgb(52,152,219), Color.FromArgb(253,188,75),
        Color.FromArgb(155,89,182), Color.FromArgb(46,204,113), Color.FromArgb(49,54,59), Color.FromArgb(231,76,60),
        Color.FromArgb(189,195,199), Color.FromArgb(201,206,59), Color.FromArgb(142,68,173), Color.FromArgb(52,73,94),
        Color.FromArgb(29,153,19), Color.FromArgb(237,21,21), Color.FromArgb(127,140,141), Color.FromArgb(61,174,233),
        Color.FromArgb(243,156,31), Color.FromArgb(41,128,190), Color.FromArgb(35,38,41), Color.FromArgb(252,252,252),
        Color.FromArgb(218,68,83), Color.FromArgb(22,160,133), Color.FromArgb(149,165,166), Color.FromArgb(44,62,80)};

        /// <summary>
        /// Palette for receiver, sender (in this order)
        /// </summary>
        public static readonly Color[] srPalette = new Color[] { Color.SlateGray, Color.FromArgb(229, 229, 220) };

        /// <summary>
        /// Known Colors into a List
        /// </summary>
        /// <remarks>see: https://www.codeproject.com/Questions/826358/How-to-choose-a-random-color-from-System-Drawing-C</remarks>
        public static readonly List<KnownColor> colorlist = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().ToList();

        #region Assemblage Utilities

        #region Collision Utilities

        /// <summary>
        /// Collision check for a given <see cref="AssemblyObject"/> in an <see cref="Assemblage"/>
        /// </summary>
        /// <param name="AOa">The <see cref="Assemblage"/> for checking</param>
        /// <param name="sO">The sender <see cref="AssemblyObject"/> to check</param>
        /// <param name="neighbourIndexes">array of neighbour AssemblyObjects Aind</param>
        /// <returns>true if a collision exists</returns>
        internal static bool CollisionCheckInAssemblage(Assemblage AOa, AssemblyObject sO, out int[] neighbourIndexes)
        {
            neighbourIndexes = null;
            // get first vertex as Point3d for inclusion check
            Point3d neighFirstVertex, sOfirstVertex = sO.OffsetMesh.Vertices[0];

            // find neighbours in Assemblage 
            List<int> neighList = new List<int>();
            // collision radius is a Field of AssemblyObject
            AOa.centroidsTree.Search(new Sphere(sO.ReferencePlane.Origin, AOa.CollisionRadius), (sender, args) =>
            {
                // recover the AssemblyObject AInd related to the found centroid
                neighList.Add(AOa.centroidsAO[args.Id]);
            });

            // check for no neighbours
            if (neighList.Count == 0) return false;

            neighbourIndexes = neighList.ToArray();
            // check for collisions + inclusion (sender in receiver, receiver in sender)
            //int pathIndex;
            Mesh AOcollision;
            foreach (int index in neighbourIndexes)
            {
                GH_Path neighPath = new GH_Path(index);
                AOcollision = AOa.AssemblyObjects[neighPath, 0].CollisionMesh;
                // check Bounding Box intersection first - if no intersection continue to the next loop iteration
                if (!BoundingBoxIntersect(sO.CollisionMesh.GetBoundingBox(false),
                    AOcollision.GetBoundingBox(false)))
                    continue;
                // check Mesh intersection
                if (Intersection.MeshMeshFast(sO.OffsetMesh, AOcollision).Length > 0)
                    return true;
                // check if sender object is inside neighbour
                if (AOcollision.IsPointInside(sOfirstVertex, RhinoAbsoluteTolerance, true))
                    return true;
                // check if neighbour is inside sender object
                // get neighbour's OffsetMesh first vertex & check if it's inside
                neighFirstVertex = AOa.AssemblyObjects[neighPath, 0].OffsetMesh.Vertices[0];
                if (sO.CollisionMesh.IsPointInside(neighFirstVertex, RhinoAbsoluteTolerance, true))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Collision Check with other <see cref="AssemblyObject"/>s for a given <see cref="AssemblyObject"/>
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="neighbours"></param>
        /// <returns></returns>
        internal static bool CollisionCheckNeighbours(AssemblyObject AO, List<AssemblyObject> neighbours)
        {

            // get first vertex as Point3d for inclusion check
            Point3d neighFirstVertex, AOfirstVertex = AO.OffsetMesh.Vertices[0];

            // check for collisions + inclusion (first points inside each other under threshold)
            foreach (AssemblyObject neighbour in neighbours)
            {
                if (Intersection.MeshMeshFast(AO.OffsetMesh, neighbour.CollisionMesh).Length > 0)
                    return true;
                if (neighbour.CollisionMesh.IsPointInside(AOfirstVertex, RhinoAbsoluteTolerance, true))
                    return true;

                neighFirstVertex = neighbour.OffsetMesh.Vertices[0];

                if (AO.CollisionMesh.IsPointInside(neighFirstVertex, RhinoAbsoluteTolerance, true))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Collision Check between 2 <see cref="AssemblyObject"/>
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="sender"></param>
        /// <returns>True if objects are colliding or one contains the other</returns>
        public static bool CollisionCheckPair(AssemblyObject receiver, AssemblyObject sender)
        {
            // mesh collision test
            if (Intersection.MeshMeshFast(receiver.CollisionMesh, sender.OffsetMesh).Length > 0)
                return true;

            // mesh inclusion test - uses first vertex as Point3d for inclusion check
            Point3d rOfirstVertex = receiver.OffsetMesh.Vertices[0];
            if (sender.CollisionMesh.IsPointInside(rOfirstVertex, RhinoAbsoluteTolerance, true))
                return true;

            Point3d sOfirstVertex = sender.OffsetMesh.Vertices[0];
            if (receiver.CollisionMesh.IsPointInside(sOfirstVertex, RhinoAbsoluteTolerance, true))
                return true;

            return false;
        }

        /// <summary>
        /// Collision Check between 2 Meshes
        /// </summary>
        /// <param name="test"></param>
        /// <param name="surroundings"></param>
        /// <returns>True if objects are colliding or one contains the other</returns>
        public static bool CollisionCheckMeshes(Mesh test, Mesh surroundings)
        {
            // mesh collision test
            if (Intersection.MeshMeshFast(test, surroundings).Length > 0)
                return true;

            // mesh inclusion test - uses first vertex as Point3d for inclusion check
            Point3d testFirstVertex = test.Vertices[0];
            if (surroundings.IsPointInside(testFirstVertex, RhinoAbsoluteTolerance, true))
                return true;

            Point3d sFirstVertex = surroundings.Vertices[0];
            if (test.IsPointInside(sFirstVertex, RhinoAbsoluteTolerance, true))
                return true;

            return false;
        }

        private static bool BoundingBoxIntersect(BoundingBox a, BoundingBox b)
        {
            return (a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
                a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
                a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z);
        }

        #endregion Collision Utilities

        #region Obstruction Utilities

        /// <summary>
        /// Check obstruction status for an <see cref="AssemblyObject"/> in the <see cref="Assemblage"/>
        /// </summary>
        /// <param name="AOa"></param>
        /// <param name="AO_AInd"></param>
        /// <returns></returns>
        /// <param name="neighbourAIndexes"></param>
        internal static bool ObstructionCheckAssemblage(Assemblage AOa, int AO_AInd, int[] neighbourAIndexes)
        {
            AssemblyObject AO = AOa.AssemblyObjects[new GH_Path(AO_AInd), 0];
            bool obstruct = false;
            Line ray;
            int[] faceIDs;

            // check two-way: 
            // 1. object Handles connected or obstructed by neighbours
            // 2. neighbour Handles obstructed by object

            GH_Path neighPath;
            int neighSeqInd;

            // scan neighbours
            foreach (int neighAInd in neighbourAIndexes)
            {
                // find neighbour sequential index (for faster tree access)
                neighPath = new GH_Path(neighAInd);
                neighSeqInd = AOa.AssemblyObjects.Paths.IndexOf(neighPath);
                // scan neighbour's Handles
                for (int j = 0; j < AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles.Length; j++)
                {
                    // if the handle is not available continue
                    if (AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].Occupancy != 0) continue;

                    // check for accidental handle connection
                    bool connect = false;
                    // scan sO Handles
                    for (int k = 0; k < AO.Handles.Length; k++)
                    {
                        // if sO handle is not available continue
                        if (AO.Handles[k].Occupancy != 0) continue;
                        // ANY Handle (type independent) who is accidentally in contact is considered connected by default
                        // maybe set an option for strict type or rule based check if necessary
                        // for rule based checks, newly placed AO is treated as sender, neighbour is treated as receiver
                        // if (RuleExist(AOa, AO.type, AOa.assemblyObjects[index].type, AO.Handles[k].type, AOa.assemblyObjects[index].Handles[j].type))
                        // if Handles are of the same type...
                        // if (AO.Handles[k].type == AOa.assemblyObjects[index].Handles[j].type)
                        // ...and their distance is below absolute tolerance...
                        if (AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].Sender.Origin.DistanceToSquared(AO.Handles[k].Sender.Origin) < RhinoAbsoluteToleranceSquared)
                        {
                            // ...update Handles
                            UpdateHandlesOnConnection(AO, k, AOa.AssemblyObjects.Branch(neighSeqInd)[0], j);
                            connect = true;
                            break;
                        }
                    }
                    // if a connection happened, go to next handle in neighbour
                    if (connect) continue;

                    // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                    // shoot a line from the handle
                    Plane hSender = AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].Sender;
                    ray = new Line(hSender.Origin - (hSender.ZAxis * ObstructionRayOffset), hSender.ZAxis * ObstructionRayLength);

                    // if it intercepts the last added object
                    if (Intersection.MeshLine(AO.CollisionMesh, ray, out faceIDs).Length != 0)
                    {
                        // change handle occupancy to -1 (occluded) and add Object index to occluded handle neighbourObject
                        AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].Occupancy = -1;
                        AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].NeighbourObject = AO_AInd;
                        // update Object OccludedNeighbours status
                        AOa.AssemblyObjects[new GH_Path(AO_AInd), 0].OccludedNeighbours.Add(new int[] { neighAInd, j });
                        // change obstruct variable status
                        obstruct = true;
                    }

                }

                // CHECK OBSTRUCTION OF sO HANDLES BY NEIGHBOUR
                for (int k = 0; k < AO.Handles.Length; k++)
                {
                    // if sO handle is not available continue
                    if (AO.Handles[k].Occupancy != 0) continue;

                    // shoot a line from the handle
                    ray = new Line(AO.Handles[k].Sender.Origin - (AO.Handles[k].Sender.ZAxis * ObstructionRayOffset), AO.Handles[k].Sender.ZAxis * ObstructionRayLength);

                    // if it intercepts the neighbour object
                    if (Intersection.MeshLine(AOa.AssemblyObjects.Branch(neighSeqInd)[0].CollisionMesh, ray, out faceIDs).Length != 0)
                    {
                        // change handle occupancy to -1 (occluded) and add neighbour Object index to occluded handle neighbourObject
                        AO.Handles[k].Occupancy = -1;
                        AO.Handles[k].NeighbourObject = neighAInd;
                        // update neighbourObject OccludedNeighbours status
                        AOa.AssemblyObjects.Branch(neighSeqInd)[0].OccludedNeighbours.Add(new int[] { AO_AInd, k });
                        // change obstruct variable status
                        obstruct = true;
                    }
                }
            }
            return obstruct;
        }

        /// <summary>
        /// Check obstruction and <see cref="Handle"/> Occupancy in a List of <see cref="AssemblyObject"/>s
        /// </summary>
        /// <param name="AOList">List of <see cref="AssemblyObject"/>s to check</param>
        /// <returns></returns>
        internal static bool ObstructionCheckList(List<AssemblyObject> AOList)
        {
            //AssemblyObject AO = AOa.assemblage[AOindex];
            bool obstruct = false;
            Line ray;
            int[] faceIDs;

            // check two-way: 
            // 1. object Handles connected or obstructed by neighbours
            // 2. neighbour Handles obstructed by object

            for (int i = 0; i < AOList.Count; i++)
            // scan neighbours
            //foreach (int index in neighList)
            {
                for (int j = i + 1; j < AOList.Count; j++)
                {
                    // scan neighbour's Handles
                    for (int k = 0; k < AOList[j].Handles.Length; k++)
                    {
                        // if the handle is not available continue
                        if (AOList[j].Handles[k].Occupancy != 0) continue;

                        // check for accidental handle connection
                        bool connect = false;
                        // scan sO Handles
                        for (int p = 0; p < AOList[i].Handles.Length; p++)
                        {
                            // if sO handle is not available continue
                            if (AOList[i].Handles[p].Occupancy != 0) continue;
                            // if Handles are of the same type...
                            if (AOList[i].Handles[p].Type == AOList[j].Handles[k].Type)
                                // ...and their distance is below absolute tolerance...
                                if (AOList[j].Handles[k].Sender.Origin.DistanceToSquared(AOList[i].Handles[p].Sender.Origin) < RhinoAbsoluteToleranceSquared)// Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                                {
                                    // ...update Handles
                                    UpdateHandlesOnConnection(AOList[i], p, AOList[j], k);
                                    connect = true;
                                    break;
                                }
                        }
                        // if a connection happened, go to next handle in neighbour
                        if (connect) continue;

                        // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                        // shoot a line from the handle
                        ray = new Line(AOList[j].Handles[k].Sender.Origin - (AOList[j].Handles[k].Sender.ZAxis * 0.1), AOList[j].Handles[k].Sender.ZAxis * ObstructionRayLength);

                        // if it intercepts the last added object
                        if (Intersection.MeshLine(AOList[i].CollisionMesh, ray, out faceIDs).Length != 0)
                        {
                            // change handle occupancy to -1 (occluded) and add Object index to occluded handle neighbourObject
                            AOList[j].Handles[k].Occupancy = -1;
                            AOList[j].Handles[k].NeighbourObject = i;
                            // update Object OccludedNeighbours status
                            AOList[i].OccludedNeighbours.Add(new int[] { j, k });
                            // change obstruct variable status
                            obstruct = true;
                        }

                    }

                    // CHECK OBSTRUCTION OF sO HANDLES BY NEIGHBOUR
                    for (int k = 0; k < AOList[i].Handles.Length; k++)
                    {
                        // if sO handle is not available continue
                        if (AOList[i].Handles[k].Occupancy != 0) continue;

                        // shoot a line from the handle
                        ray = new Line(AOList[i].Handles[k].Sender.Origin - (AOList[i].Handles[k].Sender.ZAxis * 0.1), AOList[i].Handles[k].Sender.ZAxis * 1.5);

                        // if it intercepts the neighbour object
                        if (Intersection.MeshLine(AOList[j].CollisionMesh, ray, out faceIDs).Length != 0)
                        {
                            // change handle occupancy to -1 (occluded) and add neighbour Object index to occluded handle neighbourObject
                            AOList[i].Handles[k].Occupancy = -1;
                            AOList[i].Handles[k].NeighbourObject = j;
                            // update neighbourObject OccludedNeighbours status
                            AOList[j].OccludedNeighbours.Add(new int[] { i, k });
                            // change obstruct variable status
                            obstruct = true;
                        }
                    }
                }
            }
            return obstruct;
        }

        #endregion Obstruction Utilities

        /// <summary>
        /// Builds the Dictionary of AssemblyObjects
        /// </summary>
        /// <param name="AOset">the array of unique <see cref="AssemblyObject"/>s constituting the set</param>
        /// <returns>The (Name, type) Dictionary built from the AOSet</returns>
        public static Dictionary<string, int> BuildDictionary(AssemblyObject[] AOset)
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();

            for (int i = 0; i < AOset.Length; i++)
            {
                AOset[i].Type = i;
                dict.Add(AOset[i].Name, AOset[i].Type);
            }
            return dict;
        }

        /// <summary>
        /// Clone an Assemblage
        /// </summary>
        /// <param name="AOa">Assemblage to clone</param>
        /// <returns>A cloned Assemblage</returns>
        public static Assemblage Clone(Assemblage AOa)
        {
            Assemblage clonedAOa = new Assemblage();

            // cloning by value data (primitives and structs)
            clonedAOa.HeuristicsSettings = AOa.HeuristicsSettings;
            clonedAOa.ExogenousSettings = AOa.ExogenousSettings;
            clonedAOa.currentHeuristics = AOa.currentHeuristics;
            clonedAOa.CollisionRadius = AOa.CollisionRadius;
            clonedAOa.CheckWorldZLock = AOa.CheckWorldZLock;
            clonedAOa.E_sandbox = AOa.E_sandbox;

            // clone AOSet
            clonedAOa.AOSet = new AssemblyObject[AOa.AOSet.Length];
            for (int i = 0; i < AOa.AOSet.Length; i++)
                clonedAOa.AOSet[i] = Clone(AOa.AOSet[i]);

            // clone dictionary
            clonedAOa.AOSetDictionary = new Dictionary<string, int>(AOa.AOSetDictionary);

            // clone DataTrees
            clonedAOa.heuristicsTree = new DataTree<Rule>(AOa.heuristicsTree, r => r);
            clonedAOa.AssemblageRules = new DataTree<string>(AOa.AssemblageRules, r => r);
            clonedAOa.ReceiverAIndexes = new DataTree<int>(AOa.ReceiverAIndexes, i => i);
            clonedAOa.AssemblyObjects = new DataTree<AssemblyObject>(AOa.AssemblyObjects, ao => CloneWithConnectivity(ao));
            // clone AssemblyObjects (OLD)
            //clonedAOa.AssemblyObjects = new DataTree<AssemblyObject>();
            //for (int i = 0; i < AOa.AssemblyObjects.BranchCount; i++)
            //    clonedAOa.AssemblyObjects.Add(CloneWithConnectivity(AOa.AssemblyObjects.Branches[i][0]), AOa.AssemblyObjects.Paths[i]);

            // clone Lists
            clonedAOa.availableObjects = AOa.availableObjects.Select(av => av).ToList();
            clonedAOa.unreachableObjects = AOa.unreachableObjects.Select(ur => ur).ToList();
            clonedAOa.availableReceiverValues = AOa.availableReceiverValues.Select(arv => arv).ToList();
            clonedAOa.centroidsAO = AOa.centroidsAO.Select(c => c).ToList();

            clonedAOa.centroidsTree = new RTree();//AOa.centroidsTree;
            for (int i = 0; i < AOa.AssemblyObjects.BranchCount; i++)
            {
                // add object to the centroids tree
                // future implementation: if object has children, insert all children centroids under the same AInd
                clonedAOa.centroidsTree.Insert(AOa.AssemblyObjects.Branches[i][0].ReferencePlane.Origin, AOa.AssemblyObjects.Branches[i][0].AInd);
            }
            //clonedAOa.handleTypes = AOa.handleTypes;
            // candidateObjects doesn't need cloning
            return clonedAOa;
        }

        /// <summary>
        /// Remove an <see cref="AssemblyObject"/> from an <see cref="Assemblage"/>, updating Topology information
        /// </summary>
        /// <param name="AOa">The Assemblage to remove from</param>
        /// <param name="AInd">the Assemblage Index of the AssemblyObject to remove</param>
        /// <returns>true if successful, false otherwise</returns>
        public static bool RemoveAssemblyObject(Assemblage AOa, int AInd)
        {
            GH_Path AOPath = new GH_Path(AInd);
            // if the index does not exist return
            if (!AOa.AssemblyObjects.PathExists(AOPath)) return false;

            AssemblyObject AO = AOa.AssemblyObjects[AOPath, 0];

            // . . . Topology operations
            // update connected AO Handles
            for (int i = 0; i < AO.Handles.Length; i++)
            {
                // AInd of neighbour object
                int neighAInd = AO.Handles[i].NeighbourObject;
                GH_Path neighPath = new GH_Path(neighAInd);

                // free connected Handles
                if (AO.Handles[i].Occupancy == 1)
                {
                    AOa.AssemblyObjects[neighPath, 0].Handles[AO.Handles[i].NeighbourHandle].Occupancy = 0;
                    AOa.AssemblyObjects[neighPath, 0].Handles[AO.Handles[i].NeighbourHandle].NeighbourObject = -1;
                    AOa.AssemblyObjects[neighPath, 0].Handles[AO.Handles[i].NeighbourHandle].NeighbourHandle = -1;
                }
                // update occluding objects
                else if (AO.Handles[i].Occupancy == -1)
                {
                    // scan OccludedNeighbours list of neighbour occluded object
                    for (int j = AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours.Count - 1; j >= 0; j--)
                    {
                        // if AInd and Handle match, remove entry
                        if (AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours[j][0] == AO.AInd &&
                           AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours[j][1] == i)
                            AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours.RemoveAt(j);
                    }
                    //AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours.Remove(new int[] { AO.AInd, i });
                }
            }

            // check its occluded objects
            for (int i = 0; i < AO.OccludedNeighbours.Count; i++)
            {
                GH_Path occludePath = new GH_Path(AO.OccludedNeighbours[i][0]);
                // free occluded handle
                AOa.AssemblyObjects[occludePath, 0].Handles[AO.OccludedNeighbours[i][1]].Occupancy = 0;
                AOa.AssemblyObjects[occludePath, 0].Handles[AO.OccludedNeighbours[i][1]].NeighbourObject = -1;
            }

            // remove from used rules
            AOa.AssemblageRules.RemovePath(AO.AInd);
            // remove from used receiver indexes
            AOa.ReceiverAIndexes.RemovePath(AO.AInd);

            // check if in available-unreachable objects and remove
            if (AOa.availableObjects.Contains(AO.AInd))
            {
                int avSeq = AOa.availableObjects.IndexOf(AO.AInd);
                AOa.availableObjects.Remove(AO.AInd);
                AOa.availableReceiverValues.RemoveAt(avSeq);
            }
            else if (AOa.unreachableObjects.Contains(AO.AInd)) AOa.unreachableObjects.Remove(AO.AInd);

            // remove from centroids tree
            AOa.centroidsTree.Remove(AO.ReferencePlane.Origin, AO.AInd);

            // remove from AssemblyObject list
            AOa.AssemblyObjects.RemovePath(AO.AInd);

            return true;
        }

        #endregion Assemblage Utilities

        #region AssemblyObject Utilities

        /// <summary>
        /// Performs a check for World Z-Axis orientation of the AssemblyObject
        /// </summary>
        /// <param name="AO">the <see cref="AssemblyObject"/> to check</param>
        /// <returns>true if the Z axis of the object Reference Plane is oriented along the World Z</returns>
        public static bool AbsoluteZCheck(AssemblyObject AO) => AO.ReferencePlane.ZAxis * Vector3d.ZAxis == 1;

        /// <summary>
        /// Performs a check for World Z-Axis orientation of the AssemblyObject, with a tolerance
        /// </summary>
        /// <param name="AO">the <see cref="AssemblyObject"/> to check</param>
        /// <param name="tol">the tolerance to respect</param>
        /// <returns>true if the Z axis of the object Reference Plane is oriented along the World Z under the given tolerance</returns>
        public static bool AbsoluteZCheck(AssemblyObject AO, double tol) => 1 - (AO.ReferencePlane.ZAxis * Vector3d.ZAxis) <= tol;

        /// <summary>
        /// Clones an <see cref="AssemblyObject"/> as an asset, resetting connectivity information
        /// </summary>
        /// <param name="AO"></param>
        /// <returns>a cloned AssemblyObejct asset</returns>
        public static AssemblyObject Clone(AssemblyObject AO)
        {
            // build deep copies of meshes
            Mesh collisionMesh, offsetMesh;
            collisionMesh = new Mesh();
            offsetMesh = new Mesh();

            collisionMesh.CopyFrom(AO.CollisionMesh);
            offsetMesh.CopyFrom(AO.OffsetMesh);

            // reset occluded neighbours
            List<int[]> occludedNeighbours = new List<int[]>();

            // clone Handles resetting connectivity
            Handle[] handles = AO.Handles.Select(h => Clone(ref h)).ToArray();

            // clone children & handleMap
            List<AssemblyObject> children = null;
            List<int[]> handleMap = null;

            if (AO.children != null)
            {
                children = AO.children.Select(ao => Clone(ao)).ToList();
                // clone handlemap
                handleMap = AO.handleMap;
            }

            // clone supports
            List<Support> supports = null;

            if (AO.supports != null)
                supports = AO.supports.Select(s => new Support(s)).ToList();

            // clone AssemblyObject with default values (idleWeight is passed twice to reset weight, -1 is passed as aInd)
            AssemblyObject AOclone = new AssemblyObject(collisionMesh, offsetMesh, handles, AO.ReferencePlane, AO.Direction, -1, occludedNeighbours,
                AO.Name, AO.Type, AO.IdleWeight, AO.IdleWeight, AO.IWeight, supports, AO.minSupports, AO.supported, AO.WorldZLock, children, handleMap, double.NaN, double.NaN);

            return AOclone;
        }

        /// <summary>
        /// Duplicates an <see cref="AssemblyObject"/> preserving connectivity information
        /// </summary>
        /// <param name="AO">The Original <see cref="AssemblyObject"/></param>
        /// <returns>A duplicated AssemblyObject with the same connectivity of the source</returns>
        /// <remarks>Useful for previous assemblages and the Goo wrapper</remarks>
        public static AssemblyObject CloneWithConnectivity(AssemblyObject AO)
        {
            // make a fresh new clone
            AssemblyObject AOcloneConnect = Clone(AO);

            for (int i = 0; i < AOcloneConnect.Handles.Length; i++)
                AOcloneConnect.Handles[i] = CloneWithConnectivity(ref AO.Handles[i]);

            AOcloneConnect.OccludedNeighbours = AO.OccludedNeighbours.Select(id => (int[])id.Clone()).ToList();
            AOcloneConnect.Weight = AO.Weight;
            AOcloneConnect.ReceiverValue = AO.ReceiverValue;
            AOcloneConnect.SenderValue = AO.SenderValue;
            AOcloneConnect.AInd = AO.AInd;

            // supports
            if (AO.supports != null)
            {
                AOcloneConnect.supports = AO.supports.Select(s => new Support(s)).ToList();
                AOcloneConnect.supported = AO.supported;
            }

            return AOcloneConnect;
        }

        /// <summary>
        /// Set a new CollisionMesh for the AssemblyObject
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="newCollisionMesh"></param>
        public static void SetCollisionMesh(AssemblyObject AO, Mesh newCollisionMesh)
        {
            AO.CollisionMesh = new Mesh();
            AO.CollisionMesh.CopyFrom(newCollisionMesh);
            double offsetTolerance = RhinoAbsoluteTolerance * 2.5;
            // do NOT use the standard Mesh Offset method
            AO.OffsetMesh = MeshOffsetWeightedAngle(AO.CollisionMesh, offsetTolerance);
        }

        #endregion AssemblyObject Utilities

        #region Handle Utilities

        private static void ResetConnectivity(ref Handle handle)
        {
            handle.Occupancy = 0;
            handle.NeighbourHandle = -1;
            handle.NeighbourObject = -1;
        }

        private static Handle CloneBasic(Handle handle)
        {
            Handle clone = new Handle();
            clone.Sender = handle.Sender;
            clone.Rotations = handle.Rotations.Select(r => r).ToArray();
            clone.RDictionary = CloneDictionaryWithValues(handle.RDictionary);
            // this is a shallow copy - not working
            //r = other.r;
            // deep array copy (from https://stackoverflow.com/questions/3464635/deep-copy-with-array)
            clone.Receivers = handle.Receivers.Select(pl => pl.Clone()).ToArray();
            clone.Type = handle.Type;
            clone.Weight = handle.IdleWeight;
            clone.IdleWeight = handle.IdleWeight;

            return clone;
        }

        /// <summary>
        /// Clones a Handle, resetting data
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>a cloned Handle</returns>
        public static Handle Clone(ref Handle handle)
        {
            Handle clone = CloneBasic(handle);

            clone.Occupancy = 0;
            clone.NeighbourHandle = -1;
            clone.NeighbourObject = -1;

            return clone;
        }

        /// <summary>
        /// Duplicates a Handle preserving connectivity information and weight
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>a duplicated Handle with the same connectivity</returns>
        public static Handle CloneWithConnectivity(ref Handle handle)
        {
            Handle clone = CloneBasic(handle);

            clone.Occupancy = handle.Occupancy;
            clone.NeighbourHandle = handle.NeighbourHandle;
            clone.NeighbourObject = handle.NeighbourObject;

            return clone;
        }

        /// <summary>
        /// Updates <see cref="Handle"/>s involved in a new connection between two <see cref="AssemblyObject"/>s
        /// </summary>
        /// <param name="AO1">first <see cref="AssemblyObject"/></param>
        /// <param name="handle1"><see cref="Handle"/> from first <see cref="AssemblyObject"/></param>
        /// <param name="AO2">second <see cref="AssemblyObject"/></param>
        /// <param name="handle2"><see cref="Handle"/> from second <see cref="AssemblyObject"/></param>
        internal static void UpdateHandlesOnConnection(AssemblyObject AO1, int handle1, AssemblyObject AO2, int handle2)
        {
            AO1.Handles[handle1].Occupancy = 1;
            AO2.Handles[handle2].Occupancy = 1;
            AO1.Handles[handle1].NeighbourObject = AO2.AInd;
            AO2.Handles[handle2].NeighbourObject = AO1.AInd;
            AO1.Handles[handle1].NeighbourHandle = handle2;
            AO2.Handles[handle2].NeighbourHandle = handle1;

            double newWeight = 0.5 * (AO1.Handles[handle1].Weight + AO2.Handles[handle2].Weight);
            AO1.Handles[handle1].Weight = newWeight;
            AO2.Handles[handle2].Weight = newWeight;
        }

        /// <summary>
        /// Purges a List of Null items
        /// </summary>
        /// <param name="inputList">A supposedly dirty <see cref="Handle"/> List containing some Null items</param>
        /// <returns>The purged List</returns>
        public static List<Handle> PurgeNullHandlesFromList(List<Handle> inputList)
        {
            List<Handle> purgedList = new List<Handle>();

            for (int i = 0; i < inputList.Count; i++)
            {
                if (inputList[i].Sender == null || inputList[i].RDictionary == null) continue;
                purgedList.Add(inputList[i]);
            }

            return purgedList;
        }

        #endregion Handle Utilities

        #region Rule Utilities

        /// <summary>
        /// Returns a list of Rules from a heuristics string
        /// </summary>
        /// <param name="AOset"></param>
        /// <param name="AOCatalog"></param>
        /// <param name="heuristics"></param>
        /// <returns></returns>
        public static List<Rule> HeuristicsRulesFromString(List<AssemblyObject> AOset, Dictionary<string, int> AOCatalog, List<string> heuristics)
        {
            List<Rule> heuT = new List<Rule>();

            string[] ruleStrings = heuristics.ToArray();

            int rT, rH, rR, sT, sH;
            double rRA;
            int iWeight;
            for (int i = 0; i < ruleStrings.Length; i++)
            {
                string[] ruleString = ruleStrings[i].Split(new[] { '<', '%' });
                string[] rec = ruleString[0].Split(new[] { '|' });
                string[] sen = ruleString[1].Split(new[] { '|' });
                // sender and receiver component types
                sT = AOCatalog[sen[0]];
                rT = AOCatalog[rec[0]];
                // sender handle index
                sH = Convert.ToInt32(sen[1]);
                // iWeight
                iWeight = Convert.ToInt32(ruleString[2]);
                string[] rRot = rec[1].Split(new[] { '=' });
                // receiver handle index and rotation
                rH = Convert.ToInt32(rRot[0]);
                rRA = Convert.ToDouble(rRot[1]);
                rR = AOset[rT].Handles[rH].RDictionary[rRA]; // using rotations

                heuT.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, iWeight));
            }
            return heuT;
        }

        #endregion Rule Utilities

        #region Supports Utilities

        /// <summary>
        /// Add <see cref="Support"/>s to the <see cref="AssemblyObject"/>
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="lines"></param>
        /// <param name="minSupports"></param>
        /// <returns>true if successful</returns>
        public static bool SetSupports(AssemblyObject AO, List<Line> lines, int minSupports)
        {
            if (lines == null || lines.Count == 0) return false;

            AO.supports = new List<Support>();

            foreach (Line l in lines)
                AO.supports.Add(new Support(l));
            AO.minSupports = minSupports;
            AO.supported = false;

            return true;
        }

        /// <summary>
        /// Resets <see cref="Support"/>s for an <see cref="AssemblyObject"/>
        /// </summary>
        /// <param name="AO"></param>
        public static void ResetSupports(AssemblyObject AO)
        {
            if (AO.supports != null)
                foreach (Support s in AO.supports)
                    s.Reset();
            AO.supported = false;
        }

        /// <summary>
        /// Check if the <see cref="AssemblyObject"/> is supported by a list of neighbouring AssemblyObjects
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="neighbours"></param>
        /// <returns>true if the object is supported, false otherwise</returns>
        public static bool CheckSupport(AssemblyObject AO, List<AssemblyObject> neighbours)
        {
            if (AO.supported) return true;

            //AO.supported = false;
            int sCount = 0;
            // connected supports (as tentative)
            List<int> cSupports = new List<int>();

            for (int i = 0; i < AO.supports.Count; i++)
                if (AO.supports[i].Connected || SupportIntersect(AO.supports[i], neighbours))
                {
                    sCount++;
                    cSupports.Add(i);
                }

            AO.supported = sCount >= AO.minSupports;

            // if not enough supports, reset the ones modified by this attempt
            if (!AO.supported)
                foreach (int ind in cSupports)
                    AO.supports[ind].Reset();

            return AO.supported;
        }

        /// <summary>
        /// Check if the <see cref="AssemblyObject"/> is supported by a list of neighbouring Meshes
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="neighMeshes"></param>
        /// <returns>true if the object is supported, false otherwise</returns>
        public static bool CheckSupport(AssemblyObject AO, List<Mesh> neighMeshes)
        {
            if (AO.supported) return true;

            //AO.supported = false;
            int sCount = 0;
            // connected supports (as tentative)
            List<int> cSupports = new List<int>();

            for (int i = 0; i < AO.supports.Count; i++)
                if (AO.supports[i].Connected || SupportIntersect(AO.supports[i], neighMeshes))
                {
                    sCount++;
                    cSupports.Add(i);
                }

            AO.supported = sCount >= AO.minSupports;

            // if not enough supports, reset the ones modified by this attempt
            if (!AO.supported)
                foreach (int ind in cSupports)
                    AO.supports[ind].Reset();

            return AO.supported;
        }

        /// <summary>
        /// Check intersection of a <see cref="Support"/> with a list of <see cref="AssemblyObject"/>s
        /// </summary>
        /// <param name="s"></param>
        /// <param name="neighbours"></param>
        /// <returns></returns>
        internal static bool SupportIntersect(Support s, List<AssemblyObject> neighbours)
        {
            int[] faceIds;
            Point3d[] intPts;
            //Vector3d dir = s.line.Direction;
            //dir.Unitize();
            //double minD;
            foreach (AssemblyObject AO in neighbours)
            {
                intPts = Intersection.MeshLine(AO.CollisionMesh, s.Line, out faceIds);
                // if intersections are found resize support line to intersection point and return true
                if (intPts.Length > 0)
                {
                    // Rhino 7 has MeshLineSorted intersection
                    // consider the point at index 0 for the time being - correct if something's wrong

                    //minD = double.MaxValue;
                    //for (int i = 0; i < intPts.Length; i++)
                    //    minD = Math.Min(minD, s.line.From.DistanceToSquared(intPts[i]));
                    //dir *= minD;
                    //s.line = new Line(s.line.From, s.line.From + dir);
                    s.Line = new Line(s.Line.From, intPts[0]);
                    s.NeighbourObject = AO.AInd;
                    //s.connected = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check intersection of a Support with a list of MeshEnvironment
        /// </summary>
        /// <param name="s"></param>
        /// <param name="envMeshes"></param>
        /// <returns></returns>
        internal static bool SupportIntersect(Support s, List<MeshEnvironment> envMeshes)
        {
            int[] faceIds;
            Point3d[] intPts;
            Vector3d dir = s.Line.Direction;
            dir.Unitize();
            double minD;
            foreach (MeshEnvironment mE in envMeshes)
            {
                intPts = Intersection.MeshLine(mE.Mesh, s.Line, out faceIds);
                // if intersections are found resize support line to intersection point and return true
                if (intPts.Length > 0)
                {
                    minD = double.MaxValue;
                    for (int i = 0; i < intPts.Length; i++)
                        minD = Math.Min(minD, s.Line.From.DistanceToSquared(intPts[i]));
                    dir *= minD;
                    s.Line = new Line(s.Line.From, s.Line.From + dir);
                    s.NeighbourObject = -2;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check intersection of a Support with a list of Meshes
        /// </summary>
        /// <param name="s"></param>
        /// <param name="meshes"></param>
        /// <returns></returns>
        internal static bool SupportIntersect(Support s, List<Mesh> meshes)
        {
            int[] faceIds;
            Point3d[] intPts;
            Vector3d dir = s.Line.Direction;
            dir.Unitize();
            double minD;
            foreach (Mesh m in meshes)
            {
                intPts = Intersection.MeshLine(m, s.Line, out faceIds);
                // if intersections are found resize support line to intersection point and return true
                if (intPts.Length > 0)
                {
                    minD = double.MaxValue;
                    for (int i = 0; i < intPts.Length; i++)
                        minD = Math.Min(minD, s.Line.From.DistanceToSquared(intPts[i]));
                    dir *= minD;
                    s.Line = new Line(s.Line.From, s.Line.From + dir);
                    s.NeighbourObject = -2;
                    return true;
                }
            }

            return false;
        }

        #endregion Supports Utilities

        #region File Utilities
        /// <summary>
        /// Rebuilds and Assemblage from a JSON dump
        /// </summary>
        /// <param name="path"></param>
        /// <returns>An Assemblage as list of AssemblyObject</returns>
        public static List<AssemblyObject> AssemblageFromJSONdump(string path)
        {
            return DeserializeAssemblage(System.IO.File.ReadAllLines(path));
        }

        /// <summary>
        /// Saves an Assemblage as a JSON file dump - every object is serialized in its entirety
        /// </summary>
        /// <param name="assemblage"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <returns>File Name (with full path) of the saved assemblage</returns>
        public static string AssemblageToJSONdump(List<AssemblyObject> assemblage, string path, string name)
        {
            // converts assemblage to string array
            string[] AOjson = SerializeAssemblage(assemblage);

            // add sequential placeholder to filename - the suffix d indicates the dump mode
            name += "_{0}_d.JSON";

            // sanity checks
            // if there is no directory, create one
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);

            string fileName = ProcessFileName(path, name);

            System.IO.File.WriteAllLines(fileName, AOjson);

            return fileName;
        }

        /// <summary>
        /// TO-DO - COMPLETE THIS METHOD
        /// </summary>
        /// <param name="assemblage"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        public static void AssemblageToJSONSmart(Assemblage assemblage, string path, string name)
        {
            //string assemblageDir;
            string assetsDir = "assets";
            // checks on directory path and filenames

            // add sequential placeholder to filename
            name += "_{0}.JSON";

            // sanity checks
            // if there are no directories, create them
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
            else if (!System.IO.Directory.Exists($"{path}\\{assetsDir}")) System.IO.Directory.CreateDirectory($"{path}\\{assetsDir}");


            string fileName = ProcessFileName(path, name);

            int count = 0;
            // save geometries as assets from dictionary (collision and offsetmeshes)
            foreach (AssemblyObject AO in assemblage.AOSet)
            {
                string data = JsonConvert.SerializeObject(AO);
                string target = string.Format("AO{0}_{1}", count, fileName);
                System.IO.File.WriteAllText(target, data);
                count++;
            }

            // save other data from assemblage (objects, connectivity, other)

        }

        private static string ProcessFileName(string path, string name)
        {
            // Assume index=0 for the first filename.
            string fileName = path + string.Format(name, 0.ToString("D3"));

            // Try to increment the index until we find a Name which doesn't exist yet.
            if (System.IO.File.Exists(fileName))
                for (int i = 1; i < int.MaxValue; i++)
                {
                    string localName = path + string.Format(name, i.ToString("D3"));
                    if (localName == fileName)
                        continue;

                    if (!System.IO.File.Exists(localName))
                    {
                        fileName = localName;
                        break;
                    }
                }
            return fileName;
        }

        /// <summary>
        /// Saves an array of strings to a file in a given path
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        public static void SaveStringsToFile(string directory, string fileName, string[] data)
        {

            if (!System.IO.Directory.Exists(directory)) System.IO.Directory.CreateDirectory(directory);

            string target = directory + fileName;

            System.IO.File.WriteAllLines(target, data);
        }
        /// <summary>
        /// Appends string data to an existing file
        /// </summary>
        /// <param name="directory">The existing file directory</param>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        public static void AppendToFile(string directory, string fileName, string data)
        {
            string path = directory + fileName;
            var writer = System.IO.File.AppendText(path);
            writer.WriteLine(data);
            writer.Close();
        }

        /// <summary>
        /// Serializes an assemblage into a string array for subsequent file saving
        /// </summary>
        /// <param name="assemblage"></param>
        /// <returns></returns>
        internal static string[] SerializeAssemblage(List<AssemblyObject> assemblage)
        {
            string[] AOjson = new string[assemblage.Count];

            if (assemblage.Count < 1000)
                for (int i = 0; i < assemblage.Count; i++)

                    AOjson[i] = JsonConvert.SerializeObject(assemblage[i]);
            else
                Parallel.For(0, assemblage.Count, i =>
                {
                    AOjson[i] = JsonConvert.SerializeObject(assemblage[i]);
                });

            return AOjson;
        }

        /// <summary>
        /// Deserializes a string array into an AssemblyObject assemblage after file loading
        /// </summary>
        /// <param name="AOjson"></param>
        /// <returns></returns>
        internal static List<AssemblyObject> DeserializeAssemblage(string[] AOjson)
        {
            AssemblyObject[] assemblage = new AssemblyObject[AOjson.Length];
            if (AOjson.Length < 1000)
                for (int i = 0; i < AOjson.Length; i++)
                    assemblage[i] = JsonConvert.DeserializeObject<AssemblyObject>(AOjson[i]);
            else
                Parallel.For(0, AOjson.Length, i =>
                {
                    assemblage[i] = JsonConvert.DeserializeObject<AssemblyObject>(AOjson[i]);
                });

            return assemblage.ToList();
        }

        #endregion File Utilities

        #region Mesh Utilities

        /// <summary>
        /// Checks if a point P is inside a Mesh by checking the number of intersections of a line
        /// from a point outside to the test point
        /// even number of intersections is outside, odd is inside.
        /// see this thread for this and more methods: https://twitter.com/OskSta/status/1491716992931356672
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="testPoint"></param>
        /// <returns>True if point is inside, False otherwise</returns>
        public static bool IsPointInMesh(Mesh mesh, Point3d testPoint)
        {
            // this point must be OUTSIDE of the Mesh
            Point3d from = (Point3d)(mesh.Vertices[0] + mesh.Normals[0]);
            int[] faceIds;
            // even number of intersections: point is outside, otherwise point is inside
            return (Intersection.MeshLine(mesh, new Line(from, testPoint), out faceIds).Length % 2 != 0);
        }

        /// <summary>
        /// Improves Mesh Offset using a bespoke method for Mesh normal calculation, based on face angle weighing
        /// reference: https://stackoverflow.com/questions/25100120/how-does-blender-calculate-vertex-normals
        /// and: http://www.bytehazard.com/articles/vertnorm.html
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="offsetDistance"></param>
        /// <remarks>This method generates better offset meshes in most cases (concave, convex, comples shapes, etc.)</remarks>
        /// <returns>An offset Mesh</returns>
        internal static Mesh MeshOffsetWeightedAngle(Mesh mesh, double offsetDistance)
        {

            Mesh offset = new Mesh();
            offset.CopyFrom(mesh);

            Vector3d[] newNormals = new Vector3d[mesh.Vertices.Count];
            Point3d[] vertices = mesh.Vertices.ToPoint3dArray();

            //initialize newNormals
            for (int i = 0; i < newNormals.Length; i++)
                newNormals[i] = Vector3d.Zero;

            // Compute new normals
            newNormals = ComputeWeightedNormals(mesh);

            // Unitize newNormals
            foreach (Vector3d n in newNormals) n.Unitize();

            // offset Mesh
            for (int i = 0; i < offset.Vertices.Count; i++)
            {
                offset.Vertices.SetVertex(i, vertices[i] + newNormals[i] * -offsetDistance);
            }

            return offset;
        }

        /// <summary>
        /// Computes Mesh normals weighted by the face angle at each vertex
        /// Mesh weighted normals implemented from the tips at the following pages:
        /// https://stackoverflow.com/questions/25100120/how-does-blender-calculate-vertex-normals
        /// http://www.bytehazard.com/articles/vertnorm.html
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns>Weighted Normals as a Vector3d array</returns>
        private static Vector3d[] ComputeWeightedNormals(Mesh mesh)
        {
            mesh.Weld(Math.PI);
            mesh.RebuildNormals();

            Vector3d[] weightedNormals = new Vector3d[mesh.Vertices.Count];
            Point3d A, B, C, D;
            double faceAngle, faceArea;

            //initialize newNormals
            for (int i = 0; i < weightedNormals.Length; i++)
                weightedNormals[i] = Vector3d.Zero;

            // Compute new normals
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                faceArea = 1.0;// MeshFaceArea(mesh, i); // do not weight by area
                A = mesh.Vertices[mesh.Faces[i].A];
                B = mesh.Vertices[mesh.Faces[i].B];
                C = mesh.Vertices[mesh.Faces[i].C];
                if (mesh.Faces[i].IsTriangle)
                {
                    faceAngle = Vector3d.VectorAngle(B - A, C - A);
                    weightedNormals[mesh.Faces[i].A] += (Vector3d)mesh.FaceNormals[i] * faceAngle * faceArea;
                    faceAngle = Vector3d.VectorAngle(A - B, C - B);
                    weightedNormals[mesh.Faces[i].B] += (Vector3d)mesh.FaceNormals[i] * faceAngle * faceArea;
                    faceAngle = Vector3d.VectorAngle(A - C, B - C);
                    weightedNormals[mesh.Faces[i].C] += (Vector3d)mesh.FaceNormals[i] * faceAngle * faceArea;

                }
                else
                {
                    D = mesh.Vertices[mesh.Faces[i].D];
                    faceAngle = Vector3d.VectorAngle(B - A, D - A);
                    weightedNormals[mesh.Faces[i].A] += (Vector3d)mesh.FaceNormals[i] * faceAngle * faceArea;
                    faceAngle = Vector3d.VectorAngle(A - B, C - B);
                    weightedNormals[mesh.Faces[i].B] += (Vector3d)mesh.FaceNormals[i] * faceAngle * faceArea;
                    faceAngle = Vector3d.VectorAngle(D - C, B - C);
                    weightedNormals[mesh.Faces[i].C] += (Vector3d)mesh.FaceNormals[i] * faceAngle * faceArea;
                    faceAngle = Vector3d.VectorAngle(A - D, C - D);
                    weightedNormals[mesh.Faces[i].D] += (Vector3d)mesh.FaceNormals[i] * faceAngle * faceArea;
                }
            }

            // Unitize newNormals (do NOT use a foreach loop for this)
            for (int i = 0; i < weightedNormals.Length; i++) weightedNormals[i].Unitize();

            return weightedNormals;
        }

        private static double MeshFaceArea(Mesh m, int i)
        {
            double area = 0;
            MeshFace f = m.Faces[i];

            if (f.IsTriangle)
                area = TriangleArea(m.Vertices[f.A], m.Vertices[f.B], m.Vertices[f.C]);
            else
                area = TriangleArea(m.Vertices[f.A], m.Vertices[f.B], m.Vertices[f.C]) + TriangleArea(m.Vertices[f.D], m.Vertices[f.A], m.Vertices[f.C]);

            return area;
        }

        private static double TriangleArea(Point3d A, Point3d B, Point3d C)
        {
            return Math.Abs(A.X * (B.Y - C.Y) + B.X * (C.Y - A.Y) + C.X * (A.Y - B.Y)) * 0.5;
        }

        /// <summary>
        /// Returns Mesh edges whose faces stand at an angle larger than the tolerance
        /// </summary>
        /// <param name="mesh">The input Mesh</param>
        /// <param name="angleTolerance">The angle tolerance in radians - default is Math.PI * 0.25 = 45°</param>
        /// <returns>Edges as an array of GH_Lines</returns>
        public static GH_Line[] GetSihouette(Mesh mesh, double angleTolerance = Math.PI * 0.25)
        {
            ConcurrentBag<GH_Line> lines = new ConcurrentBag<GH_Line>();
            //double angleTolerance = Math.PI * 0.25; // angle tolerance  ignore edges whose faces meet at an angle larger than this

            mesh.Normals.ComputeNormals();
            Rhino.Geometry.Collections.MeshTopologyEdgeList topologyEdges = mesh.TopologyEdges;

            Parallel.For(0, topologyEdges.Count, i =>
            {
                int[] connectedFaces = topologyEdges.GetConnectedFaces(i);
                if (connectedFaces.Length < 2)
                    lines.Add(new GH_Line(topologyEdges.EdgeLine(i)));

                if (connectedFaces.Length == 2)
                {
                    Vector3f norm1 = mesh.FaceNormals[connectedFaces[0]];
                    Vector3f norm2 = mesh.FaceNormals[connectedFaces[1]];
                    double nAng = Vector3d.VectorAngle(new Vector3d((double)norm1.X, (double)norm1.Y, (double)norm1.Z),
                      new Vector3d((double)norm2.X, (double)norm2.Y, (double)norm2.Z));
                    if (nAng > angleTolerance)
                        lines.Add(new GH_Line(topologyEdges.EdgeLine(i)));

                }
            });

            return lines.ToArray();
        }

        /// <summary>
        /// Triangulate a Mesh splitting quad faces along the shortest diagonal
        /// </summary>
        /// <param name="inputMesh"></param>
        /// <returns>a Mesh with triangular faces only</returns>
        private static Mesh Triangulate(Mesh inputMesh)
        {
            Mesh triMesh = new Mesh();
            int facecount = inputMesh.Faces.Count;
            for (int i = 0; i < facecount; i++)
            {
                var mf = inputMesh.Faces[i];
                if (mf.IsQuad)
                {
                    double dist1 = inputMesh.Vertices[mf.A].DistanceTo(inputMesh.Vertices[mf.C]);
                    double dist2 = inputMesh.Vertices[mf.B].DistanceTo(inputMesh.Vertices[mf.D]);
                    if (dist1 > dist2)
                    {
                        triMesh.Faces.AddFace(mf.A, mf.B, mf.D);
                        triMesh.Faces.AddFace(mf.B, mf.C, mf.D);
                    }
                    else
                    {
                        triMesh.Faces.AddFace(mf.A, mf.B, mf.C);
                        triMesh.Faces.AddFace(mf.A, mf.C, mf.D);
                    }
                }
                else
                {
                    triMesh.Faces.AddFace(mf.A, mf.B, mf.C);
                }
            }

            triMesh.Vertices.AddVertices(inputMesh.Vertices);
            triMesh.Unweld(0, false);
            triMesh.RebuildNormals();
            triMesh.UnifyNormals();
            return triMesh;
        }

        #endregion Mesh Utilities

        #region Vector Utilities

        /// <summary>
        /// Average Unitized Vector from a Vector Array - unitizes vector result at each step
        /// Useful for Vertex normal calculations
        /// </summary>
        /// <param name="vectors"></param>
        /// <returns>The average normalized vector</returns>
        private static Vector3d UnitizedAverage(IEnumerable<Vector3d> vectors)
        {
            Vector3d averageNormal = Vector3d.Zero;
            foreach (Vector3d v in vectors)
            {
                averageNormal += v;
                averageNormal.Unitize();
            }

            return averageNormal;
        }

        /// <summary>
        /// Removes duplicate ghVectors (within tolerance) from an array, returning only unique ghVectors
        /// </summary>
        /// <param name="vectors"></param>
        /// <param name="angleTolerance"></param>
        /// <returns>Array of unique ghVectors</returns>
        private static Vector3d[] GetUniqueVectors(Vector3d[] vectors, double angleTolerance)
        {
            List<Vector3d> result = new List<Vector3d>();

            for (int i = 0; i < vectors.Length; i++)
            {
                bool isDuplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if (Vector3d.VectorAngle(vectors[i], vectors[j]) < angleTolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    result.Add(vectors[i]);

                }
            }

            return result.ToArray();
        }
        #endregion Vector Utilities

        #region Color Utilities

        /// <summary>
        /// Linear interpolate between Colors
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        private static Color LerpColor(Color a, Color b, double t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;

            return Color.FromArgb((int)(a.R * (1 - t) + b.R * t), (int)(a.G * (1 - t) + b.G * t), (int)(a.B * (1 - t) + b.B * t));

        }

        #endregion Color Utilities

        #region Math Utilities

        /// <summary>
        /// Converts an angle in degrees to radians
        /// </summary>
        /// <param name="angle">The angle to convert (in degrees)</param>
        /// <returns></returns>
        internal static double DegreesToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        /// <summary>
        /// Converts an angle in radians to degrees
        /// </summary>
        /// <param name="angle">The angle to convert (in radians)</param>
        /// <returns></returns>
        internal static double RadiansToDegrees(double angle)
        {
            return (180 / Math.PI) * angle;
        }

        /// <summary>
        /// Normalizes an array of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        internal static double[] NormalizeRange(double[] values)
        {
            double vMin = values.Min();
            double vMax = values.Max();

            // if ghNumbers are identical, prevent division by 0
            if (vMin == vMax) return values.Select(x => 0.5).ToArray();

            double den = 1 / (vMax - vMin);

            double[] normVal = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
                normVal[i] = (values[i] - vMin) * den;

            return normVal;
        }

        /// <summary>
        /// Normalizes a List of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        internal static List<double> NormalizeRange(List<double> values)
        {
            double vMin = values.Min();
            double vMax = values.Max();

            // if ghNumbers are identical, prevent division by 0
            if (vMin == vMax) return values.Select(x => 0.5).ToList();

            double den = 1 / (vMax - vMin);

            List<double> normVal = new List<double>();

            for (int i = 0; i < values.Count; i++)
                normVal.Add((values[i] - vMin) * den);

            return normVal;
        }

        /// <summary>
        /// Normalizes a Jagged array of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        private static double[][] NormalizeRanges(double[][] values)
        {
            int nVals = values[0].Length;
            double[] vMin = new double[nVals], vMax = new double[nVals];
            double[] tMin = new double[nVals], tMax = new double[nVals];

            // populate arrays
            for (int j = 0; j < nVals; j++)
            {
                vMin[j] = Double.MaxValue;
                vMax[j] = Double.MinValue;
            }

            // find vMin and vMax for each set of values per point
            for (int i = 0; i < values.Length; i++)
                for (int j = 0; j < nVals; j++)
                {
                    tMin[j] = values[i][j];
                    tMax[j] = values[i][j];
                    if (tMin[j] < vMin[j]) vMin[j] = tMin[j];
                    if (tMax[j] > vMax[j]) vMax[j] = tMax[j];
                }

            double[] den = new double[nVals];

            for (int j = 0; j < nVals; j++)
                den[j] = 1 / (vMax[j] - vMin[j]);

            double[][] normVal = new double[values.Length][];

            // recompute values, preventing division by 0
            for (int i = 0; i < values.Length; i++)
                for (int j = 0; j < values[i].Length; j++)
                    normVal[i][j] = vMin[j] == vMax[j] ? 0.5 : (values[i][j] - vMin[j]) * den[j];

            return normVal;
        }

        /// <summary>
        /// Normalizes a DataTree of real numbers
        /// </summary>
        /// <param name="values">the values data tree</param>
        /// <returns></returns>
        internal static DataTree<double> NormalizeRanges(DataTree<double> values)
        {
            IList<GH_Path> paths = values.Paths;
            double[][] valuesArray = ToJaggedArray(values);
            double[][] normValuesArray = NormalizeRanges(valuesArray);

            DataTree<double> normVal = ToDataTree(normValuesArray);
            for (int i = 0; i < normVal.Paths.Count; i++)
                normVal.Paths[i] = paths[i];

            return normVal;
        }

        #endregion Math Utilities

        #region Data Utilities

        /// <summary>
        /// Converts a jagged array into a DataTree of the same type
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="jaggedArray">A jagged array to convert to DataTree</param>
        /// <returns>A DataTree of type Handle</returns>
        public static DataTree<T> ToDataTree<T>(T[][] jaggedArray)
        {
            DataTree<T> data = new DataTree<T>();

            for (int i = 0; i < jaggedArray.Length; i++)
                data.AddRange(jaggedArray[i].Select(d => d).ToList(), new GH_Path(i));

            return data;
        }

        /// <summary>
        /// Converts a list of arrays into a DataTree of the same type
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="listOfArrays">A list of Arrays to convert to DataTree</param>
        /// <returns>A DataTree of type Handle</returns>
        public static DataTree<T> ToDataTree<T>(List<T[]> listOfArrays)
        {
            DataTree<T> data = new DataTree<T>();

            for (int i = 0; i < listOfArrays.Count; i++)
                data.AddRange(listOfArrays[i].Select(d => d).ToList(), new GH_Path(i));

            return data;
        }

        /// <summary>
        /// Converts a DataTree into a jagged array of the same type\nThe array length is equal to the number of branches, regardless of paths
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="tree">A DataTree to convert to jagged array</param>
        /// <returns>A Jagged Array of type Handle</returns>
        public static T[][] ToJaggedArray<T>(DataTree<T> tree)
        {

            T[][] jArray = new T[tree.BranchCount][];

            for (int i = 0; i < tree.BranchCount; i++)
                jArray[i] = tree.Branches[i].ToArray();

            return jArray;
        }

        /// <summary>
        /// Converts a DataTree into a list of arrays of the same type\nThe list count is equal to the number of branches, regardless of paths
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="tree">A DataTree to convert to List of arrays</param>
        /// <returns>A List of arrays of type Handle</returns>
        public static List<T[]> ToListOfArrays<T>(DataTree<T> tree)
        {

            List<T[]> arraysList = new List<T[]>();

            for (int i = 0; i < tree.BranchCount; i++)
                arraysList.Add(tree.Branches[i].ToArray());

            return arraysList;
        }

        /// <summary>
        /// Clones a Dictionary and its values
        /// </summary>
        /// <typeparam Name="TKey"></typeparam>
        /// <typeparam Name="TValue"></typeparam>
        /// <param name="original"></param>
        /// <returns>The cloned Dictionary</returns>
        /// <remarks>as seen here: https://stackoverflow.com/questions/139592/what-is-the-best-way-to-clone-deep-copy-a-net-generic-dictionarystring-t</remarks>
        public static Dictionary<TKey, TValue> CloneDictionaryWithValues<TKey, TValue>(Dictionary<TKey, TValue> original)
        {
            Dictionary<TKey, TValue> copy = new Dictionary<TKey, TValue>(original.Count, original.Comparer);
            foreach (KeyValuePair<TKey, TValue> entry in original)
            {
                copy.Add(entry.Key, (TValue)entry.Value);
            }
            return copy;
        }

        /// <summary>
        /// Renames a key in a Dictionary
        /// </summary>
        /// <typeparam Name="TKey"></typeparam>
        /// <typeparam Name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="fromKey"></param>
        /// <param name="toKey"></param>
        /// <returns>true if successful</returns>
        /// <remarks>as seen here: https://stackoverflow.com/questions/6499334/best-way-to-change-dictionary-key</remarks>
        public static bool RenameKey<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey fromKey, TKey toKey)
        {
            TValue value = dictionary[fromKey];
            if (!dictionary.Remove(fromKey))
                return false;
            dictionary[toKey] = value;
            return true;
        }

        /// <summary>
        /// Converts a GH_Structure of GH_Number to a DataTree of double
        /// </summary>
        /// <param name="ghNumbers"></param>
        /// <returns></returns>
        public static DataTree<double> GHS2TreeDoubles(GH_Structure<GH_Number> ghNumbers)
        {
            DataTree<double> rhNumbers = new DataTree<double>();

            if (ghNumbers != null)
                for (int i = 0; i < ghNumbers.Branches.Count; i++)
                    rhNumbers.AddRange(ghNumbers.Branches[i].Select(n => n.Value).ToList(), ghNumbers.Paths[i]);

            return rhNumbers;
        }

        /// <summary>
        /// Converts a GH_Structure of GH_Vector to a DataTree of Vector3d
        /// </summary>
        /// <param name="ghVectors"></param>
        /// <returns></returns>
        public static DataTree<Vector3d> GHS2TreeVectors(GH_Structure<GH_Vector> ghVectors)
        {
            DataTree<Vector3d> rhVectors = new DataTree<Vector3d>();

            if (ghVectors != null)
                for (int i = 0; i < ghVectors.Branches.Count; i++)
                    rhVectors.AddRange(ghVectors.Branches[i].Select(n => n.Value).ToList(), ghVectors.Paths[i]);

            return rhVectors;
        }

        /// <summary>
        /// Converts a GH_Structure of GH_Integer to a DataTree of integer
        /// </summary>
        /// <param name="ghInt"></param>
        /// <returns></returns>
        public static DataTree<int> GHS2TreeIntegers(GH_Structure<GH_Integer> ghInt)
        {
            DataTree<int> rhInt = new DataTree<int>();

            if (ghInt != null)
                for (int i = 0; i < ghInt.Branches.Count; i++)
                    rhInt.AddRange(ghInt.Branches[i].Select(n => n.Value).ToList(), ghInt.Paths[i]);

            return rhInt;
        }

        #endregion Data Utilities

    }
}
