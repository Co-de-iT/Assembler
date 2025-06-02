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
        internal static bool IsAOCollidingWithAssemblage(Assemblage AOa, AssemblyObject sO, out int[] neighbourIndexes)
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
                neighList.Add(AOa.centroidsAInds[args.Id]);
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
        /// <returns>true if a collision exists</returns>
        internal static bool IsAOCollidingWithNeighbours(AssemblyObject AO, List<AssemblyObject> neighbours)
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
        /// <param name="AO"></param>
        /// <param name="otherAO"></param>
        /// <returns>True if objects are colliding or one contains the other</returns>
        public static bool IsAOCollidingWithAnother(AssemblyObject AO, AssemblyObject otherAO)
        {
            // mesh collision test
            if (Intersection.MeshMeshFast(AO.CollisionMesh, otherAO.OffsetMesh).Length > 0)
                return true;

            // mesh inclusion test - uses first vertex as Point3d for inclusion check
            Point3d AOfirstVertex = AO.OffsetMesh.Vertices[0];
            if (otherAO.CollisionMesh.IsPointInside(AOfirstVertex, Constants.RhinoAbsoluteTolerance, true))
                return true;

            Point3d otherAOfirstVertex = otherAO.OffsetMesh.Vertices[0];
            if (AO.CollisionMesh.IsPointInside(otherAOfirstVertex, Constants.RhinoAbsoluteTolerance, true))
                return true;

            return false;
        }

        /// <summary>
        /// Collision Check between 2 Meshes
        /// </summary>
        /// <param name="AO"><see cref="AssemblyObject"/> to check</param>
        /// <param name="surroundings">Mesh of external geometry</param>
        /// <returns>True if objects are colliding or one contains the other</returns>
        public static bool IsAOCollidingWithMeshes(AssemblyObject AO, Mesh surroundings)
        {
            // mesh collision test
            if (Intersection.MeshMeshFast(AO.CollisionMesh, surroundings).Length > 0)
                return true;

            // mesh inclusion test - uses first vertex as Point3d for inclusion check
            Point3d testFirstVertex = AO.CollisionMesh.Vertices[0];
            if (surroundings.IsPointInside(testFirstVertex, Constants.RhinoAbsoluteTolerance, true))
                return true;

            Point3d sFirstVertex = surroundings.Vertices[0];
            if (AO.CollisionMesh.IsPointInside(sFirstVertex, Constants.RhinoAbsoluteTolerance, true))
                return true;

            return false;
        }

        private static bool BoundingBoxIntersect(BoundingBox a, BoundingBox b)
        {
            return (a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
                a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
                a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z);
        }

        /// <summary>
        /// Checks environment compatibility of an AssemblyObject
        /// </summary>
        /// <param name="AO"><see cref="AssemblyObject"/> to verify</param>
        /// <param name="EnvironmentMeshes"></param>
        /// <returns>true if an object is not compatible with the <see cref="MeshEnvironment"/>s</returns>
        /// <remarks>An eventual Container is checked using collision mode</remarks>
        //internal static bool EnvironmentClashCollision(AssemblyObject AO, List<MeshEnvironment> EnvironmentMeshes)
        //{
        //    foreach (MeshEnvironment mEnv in EnvironmentMeshes)
        //    {

        //        switch (mEnv.Type)
        //        {
        //            case EnvironmentType.Void: // controls only centroid in/out
        //                if (mEnv.IsPointInvalid(AO.ReferencePlane.Origin)) return true;
        //                break;
        //            case EnvironmentType.Solid:
        //                if (mEnv.CollisionCheck(AO.CollisionMesh)) return true;
        //                goto case EnvironmentType.Void;
        //            case EnvironmentType.Container:
        //                goto case EnvironmentType.Solid;
        //        }
        //    }

        //    return false;
        //}

        /// <summary>
        /// Checks environment compatibility of an AssemblyObject
        /// </summary>
        /// <param name="AO"><see cref="AssemblyObject"/> to verify</param>
        /// <param name="EnvironmentMeshes"></param>
        /// <returns>true if an object is not compatible with the <see cref="MeshEnvironment"/>s</returns>
        /// <remarks>An eventual Container is checked using inclusion mode</remarks>
        //internal static bool EnvironmentClashInclusion(AssemblyObject AO, List<MeshEnvironment> EnvironmentMeshes)
        //{
        //    foreach (MeshEnvironment mEnv in EnvironmentMeshes)
        //    {

        //        switch (mEnv.Type)
        //        {
        //            case EnvironmentType.Void: // controls only centroid in/out
        //                if (mEnv.IsPointInvalid(AO.ReferencePlane.Origin)) return true;
        //                break;
        //            case EnvironmentType.Solid:
        //                if (mEnv.CollisionCheck(AO.CollisionMesh)) return true;
        //                goto case EnvironmentType.Void;
        //            case EnvironmentType.Container:
        //                goto case EnvironmentType.Void;
        //        }
        //    }

        //    return false;
        //}

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
            //Line ray;

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

                        // ANY Handle (type independent) who is accidentally in contact is registered as secondary connection (Occupancy = 2)
                        // if Handles are of the same type... (OPTIONAL CHECK)
                        // if (AO.Handles[k].type == AOa.assemblyObjects[index].Handles[j].type)
                        // ...and their distance is below absolute tolerance...
                        if (AOa.AssemblyObjects.Branch(neighSeqInd)[0].Handles[j].SenderPlane.Origin.DistanceToSquared(AO.Handles[k].SenderPlane.Origin) < Constants.RhinoAbsoluteToleranceSquared)
                        {
                            // ...update Handles
                            HandleUtils.UpdateHandlesOnConnection(AO, k, AOa.AssemblyObjects.Branch(neighSeqInd)[0], j, 2);
                            connect = true;
                            break;
                        }
                    }
                    // if a connection happened, go to next handle in neighbour
                    if (connect) continue;

                    // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                    obstruct |= HandleObstructionCheck(AOa.AssemblyObjects.Branch(neighSeqInd)[0],
                        AOa.AssemblyObjects[new GH_Path(AO_AInd), 0], /*neighAInd, AO_AInd,*/ j);
                }

                // CHECK OBSTRUCTION OF sO HANDLES BY NEIGHBOUR
                for (int k = 0; k < AO.Handles.Length; k++)
                {
                    // if sO handle is not available continue
                    if (AO.Handles[k].Occupancy != 0) continue;

                    obstruct |= HandleObstructionCheck(AOa.AssemblyObjects[new GH_Path(AO_AInd), 0],
                        AOa.AssemblyObjects.Branch(neighSeqInd)[0], /*AO_AInd, neighAInd,*/ k);
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
            //Line ray;

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
                                if (AOList[j].Handles[k].SenderPlane.Origin.DistanceToSquared(AOList[i].Handles[p].SenderPlane.Origin) < Constants.RhinoAbsoluteToleranceSquared)
                                {
                                    // ...update Handles (these are all considered secondary connections)
                                    HandleUtils.UpdateHandlesOnConnection(AOList[i], p, AOList[j], k, 2);
                                    connect = true;
                                    break;
                                }
                        }
                        // if a connection happened, go to next handle in neighbour
                        if (connect) continue;

                        // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                        obstruct = HandleObstructionCheck(AOList[j], AOList[i], j, i, k);
                    }

                    // CHECK OBSTRUCTION OF sO HANDLES BY NEIGHBOUR
                    for (int k = 0; k < AOList[i].Handles.Length; k++)
                    {
                        // if sO handle is not available continue
                        if (AOList[i].Handles[k].Occupancy != 0) continue;

                        obstruct = HandleObstructionCheck(AOList[i], AOList[j], i, j, k);
                    }
                }
            }
            return obstruct;
        }
        private static bool HandleObstructionCheck(AssemblyObject AO, AssemblyObject otherAO, int handleInd)
        {
            return HandleObstructionCheck(AO, otherAO, AO.AInd, otherAO.AInd, handleInd);
        }
        private static bool HandleObstructionCheck(AssemblyObject AO, AssemblyObject otherAO, int AInd, int otherInd, int handleInd)
        {
            bool obstruct = false;
            Line ray;
            Plane hSender = AO.Handles[handleInd].SenderPlane;

            // shoot a line from the handle
            ray = new Line(hSender.Origin - (hSender.ZAxis * Constants.ObstructionRayOffset), hSender.ZAxis * Constants.ObstructionRayLength);

            // if it intercepts the otherAO
            if (Intersection.MeshLine(otherAO.CollisionMesh, ray, out _).Length != 0)
            {
                // change Handle occupancy to -1 (occluded) and add otherAO index to its NeighbourObject
                AO.Handles[handleInd].Occupancy = -1;
                AO.Handles[handleInd].NeighbourObject = otherInd;
                // update otherAO OccludedNeighbours status
                otherAO.OccludedNeighbours.Add(new int[] { AInd, handleInd });
                // change obstruct variable status
                obstruct = true;
            }

            return obstruct;
        }

        #endregion Obstruction Utilities

        /// <summary>
        /// Extract the AOset from a list of AssemblyObjects
        /// </summary>
        /// <param name="AOs">The initial list of <see cref="AssemblyObject"/></param>
        /// <returns>the list of unique <see cref="AssemblyObject"/>s constituting the set</returns>
        public static List<AssemblyObject> ExtractAOSet(List<AssemblyObject> AOs)
        {
            List<AssemblyObject> AOSet = new List<AssemblyObject>();

            if (AOs.Count == 0) return null;

            Dictionary<int, int> uniqueTypes = new Dictionary<int, int>
            {
                { AOs[0].Type, 0 } // type, list index
            };

            for (int i = 1; i < AOs.Count; i++)
            {
                if (!uniqueTypes.ContainsKey(AOs[i].Type))
                    uniqueTypes.Add(AOs[i].Type, i);
            }

            foreach (KeyValuePair<int, int> pair in uniqueTypes)
            {
                AOSet.Add(AssemblyObjectUtils.Reset(AOs[pair.Value]));
            }

            return AOSet;
        }

        /// <summary>
        /// Builds the Dictionary of AssemblyObjects fomr an AOset
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
            Assemblage clonedAOa = new Assemblage
            {
                // cloning by value data (primitives and structs)
                HeuristicsSettings = AOa.HeuristicsSettings,
                ExogenousSettings = AOa.ExogenousSettings,
                currentHeuristicsIndex = AOa.currentHeuristicsIndex,
                CollisionRadius = AOa.CollisionRadius,
                nextAInd = AOa.nextAInd,
                CheckWorldZLock = AOa.CheckWorldZLock,
                UseSupports = AOa.UseSupports,
                E_sb = AOa.E_sb,
                //E_sandbox = AOa.E_sandbox;
                AOSet = new AssemblyObject[AOa.AOSet.Length]
            };
            // clone AOSet
            for (int i = 0; i < AOa.AOSet.Length; i++)
                clonedAOa.AOSet[i] = AssemblyObjectUtils.Clone(AOa.AOSet[i]);

            // clone dictionary
            clonedAOa.AOSetDictionary = new Dictionary<string, int>(AOa.AOSetDictionary);

            // clone DataTrees
            clonedAOa.heuristicsTree = new DataTree<Rule>(AOa.heuristicsTree, r => r);
            clonedAOa.AssemblageRules = new DataTree<string>(AOa.AssemblageRules, r => r);
            clonedAOa.ReceiverAIndexes = new DataTree<int>(AOa.ReceiverAIndexes, i => i);
            clonedAOa.AssemblyObjects = new DataTree<AssemblyObject>(AOa.AssemblyObjects, ao => AssemblyObjectUtils.CloneWithConnectivityAndValues(ao));

            // clone Lists
            clonedAOa.availableObjectsAInds = AOa.availableObjectsAInds.Select(av => av).ToList();
            clonedAOa.unreachableObjectsAInds = AOa.unreachableObjectsAInds.Select(ur => ur).ToList();
            clonedAOa.availableReceiverValues = AOa.availableReceiverValues.Select(arv => arv).ToList();
            clonedAOa.centroidsAInds = AOa.centroidsAInds.Select(c => c).ToList();

            clonedAOa.centroidsTree = new RTree();
            for (int i = 0; i < AOa.AssemblyObjects.BranchCount; i++)
            {
                // add object to the centroids tree
                // future implementation: if object has children, insert all children centroids under the same AInd
                clonedAOa.centroidsTree.Insert(AOa.AssemblyObjects.Branches[i][0].ReferencePlane.Origin, AOa.AssemblyObjects.Branches[i][0].AInd);
            }
            // candidateObjects doesn't need cloning
            return clonedAOa;
        }

        #region Topology Utilities
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
            int neighAInd;
            GH_Path neighPath;
            bool updateNeighbourStatus;

            // . . . Topology operations
            // update connected AO Handles
            for (int i = 0; i < AO.Handles.Length; i++)
            {
                // if handle is free, continue
                if (AO.Handles[i].Occupancy == 0) continue;

                // AInd of neighbour object
                neighAInd = AO.Handles[i].NeighbourObject;
                neighPath = new GH_Path(neighAInd);
                updateNeighbourStatus = false;

                // free connected or contact Handles
                if (AO.Handles[i].Occupancy >= 1)
                {
                    AOa.AssemblyObjects[neighPath, 0].Handles[AO.Handles[i].NeighbourHandle].Occupancy = 0;
                    AOa.AssemblyObjects[neighPath, 0].Handles[AO.Handles[i].NeighbourHandle].NeighbourObject = -1;
                    AOa.AssemblyObjects[neighPath, 0].Handles[AO.Handles[i].NeighbourHandle].NeighbourHandle = -1;
                    // flag for neighbour status update
                    updateNeighbourStatus = true;
                }
                // update occluding objects
                else if (AO.Handles[i].Occupancy == -1)
                {
                    // scan OccludedNeighbours list of neighbour occluded object
                    for (int j = AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours.Count - 1; j >= 0; j--)
                    {
                        // if AInd and Handle match
                        if (AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours[j][0] == AO.AInd &&
                           AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours[j][1] == i)
                        {
                            // remove entry
                            AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours.RemoveAt(j);
                            // flag for neighbour status update
                            updateNeighbourStatus = true;
                        }
                    }
                    //AOa.AssemblyObjects[neighPath, 0].OccludedNeighbours.Remove(new int[] { AO.AInd, i });
                }

                // update neighbour available/unreachable status
                if (updateNeighbourStatus)
                    MakeAOAvailable(AOa, neighAInd, neighPath);
            }

            // check its occluded objects
            for (int i = 0; i < AO.OccludedNeighbours.Count; i++)
            {
                GH_Path occludePath = new GH_Path(AO.OccludedNeighbours[i][0]);
                int occludedAInd = AO.OccludedNeighbours[i][0];
                int occludedHandleIndex = AO.OccludedNeighbours[i][1];
                // free occluded handle
                AOa.AssemblyObjects[occludePath, 0].Handles[occludedHandleIndex].Occupancy = 0;
                AOa.AssemblyObjects[occludePath, 0].Handles[occludedHandleIndex].NeighbourObject = -1;
                // Update available/unreachable status
                MakeAOAvailable(AOa, occludedAInd, occludePath);
            }

            // remove from used rules
            AOa.AssemblageRules.RemovePath(AO.AInd);
            // remove from used receiver indexes
            AOa.ReceiverAIndexes.RemovePath(AO.AInd);

            // check if in available-unreachable objects and remove
            if (AOa.availableObjectsAInds.Contains(AO.AInd))
            {
                int avSeq = AOa.availableObjectsAInds.IndexOf(AO.AInd);
                AOa.availableObjectsAInds.Remove(AO.AInd);
                AOa.availableReceiverValues.RemoveAt(avSeq);
            }
            else if (AOa.unreachableObjectsAInds.Contains(AO.AInd)) AOa.unreachableObjectsAInds.Remove(AO.AInd);

            // remove from centroids tree
            AOa.centroidsTree.Remove(AO.ReferencePlane.Origin, AO.AInd);

            // remove from AssemblyObject list
            AOa.AssemblyObjects.RemovePath(AOPath);//AO.AInd);

            return true;
        }

        /// <summary>
        /// Remove a collection of <see cref="AssemblyObject"/>s from an <see cref="Assemblage"/>, updating Topology information
        /// </summary>
        /// <param name="AOa">The Assemblage to remove from</param>
        /// <param name="AIndexes">the Assemblage Indexes of the AssemblyObjects to remove</param>
        /// <returns>an array of booleans, one for each index - true if successful, false otherwise</returns>
        public static bool[] RemoveAssemblyObjects(Assemblage AOa, IEnumerable<int> AIndexes)
        {
            bool[] result = new bool[AIndexes.Count()];
            for (int i = 0; i < result.Length; i++)
                result[i] = RemoveAssemblyObject(AOa, AIndexes.ElementAt<int>(i));
            return result;
        }

        /// <summary>
        /// Updates AO status, eventually removing it from Unreachable and adding it to Available list if not there already
        /// </summary>
        /// <param name="AOa">The assemblage to modify</param>
        /// <param name="AInd">Index of the <see cref="AssemblyObject"/> to update</param>
        /// <param name="AOPath">The <see cref="AssemblyObject"/> path</param>
        private static void MakeAOAvailable(Assemblage AOa, int AInd, GH_Path AOPath)
        {
            // if AO AInd is in unreachable remove from list
            if (AOa.unreachableObjectsAInds.Contains(AInd)) AOa.unreachableObjectsAInds.Remove(AInd);
            // if not in available yet add to available list and add its receiver value to the list
            if (!AOa.availableObjectsAInds.Contains(AInd))
            {
                AOa.availableObjectsAInds.Add(AInd);
                AOa.availableReceiverValues.Add(AOa.AssemblyObjects[AOPath, 0].ReceiverValue);
            }
        }

        /// <summary>
        /// Updates AO status, eventually removing it from Unreachable and adding it to Available list if not there already
        /// </summary>
        /// <param name="AOa">The assemblage to modify</param>
        /// <param name="AInd">Index of the <see cref="AssemblyObject"/> to update</param>
        public static void MakeAOAvailable(Assemblage AOa, int AInd) => MakeAOAvailable(AOa, AInd, new GH_Path(AInd));

        #endregion Topology Utilities

        #region Compute Value Methods


        #endregion Compute Value Methods

        #region Select Value Methods

        //public static int SelectRandomIndex(double[] values) => (int)(MathUtils.rnd.NextDouble() * values.Length);

        //public static int SelectMinIndex(double[] values)
        //{
        //    double min = values[0];// double.MaxValue;
        //    int minindex = 0;// -1;
        //    for (int i = 1; i < values.Length; i++)
        //    {
        //        if (values[i] < min)
        //        {
        //            min = values[i];
        //            minindex = i;
        //        }
        //    }

        //    return minindex;
        //}

        //public static int SelectMaxIndex(double[] values)
        //{
        //    double max = values[0];//double.MinValue;
        //    int maxindex = 0;//-1;
        //    for (int i = 1; i < values.Length; i++)
        //    {
        //        if (values[i] > max)
        //        {
        //            max = values[i];
        //            maxindex = i;
        //        }
        //    }

        //    return maxindex;
        //}

        //public static int SelectWRCIndex(double[] values)
        //{
        //    // Weighted Random Choice among valid rules
        //    int[] iWeights = values.Select(v => (int)(v * 1000)).ToArray();
        //    int[] indexes = new int[values.Length];
        //    for (int i = 0; i < values.Length; i++)
        //        indexes[i] = i;

        //    return MathUtils.WeightedRandomChoice(indexes, iWeights);
        //}

        #endregion Select Value Methods
    }
}
