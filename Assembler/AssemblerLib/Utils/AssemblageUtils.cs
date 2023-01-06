using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Utils
{
    public static class AssemblageUtils
    {

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
                if (AOcollision.IsPointInside(sOfirstVertex, Constants.RhinoAbsoluteTolerance, true))
                    return true;
                // check if neighbour is inside sender object
                // get neighbour's OffsetMesh first vertex & check if it's inside
                neighFirstVertex = AOa.AssemblyObjects[neighPath, 0].OffsetMesh.Vertices[0];
                if (sO.CollisionMesh.IsPointInside(neighFirstVertex, Constants.RhinoAbsoluteTolerance, true))
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
                if (neighbour.CollisionMesh.IsPointInside(AOfirstVertex, Constants.RhinoAbsoluteTolerance, true))
                    return true;

                neighFirstVertex = neighbour.OffsetMesh.Vertices[0];

                if (AO.CollisionMesh.IsPointInside(neighFirstVertex, Constants.RhinoAbsoluteTolerance, true))
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
            if (sender.CollisionMesh.IsPointInside(rOfirstVertex, Constants.RhinoAbsoluteTolerance, true))
                return true;

            Point3d sOfirstVertex = sender.OffsetMesh.Vertices[0];
            if (receiver.CollisionMesh.IsPointInside(sOfirstVertex, Constants.RhinoAbsoluteTolerance, true))
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
            if (surroundings.IsPointInside(testFirstVertex, Constants.RhinoAbsoluteTolerance, true))
                return true;

            Point3d sFirstVertex = surroundings.Vertices[0];
            if (test.IsPointInside(sFirstVertex, Constants.RhinoAbsoluteTolerance, true))
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
                        if (AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].Sender.Origin.DistanceToSquared(AO.Handles[k].Sender.Origin) < Constants.RhinoAbsoluteToleranceSquared)
                        {
                            // ...update Handles
                            HandleUtils.UpdateHandlesOnConnection(AO, k, AOa.AssemblyObjects.Branch(neighSeqInd)[0], j);
                            connect = true;
                            break;
                        }
                    }
                    // if a connection happened, go to next handle in neighbour
                    if (connect) continue;

                    // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                    // shoot a line from the handle
                    Plane hSender = AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].Sender;
                    ray = new Line(hSender.Origin - (hSender.ZAxis * Constants.ObstructionRayOffset), hSender.ZAxis * Constants.ObstructionRayLength);

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
                    ray = new Line(AO.Handles[k].Sender.Origin - (AO.Handles[k].Sender.ZAxis * Constants.ObstructionRayOffset), AO.Handles[k].Sender.ZAxis * Constants.ObstructionRayLength);

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
                                if (AOList[j].Handles[k].Sender.Origin.DistanceToSquared(AOList[i].Handles[p].Sender.Origin) < Constants.RhinoAbsoluteToleranceSquared)
                                {
                                    // ...update Handles
                                    HandleUtils.UpdateHandlesOnConnection(AOList[i], p, AOList[j], k);
                                    connect = true;
                                    break;
                                }
                        }
                        // if a connection happened, go to next handle in neighbour
                        if (connect) continue;

                        // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                        // shoot a line from the handle
                        ray = new Line(AOList[j].Handles[k].Sender.Origin - (AOList[j].Handles[k].Sender.ZAxis * 0.1), AOList[j].Handles[k].Sender.ZAxis * 1.5);

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
                clonedAOa.AOSet[i] = AssemblyObjectUtils.Clone(AOa.AOSet[i]);

            // clone dictionary
            clonedAOa.AOSetDictionary = new Dictionary<string, int>(AOa.AOSetDictionary);

            // clone DataTrees
            clonedAOa.heuristicsTree = new DataTree<Rule>(AOa.heuristicsTree, r => r);
            clonedAOa.AssemblageRules = new DataTree<string>(AOa.AssemblageRules, r => r);
            clonedAOa.ReceiverAIndexes = new DataTree<int>(AOa.ReceiverAIndexes, i => i);
            clonedAOa.AssemblyObjects = new DataTree<AssemblyObject>(AOa.AssemblyObjects, ao => AssemblyObjectUtils.CloneWithConnectivity(ao));
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

    }
}
