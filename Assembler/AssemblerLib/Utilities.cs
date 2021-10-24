using Grasshopper;
using Grasshopper.GUI.Gradient;
using Grasshopper.Kernel;
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
using System.Threading;
using System.Threading.Tasks;

namespace AssemblerLib
{

    /// <summary>
    /// A static Utilities class grouping some useful methods
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// Tolerance from Rhino file
        /// </summary>
        public static readonly double RhinoAbsoluteTolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
        /// <summary>
        /// Tolerance squared - for fast neighbour search
        /// </summary>
        public static readonly double RhinoAbsoluteToleranceSquared = Math.Pow(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 2);

        public static readonly GH_Gradient historyGradient = new GH_Gradient(new double[] { 0.0, 0.5, 0.9, 1.0 },
            new Color[] { Color.Black, Color.FromArgb(56, 136, 150), Color.FromArgb(186, 224, 224), Color.White });
        public static readonly GH_Gradient zHeightGradient = new GH_Gradient(new double[] { 0.0, 0.33, 0.66, 1.0 },
            new Color[] { Color.Black, Color.FromArgb(150, 66, 114), Color.FromArgb(224, 186, 187), Color.White });
        public static readonly GH_Gradient densityGradient = new GH_Gradient(new double[] { 0.0, 1.0 }, new Color[] { Color.White, Color.Red });

        // AssemblyObject Type palette has a max of 24 colors - which is already WAY TOO MANY!!!
        public static readonly Color[] AOTypePalette = new Color[] {
        Color.FromArgb(192,57,43), Color.FromArgb(100,100,100), Color.FromArgb(52,152,219), Color.FromArgb(253,188,75),
        Color.FromArgb(155,89,182), Color.FromArgb(46,204,113), Color.FromArgb(49,54,59), Color.FromArgb(231,76,60),
        Color.FromArgb(189,195,199), Color.FromArgb(201,206,59), Color.FromArgb(142,68,173), Color.FromArgb(52,73,94),
        Color.FromArgb(29,153,19), Color.FromArgb(237,21,21), Color.FromArgb(127,140,141), Color.FromArgb(61,174,233),
        Color.FromArgb(243,156,31), Color.FromArgb(41,128,190), Color.FromArgb(35,38,41), Color.FromArgb(252,252,252),
        Color.FromArgb(218,68,83), Color.FromArgb(22,160,133), Color.FromArgb(149,165,166), Color.FromArgb(44,62,80)};

        public static readonly Color[] srPalette = new Color[] { Color.SlateGray, Color.FromArgb(229, 229, 220) }; // receiver, sender
                                                                                                                   //public static readonly Color[] objPalette_OLD = new Color[] {  Color.Goldenrod, Color.HotPink, Color.YellowGreen, Color.Blue, Color.DarkKhaki, Color.CadetBlue,
                                                                                                                   //    Color.Plum, Color.LightSteelBlue, Color.PaleTurquoise, Color.Olive, Color.Violet, Color.DimGray, Color.Snow, Color.DarkSlateGray, Color.DarkGoldenrod, Color.DarkOliveGreen};

        // get System Color in a List
        // from https://www.codeproject.com/Questions/826358/How-to-choose-a-random-color-from-System-Drawing-C
        public static readonly List<KnownColor> colorlist = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().ToList();

        #region Assemblage Utilities

        /// <summary>
        /// Collision Check in the assemblage for a given <see cref="Assemblage"/> and <see cref="AssemblyObject"/>
        /// </summary>
        /// <param name="AOa">The <see cref="Assemblage"/> to check</param>
        /// <param name="sO">The sender <see cref="AssemblyObject"/></param>
        /// <returns></returns>
        public static bool CollisionCheckAssemblage(Assemblage AOa, AssemblyObject sO)
        {
            // get first vertex as Point3d for inclusion check
            Point3d neighFirstVertex, sOfirstVertex = sO.offsetMesh.Vertices[0];

            // find neighbours in Assemblage 
            List<int> neighList = new List<int>();
            // collision radius is a field of AssemblyObjects
            AOa.centroidsTree.Search(new Sphere(sO.referencePlane.Origin, sO.collisionRadius), (object sender, RTreeEventArgs e) =>
            {
                // recover the AssemblyObject index related to the found centroid
                neighList.Add(AOa.centroidsAO[e.Id]);
            });

            // check for no neighbours
            if (neighList.Count == 0)
                return false;

            // check for collisions + inclusion (sender in receiver, receiver in sender)
            foreach (int index in neighList)
            {
                if (Intersection.MeshMeshFast(sO.offsetMesh, AOa.assemblyObjects[index].collisionMesh).Length > 0)
                    return true;
                // check if sender object is inside neighbour
                if (AOa.assemblyObjects[index].collisionMesh.IsPointInside(sOfirstVertex, RhinoAbsoluteTolerance, true))
                    return true;
                // check if neighbour is inside sender object
                // get neighbour's OffsetMesh first vertex & check if it's inside
                neighFirstVertex = AOa.assemblyObjects[index].offsetMesh.Vertices[0];
                if (sO.collisionMesh.IsPointInside(neighFirstVertex, RhinoAbsoluteTolerance, true))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Collision Check in the assemblage for a given <see cref="Assemblage"/> and <see cref="AssemblyObject"/> - Parallel version
        /// </summary>
        /// <param name="AOa">The <see cref="Assemblage"/> to check</param>
        /// <param name="sO">The sender <see cref="AssemblyObject"/></param>
        /// <returns></returns>
        public static bool CollisionCheckAssemblageParallel(Assemblage AOa, AssemblyObject sO)
        {

            // get first vertex as Point3d for inclusion check
            Point3d sOfirstVertex = sO.offsetMesh.Vertices[0];

            // find neighbours in Assemblage 
            List<int> neighList = new List<int>();
            // collision radius is a field of AssemblyObjects
            AOa.centroidsTree.Search(new Sphere(sO.referencePlane.Origin, sO.collisionRadius), (object sender, RTreeEventArgs e) =>
            {
                // recover the AssemblyObject index related to the found centroid
                neighList.Add(AOa.centroidsAO[e.Id]);
            });

            // check for no neighbours
            if (neighList.Count == 0)
                return false;
            bool intersectionFound = false;
            // check for collisions + inclusion (sender in receiver, receiver in sender)
            // see https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-write-a-parallel-foreach-loop-with-partition-local-variables
            // works but it's PAINFULLY slow
            Parallel.ForEach<int, bool>(neighList, ()=>false, (index,loop,result) =>
            //foreach (int index in neighList)
            {
                if (result) return true;
                if (Intersection.MeshMeshFast(sO.offsetMesh, AOa.assemblyObjects[index].collisionMesh).Length > 0)
                    return true;
                // check if sender object is inside neighbour
                if (AOa.assemblyObjects[index].collisionMesh.IsPointInside(sOfirstVertex, RhinoAbsoluteTolerance, true))
                    return true;
                // check if neighbour is inside sender object
                // get neighbour's OffsetMesh first vertex & check if it's inside
                Point3d neighFirstVertex = AOa.assemblyObjects[index].offsetMesh.Vertices[0];
                if (sO.collisionMesh.IsPointInside(neighFirstVertex, RhinoAbsoluteTolerance, true))
                    return true;
                return false;
            },
            (finalresult) => { intersectionFound = intersectionFound || finalresult; });

            return intersectionFound;
        }

        /// <summary>
        /// Collision Check in the <see cref="Assemblage"/> for a given <see cref="AssemblyObject"/>
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="neighList"></param>
        /// <returns></returns>
        public static bool CollisionCheckNeighbours(AssemblyObject AO, List<AssemblyObject> neighList)
        {

            // get first vertex as Point3d for inclusion check
            Point3d neighFirstVertex, AOfirstVertex = AO.offsetMesh.Vertices[0];

            // check for collisions + distance between centroids under threshold
            foreach (AssemblyObject neighbour in neighList)
            {
                if (Intersection.MeshMeshFast(AO.offsetMesh, neighbour.collisionMesh).Length > 0)
                    return true;
                if (neighbour.collisionMesh.IsPointInside(AOfirstVertex, RhinoAbsoluteTolerance, true))
                    return true;

                neighFirstVertex = neighbour.offsetMesh.Vertices[0];

                if (AO.collisionMesh.IsPointInside(neighFirstVertex, RhinoAbsoluteTolerance, true))
                    return true;

                //if (AO.referencePlane.Origin.DistanceToSquared(other.referencePlane.Origin) < tolSquared)// Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) // formerly 0.01
                //    return true;
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
            // mesh colliison test
            if (Intersection.MeshMeshFast(receiver.collisionMesh, sender.offsetMesh).Length > 0)
                return true;

            // mesh inclusion test
            // get first vertex as Point3d for inclusion check
            Point3d sOfirstVertex = sender.offsetMesh.Vertices[0];
            Point3d rOfirstVertex = receiver.offsetMesh.Vertices[0];

            if (sender.collisionMesh.IsPointInside(rOfirstVertex, RhinoAbsoluteTolerance, true))
                return true;
            if (receiver.collisionMesh.IsPointInside(sOfirstVertex, RhinoAbsoluteTolerance, true))
                return true;

            return false;
        }

        /// <summary>
        /// Check obstruction status for an <see cref="AssemblyObject"/> in the <see cref="Assemblage"/>
        /// </summary>
        /// <param name="AOa"></param>
        /// <param name="AOindex"></param>
        /// <returns></returns>
        public static bool ObstructionCheckAssemblage(Assemblage AOa, int AOindex)
        {
            AssemblyObject AO = AOa.assemblyObjects[AOindex];
            bool obstruct = false;
            Line ray;
            int[] faceIDs;

            // find neighbours in Assemblage
            List<int> neighList = new List<int>();
            // collision radius is a field of AssemblyObjects
            AOa.centroidsTree.Search(new Sphere(AO.referencePlane.Origin, AO.collisionRadius), (object sender, RTreeEventArgs e) =>
            {
                // check and recover the AssemblyObject index related to the found centroid
                if (AOa.centroidsAO[e.Id] != AOindex) neighList.Add(AOa.centroidsAO[e.Id]);
            });

            // if there are no neighbours return
            if (neighList.Count == 0)
                return obstruct;

            // check two-way: 
            // 1. object handles connected or obstructed by neighbours
            // 2. neighbour handles obstructed by object

            // scan neighbours
            foreach (int index in neighList)
            {
                // scan neighbour's handles
                for (int j = 0; j < AOa.assemblyObjects[index].handles.Length; j++)
                {
                    // if the handle is not available continue
                    if (AOa.assemblyObjects[index].handles[j].occupancy != 0) continue;

                    // check for accidental handle connection
                    bool connect = false;
                    // scan sO handles
                    for (int k = 0; k < AO.handles.Length; k++)
                    {
                        // if sO handle is not available continue
                        if (AO.handles[k].occupancy != 0) continue;
                        // ANY Handle (type independent) who is accidentally in contact is considered connected by default
                        // maybe set an option for strict type or rule based check if necessary
                        // for rule based chacks, newly placed AO is treated as sender, neighbour is treated as receiver
                        // if (RuleExist(AOa, AO.type, AOa.assemblyObjects[index].type, AO.handles[k].type, AOa.assemblyObjects[index].handles[j].type))
                        // if handles are of the same type...
                        //if (AO.handles[k].type == AOa.assemblyObjects[index].handles[j].type)
                        // ...and their distance is below absolute tolerance...
                        if (AOa.assemblyObjects[index].handles[j].sender.Origin.DistanceToSquared(AO.handles[k].sender.Origin) < RhinoAbsoluteToleranceSquared)
                        {
                            // ...update handles
                            double sOHWeight = AO.handles[k].weight;
                            AO.UpdateHandle(k, 1, index, j, AOa.assemblyObjects[index].handles[j].weight);
                            AOa.assemblyObjects[index].UpdateHandle(j, 1, AOindex, k, sOHWeight);
                            connect = true;
                            break;
                        }
                    }
                    // if a connection happened, go to next handle in neighbour
                    if (connect) continue;

                    // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                    // shoot a line from the handle
                    ray = new Line(AOa.assemblyObjects[index].handles[j].sender.Origin - (AOa.assemblyObjects[index].handles[j].sender.ZAxis * Utilities.RhinoAbsoluteTolerance * 5), AOa.assemblyObjects[index].handles[j].sender.ZAxis * 1.5);

                    // if it intercepts the last added object
                    if (Intersection.MeshLine(AO.collisionMesh, ray, out faceIDs).Length != 0)
                    {
                        // change handle occupancy to -1 (occluded) and add Object index to occluded handle neighbourObject
                        AOa.assemblyObjects[index].handles[j].occupancy = -1;
                        AOa.assemblyObjects[index].handles[j].neighbourObject = AOindex;
                        // update Object OccludedNeighbours status
                        AOa.assemblyObjects[AOindex].occludedNeighbours.Add(new int[] { index, j });
                        // change obstruct variable status
                        obstruct = true;
                    }

                }

                // CHECK OBSTRUCTION OF sO HANDLES BY NEIGHBOUR
                for (int k = 0; k < AO.handles.Length; k++)
                {
                    // if sO handle is not available continue
                    if (AO.handles[k].occupancy != 0) continue;

                    // shoot a line from the handle
                    ray = new Line(AO.handles[k].sender.Origin - (AO.handles[k].sender.ZAxis * Utilities.RhinoAbsoluteTolerance * 5), AO.handles[k].sender.ZAxis * 1.5);

                    // if it intercepts the neighbour object
                    if (Intersection.MeshLine(AOa.assemblyObjects[index].collisionMesh, ray, out faceIDs).Length != 0)
                    {
                        // change handle occupancy to -1 (occluded) and add neighbour Object index to occluded handle neighbourObject
                        AO.handles[k].occupancy = -1;
                        AO.handles[k].neighbourObject = index;
                        // update neighbourObject OccludedNeighbours status
                        AOa.assemblyObjects[index].occludedNeighbours.Add(new int[] { AOindex, k });
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
        public static bool ObstructionCheckList(List<AssemblyObject> AOList)
        {
            //AssemblyObject AO = AOa.assemblage[AOindex];
            bool obstruct = false;
            Line ray;
            int[] faceIDs;


            // check two-way: 
            // 1. object handles connected or obstructed by neighbours
            // 2. neighbour handles obstructed by object

            for (int i = 0; i < AOList.Count; i++)
            // scan neighbours
            //foreach (int index in neighList)
            {
                for (int j = i + 1; j < AOList.Count; j++)
                {
                    // scan neighbour's handles
                    for (int k = 0; k < AOList[j].handles.Length; k++)
                    {
                        // if the handle is not available continue
                        if (AOList[j].handles[k].occupancy != 0) continue;

                        // check for accidental handle connection
                        bool connect = false;
                        // scan sO handles
                        for (int p = 0; p < AOList[i].handles.Length; p++)
                        {
                            // if sO handle is not available continue
                            if (AOList[i].handles[p].occupancy != 0) continue;
                            // if handles are of the same type...
                            if (AOList[i].handles[p].type == AOList[j].handles[k].type)
                                // ...and their distance is below absolute tolerance...
                                if (AOList[j].handles[k].sender.Origin.DistanceToSquared(AOList[i].handles[p].sender.Origin) < RhinoAbsoluteToleranceSquared)// Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                                {
                                    // ...update handles
                                    double sOHWeight = AOList[i].handles[p].weight;
                                    AOList[i].UpdateHandle(p, 1, j, k, AOList[j].handles[k].weight);
                                    AOList[j].UpdateHandle(k, 1, i, p, sOHWeight);
                                    connect = true;
                                    break;
                                }
                        }
                        // if a connection happened, go to next handle in neighbour
                        if (connect) continue;

                        // CHECK OBSTRUCTION OF NEIGHBOUR HANDLES BY sO
                        // shoot a line from the handle
                        ray = new Line(AOList[j].handles[k].sender.Origin - (AOList[j].handles[k].sender.ZAxis * 0.1), AOList[j].handles[k].sender.ZAxis * 1.5);

                        // if it intercepts the last added object
                        if (Intersection.MeshLine(AOList[i].collisionMesh, ray, out faceIDs).Length != 0)
                        {
                            // change handle occupancy to -1 (occluded) and add Object index to occluded handle neighbourObject
                            AOList[j].handles[k].occupancy = -1;
                            AOList[j].handles[k].neighbourObject = i;
                            // update Object OccludedNeighbours status
                            AOList[i].occludedNeighbours.Add(new int[] { j, k });
                            // change obstruct variable status
                            obstruct = true;
                        }

                    }

                    // CHECK OBSTRUCTION OF sO HANDLES BY NEIGHBOUR
                    for (int k = 0; k < AOList[i].handles.Length; k++)
                    {
                        // if sO handle is not available continue
                        if (AOList[i].handles[k].occupancy != 0) continue;

                        // shoot a line from the handle
                        ray = new Line(AOList[i].handles[k].sender.Origin - (AOList[i].handles[k].sender.ZAxis * 0.1), AOList[i].handles[k].sender.ZAxis * 1.5);

                        // if it intercepts the neighbour object
                        if (Intersection.MeshLine(AOList[j].collisionMesh, ray, out faceIDs).Length != 0)
                        {
                            // change handle occupancy to -1 (occluded) and add neighbour Object index to occluded handle neighbourObject
                            AOList[i].handles[k].occupancy = -1;
                            AOList[i].handles[k].neighbourObject = j;
                            // update neighbourObject OccludedNeighbours status
                            AOList[j].occludedNeighbours.Add(new int[] { i, k });
                            // change obstruct variable status
                            obstruct = true;
                        }
                    }
                }
            }
            return obstruct;
        }

        public static bool AbsoluteZCheck(AssemblyObject AO)
        {
            return AO.referencePlane.ZAxis * Vector3d.ZAxis == 1;
        }

        private static bool RuleExist(Assemblage AOa, int sO, int rO, int sH, int rH)
        {
            if (AOa.heuristicsTree.PathExists(AOa.currentHeuristics, AOa.assemblyObjects[rO].type))
            // search for rInd rule
            {
                // scan rO rules
                foreach (Rule r in AOa.heuristicsTree.Branch(AOa.currentHeuristics, AOa.assemblyObjects[rO].type))
                {
                    if (r.sT != AOa.assemblyObjects[sO].type) continue;
                    else if (r.sH == sH && r.rH == rH) return true;
                }
            }
            else if (AOa.heuristicsTree.PathExists(AOa.currentHeuristics, AOa.assemblyObjects[sO].type))
            // search for sInd rule
            {
                // scan sO rules
                foreach (Rule r in AOa.heuristicsTree.Branch(AOa.currentHeuristics, AOa.assemblyObjects[sO].type))
                {
                    if (r.sT != AOa.assemblyObjects[rO].type) continue;
                    else if (r.sH == rH && r.rH == sH) return true;
                }

            }
            return false; // there is no corresponding rule
        }

        #endregion

        #region Object Utilities

        /// <summary>
        /// Builds the dictionary of AssemblyObjects
        /// </summary>
        /// <param name="AOset">the array of unique AssemblyObjects constituting the set</param>
        /// <returns></returns>
        public static Dictionary<string, int> BuildDictionary(AssemblyObject[] AOset, bool forceOrder)
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();

            if (forceOrder)
                for (int i = 0; i < AOset.Length; i++)
                    AOset[i].type = i;

            foreach (AssemblyObject ao in AOset)
                dict.Add(ao.name, ao.type);

            return dict;
        }

        /// <summary>
        /// Builds the Handles HashSet - for compatibility checks of loaded assemblages
        /// </summary>
        /// <param name="AOset">the array of unique AssemblyObjects constituting the set</param>
        /// <returns></returns>
        public static HashSet<int> BuildHandlesHashSet(AssemblyObject[] AOset)
        {
            HashSet<int> hh = new HashSet<int>();
            foreach (AssemblyObject ao in AOset)
                foreach (Handle h in ao.handles)
                    hh.Add(h.type);

            return hh;
        }

        /// <summary>
        /// Clones an AssemblyObject as an asset, without the connectivity information
        /// </summary>
        /// <param name="AO"></param>
        /// <returns>a cloned AssemblyObejct</returns>
        public static AssemblyObject Clone(AssemblyObject AO)
        {
            // build deep copies of meshes
            Mesh collisionMesh, offsetMesh;
            collisionMesh = new Mesh();
            offsetMesh = new Mesh();

            collisionMesh.CopyFrom(AO.collisionMesh);
            offsetMesh.CopyFrom(AO.offsetMesh);

            // reset occluded neighbours
            List<int[]> occludedNeighbours = new List<int[]>();

            // clone Handles resetting connectivity
            Handle[] handles = AO.handles.Select(h => Clone(ref h)).ToArray();

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
            List<Support> supports = new List<Support>();

            if (AO.supports != null)
                supports = AO.supports.Select(s => new Support(s)).ToList();

            // clone AssemblyObject
            AssemblyObject AOclone = new AssemblyObject(collisionMesh, offsetMesh, handles, AO.referencePlane, AO.direction, AO.AInd, occludedNeighbours, AO.collisionRadius,
                AO.name, AO.type, AO.weight, AO.iWeight, supports, AO.minSupports, AO.supported, AO.absoluteZLock, children, handleMap);

            return AOclone;
        }

        /// <summary>
        /// Duplicates an AssemblyObject preserving connectivity information. Useful for previous assemblages and the Goo wrapper.
        /// </summary>
        /// <param name="AO">The Original AssemblyObject</param>
        /// <returns>A duplicated AssemblyObject with the same connectivity of the source</returns>
        public static AssemblyObject CloneWithConnectivity(AssemblyObject AO)
        {
            AssemblyObject AOcloneConnect = Clone(AO); // new AssemblyObject(AO);

            for (int i = 0; i < AOcloneConnect.handles.Length; i++)
                AOcloneConnect.handles[i] = CloneWithConnectivity(ref AO.handles[i]);//AO.handles[i].DuplicateWithConnectivity();

            AOcloneConnect.occludedNeighbours = AO.occludedNeighbours;

            return AOcloneConnect;
        }

        /// <summary>
        /// Set a new CollisionMesh for the AssemblyObject
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="newCollisionMesh"></param>
        public static void SetCollisionMesh(AssemblyObject AO, Mesh newCollisionMesh)
        {
            AO.collisionMesh = new Mesh();
            AO.collisionMesh.CopyFrom(newCollisionMesh);
            double offsetTol = RhinoAbsoluteTolerance * 2.5;
            AO.offsetMesh = MeshOffsetWeightedAngle(AO.collisionMesh, offsetTol); // do NOT use the standard Mesh Offset method
            AO.collisionRadius = AO.collisionMesh.GetBoundingBox(false).Diagonal.Length * 2.5;
        }



        #endregion

        #region Supports utilities

        /// <summary>
        /// Add Supports to the AssemblyObject - returns true if successful
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="lines"></param>
        /// <param name="minSupports"></param>
        /// <returns></returns>
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
        /// Check if the object is supported by a list of neighbouring AssemblyObjects
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="neighbours"></param>
        /// <returns></returns>
        public static bool CheckSupport(AssemblyObject AO, List<AssemblyObject> neighbours)
        {
            if (AO.supported) return true;

            //AO.supported = false;

            int sCount = 0;
            // connected supports (as tentative)
            List<int> cSupports = new List<int>();

            for (int i = 0; i < AO.supports.Count; i++)
                if (AO.supports[i].connected || SupportIntersect(AO.supports[i],neighbours))
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
        /// Check if the object is supported by a list of neighbouring Meshes
        /// </summary>
        /// <param name="AO"></param>
        /// <param name="neighMeshes"></param>
        /// <returns></returns>
        public static bool CheckSupport(AssemblyObject AO, List<Mesh> neighMeshes)
        {
            if (AO.supported) return true;

            //AO.supported = false;
            int sCount = 0;
            // connected supports (as tentative)
            List<int> cSupports = new List<int>();

            for (int i = 0; i < AO.supports.Count; i++)
                if (AO.supports[i].connected || SupportIntersect(AO.supports[i], neighMeshes))
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
        /// Check intersection of a Support with a list of AssemblyObjects
        /// </summary>
        /// <param name="s"></param>
        /// <param name="neighbours"></param>
        /// <returns></returns>
        public static bool SupportIntersect(Support s, List<AssemblyObject> neighbours)
        {
            int[] faceIds;
            Point3d[] intPts;
            Vector3d dir = s.line.Direction;
            dir.Unitize();
            double minD;
            foreach (AssemblyObject AO in neighbours)
            {
                intPts = Intersection.MeshLine(AO.collisionMesh, s.line, out faceIds);
                // if intersections are found resize support line to intersection point and return true
                if (intPts.Length > 0)
                {
                    minD = double.MaxValue;
                    for (int i = 0; i < intPts.Length; i++)
                        minD = Math.Min(minD, s.line.From.DistanceToSquared(intPts[i]));
                    dir *= minD;
                    s.line = new Line(s.line.From, s.line.From + dir);
                    s.neighbourObject = AO.AInd;
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
        public static bool SupportIntersect(Support s, List<MeshEnvironment> envMeshes)
        {
            int[] faceIds;
            Point3d[] intPts;
            Vector3d dir = s.line.Direction;
            dir.Unitize();
            double minD;
            foreach (MeshEnvironment mE in envMeshes)
            {
                intPts = Intersection.MeshLine(mE.mesh, s.line, out faceIds);
                // if intersections are found resize support line to intersection point and return true
                if (intPts.Length > 0)
                {
                    minD = double.MaxValue;
                    for (int i = 0; i < intPts.Length; i++)
                        minD = Math.Min(minD, s.line.From.DistanceToSquared(intPts[i]));
                    dir *= minD;
                    s.line = new Line(s.line.From, s.line.From + dir);
                    s.neighbourObject = -2;
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
        public static bool SupportIntersect(Support s, List<Mesh> meshes)
        {
            int[] faceIds;
            Point3d[] intPts;
            Vector3d dir = s.line.Direction;
            dir.Unitize();
            double minD;
            foreach (Mesh m in meshes)
            {
                intPts = Intersection.MeshLine(m, s.line, out faceIds);
                // if intersections are found resize support line to intersection point and return true
                if (intPts.Length > 0)
                {
                    minD = double.MaxValue;
                    for (int i = 0; i < intPts.Length; i++)
                        minD = Math.Min(minD, s.line.From.DistanceToSquared(intPts[i]));
                    dir *= minD;
                    s.line = new Line(s.line.From, s.line.From + dir);
                    s.neighbourObject = -2;
                    return true;
                }
            }

            return false;
        }



        #endregion

        #region Handle utilities

        /// <summary>
        /// Clones a Handle
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>a cloned Handle</returns>
        public static Handle Clone(ref Handle handle)
        {
            Handle clone = new Handle();
            clone.sender = handle.sender;
            clone.rRotations = handle.rRotations;
            clone.rDictionary = CloneDictionaryWithValues(handle.rDictionary);
            // this is a shallow copy - not working
            //r = other.r;
            // deep array copy (from https://stackoverflow.com/questions/3464635/deep-copy-with-array)
            clone.receivers = handle.receivers.Select(pl => pl.Clone()).ToArray();
            clone.type = handle.type;
            clone.weight = handle.weight;
            clone.occupancy = 0;
            clone.neighbourHandle = -1;
            clone.neighbourObject = -1;

            return clone;
        }

        /// <summary>
        /// Duplicates a Handle preserving connectivity information
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>a duplicated Handle with the same connectivity</returns>
        public static Handle CloneWithConnectivity(ref Handle handle)
        {
            Handle handleCloneConnect = new Handle();
            handleCloneConnect.sender = handle.sender;
            handleCloneConnect.rRotations = handle.rRotations;
            handleCloneConnect.rDictionary = CloneDictionaryWithValues(handle.rDictionary);
            // this is a shallow copy - not working
            //r = other.r;
            // deep array copy (from https://stackoverflow.com/questions/3464635/deep-copy-with-array)
            handleCloneConnect.receivers = handle.receivers.Select(pl => pl.Clone()).ToArray();
            handleCloneConnect.type = handle.type;
            handleCloneConnect.weight = handle.weight;
            handleCloneConnect.occupancy = handle.occupancy;
            handleCloneConnect.neighbourHandle = handle.neighbourHandle;
            handleCloneConnect.neighbourObject = handle.neighbourObject;

            return handleCloneConnect;
        }

        #endregion

        #region Rule Utilities
        /// <summary>
        /// Returns a list of Rules from a heuristics string, outputs also a Data Tree of the rules strings
        /// </summary>
        /// <param name="AOset"></param>
        /// <param name="AOCatalog"></param>
        /// <param name="heuristics"></param>
        /// <param name="heuristicsTree"></param>
        /// <returns></returns>
        public static List<Rule> HeuristicsRulesFromString(List<AssemblyObject> AOset, Dictionary<string, int> AOCatalog, List<string> heuristics, out DataTree<string> heuristicsTree)
        {
            List<Rule> heuList = HeuristicsRulesFromString(AOset, AOCatalog, heuristics);
            heuristicsTree = new DataTree<string>();

            for (int i = 0; i < heuristics.Count; i++)
                heuristicsTree.Add(heuristics[i], new GH_Path(i));

            //List<Rule> heuList = new List<Rule>();
            //heuristicsTree = new DataTree<string>();

            //string[] ruleStrings = heuristics.ToArray();

            //int rT, rH, rR, sT, sH;
            //double rRA;
            //int iWeight;
            //for (int i = 0; i < ruleStrings.Length; i++)
            //{
            //    string[] ruleString = ruleStrings[i].Split(new[] { '<', '%' });
            //    string[] rec = ruleString[0].Split(new[] { '|' });
            //    string[] sen = ruleString[1].Split(new[] { '|' });
            //    // sender and receiver component types
            //    sT = AOCatalog[sen[0]];
            //    rT = AOCatalog[rec[0]];
            //    // sender handle index
            //    sH = Convert.ToInt32(sen[1]);
            //    // iWeight
            //    iWeight = Convert.ToInt32(ruleString[2]);
            //    string[] rRot = rec[1].Split(new[] { '=' });
            //    // receiver handle index and rotation
            //    rH = Convert.ToInt32(rRot[0]);
            //    rRA = Convert.ToDouble(rRot[1]);
            //    rR = AOset[rT].handles[rH].rDictionary[rRA]; // using rotations

            //    heuList.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, iWeight));
            //    heuristicsTree.Add(ruleStrings[i], new GH_Path(i));
            //}
            return heuList;
        }

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
                rR = AOset[rT].handles[rH].rDictionary[rRA]; // using rotations

                heuT.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, iWeight));
            }
            return heuT;
        }

        #endregion

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
        /// <returns>File name (with full path) of the saved assemblage</returns>
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
        public static void AssemblageToJSONSmart(List<AssemblyObject> assemblage, string path, string name)
        {
            // checks on directory path and filename

            // add sequential placeholder to filename
            name += "_{0}.JSON";

            // sanity checks
            // if there is no directory, create one
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);

            string fileName = ProcessFileName(path, name);

            // save geometries as assets from dictionary (collision and offsetmeshes)

            // save other data from assemblage (objects, connectivity, other)

        }

        private static string ProcessFileName(string path, string name)
        {
            // Assume index=0 for the first filename.
            string fileName = path + string.Format(name, 0.ToString("D3"));

            // Try to increment the index until we find a name which doesn't exist yet.
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
        /// Reads a file as a unique string
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string ReadFileUnique(string path)
        {
            return System.IO.File.ReadAllText(path);
        }

        /// <summary>
        /// Reads a file by line
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string[] ReadFileByLines(string path)
        {
            return System.IO.File.ReadAllLines(path);
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

        public static void AppendToFile(string directory, string fileName, string data)
        {
            string target = directory + fileName;
            var writer = System.IO.File.AppendText(target);
            writer.WriteLine(data);
            writer.Close();
        }

        /// <summary>
        /// Gets the GH File Path when called from a component
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        public static string GetGHFilePath(GH_Component caller)
        {
            GH_Document ghDoc = null;// = Grasshopper.Instances.ActiveCanvas.Document;
            ghDoc = caller.OnPingDocument();


            if (ghDoc == null || !ghDoc.IsFilePathDefined) return string.Empty;

            int nameLen = ghDoc.DisplayName.TrimEnd('*').Length + 3; // +3 accounts for the '.gh' extension
            int fileLen = ghDoc.FilePath.TrimEnd('*').Length;

            //string F = ghDoc.DisplayName.TrimEnd('*'); // filename (in case it might be needed)
            string GHFilePath = ghDoc.FilePath.Substring(0, fileLen - nameLen);

            return GHFilePath;
        }

        /// <summary>
        /// Serializes an assemblage into a string array for subsequent file saving
        /// </summary>
        /// <param name="assemblage"></param>
        /// <returns></returns>
        public static string[] SerializeAssemblage(List<AssemblyObject> assemblage)
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
        public static List<AssemblyObject> DeserializeAssemblage(string[] AOjson)
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

        #endregion

        #region Mesh Utilities

        /// <summary>
        /// Checks if point P is inside a Mesh by checking angle of projection vector with face normal
        /// </summary>
        /// <param name="testPoint">the point to check</param>
        /// <param name="searchDist">maximum distance for inclusion check</param>
        /// <returns>true if point is inside the mesh</returns>
        /// <remarks>This returns some false positives - use Mesh.IsPointInside() native function instead</remarks>
        public static bool IsPointInMesh(Mesh mesh, Point3d testPoint, double searchDist)
        {
            MeshPoint mP = mesh.ClosestMeshPoint(testPoint, searchDist);
            if (mP == null) return false;
            return Vector3d.VectorAngle(mesh.FaceNormals[mP.FaceIndex], mP.Point - testPoint) < (Math.PI * 0.5);
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
        public static Mesh MeshOffsetWeightedAngle(Mesh mesh, double offsetDistance)
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
        /// Mesh weighted normals implemented from the tips at the folowing pages:
        /// https://stackoverflow.com/questions/25100120/how-does-blender-calculate-vertex-normals
        /// http://www.bytehazard.com/articles/vertnorm.html
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns>Weighted Normals as a Vector3d array</returns>
        public static Vector3d[] ComputeWeightedNormals(Mesh mesh)
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

        static double MeshFaceArea(Mesh m, int i)
        {
            double area = 0;
            MeshFace f = m.Faces[i];

            if (f.IsTriangle)
                area = TriangleArea(m.Vertices[f.A], m.Vertices[f.B], m.Vertices[f.C]);
            else
                area = TriangleArea(m.Vertices[f.A], m.Vertices[f.B], m.Vertices[f.C]) + TriangleArea(m.Vertices[f.D], m.Vertices[f.A], m.Vertices[f.C]);

            return area;
        }

        static double TriangleArea(Point3d A, Point3d B, Point3d C)
        {
            return Math.Abs(A.X * (B.Y - C.Y) + B.X * (C.Y - A.Y) + C.X * (A.Y - B.Y)) * 0.5;
        }

        public static GH_Line[] GetSihouette(Mesh M)
        {
            ConcurrentBag<GH_Line> lines = new ConcurrentBag<GH_Line>();
            double angleTolerance = Math.PI * 0.25; // angle tolerance  ignore edges whose faces meet at an angle larger than this

            M.Normals.ComputeNormals();
            Rhino.Geometry.Collections.MeshTopologyEdgeList topologyEdges = M.TopologyEdges;

            Parallel.For(0, topologyEdges.Count, i =>
            {
                int[] connectedFaces = topologyEdges.GetConnectedFaces(i);
                if (connectedFaces.Length < 2)
                    lines.Add(new GH_Line(topologyEdges.EdgeLine(i)));

                if (connectedFaces.Length == 2)
                {
                    Vector3f norm1 = M.FaceNormals[connectedFaces[0]];
                    Vector3f norm2 = M.FaceNormals[connectedFaces[1]];
                    double nAng = Vector3d.VectorAngle(new Vector3d((double)norm1.X, (double)norm1.Y, (double)norm1.Z),
                      new Vector3d((double)norm2.X, (double)norm2.Y, (double)norm2.Z));
                    if (nAng > angleTolerance)
                        lines.Add(new GH_Line(topologyEdges.EdgeLine(i)));

                }
            });

            return lines.ToArray();
        }

        #endregion

        #region Vector Utilities

        /// <summary>
        /// Average Unitized Vector from a Vector Array - unitizes vector result at each step
        /// Useful for Vertex normal calculations
        /// </summary>
        /// <param name="vectors"></param>
        /// <returns>The average normalized vector</returns>
        public static Vector3d AverageUnitized(Vector3d[] vectors)
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
        /// Average Unitized Vector from a Vector List - unitizes vector result at each step
        /// Useful for Vertex normal calculations
        /// </summary>
        /// <param name="vectors"></param>
        /// <returns>The average normalized</returns>
        public static Vector3d AverageUnitized(List<Vector3d> vectors)
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
        /// Removes duplicate vectors (within tolerance) from an array, returing only unique vectors
        /// </summary>
        /// <param name="vectors"></param>
        /// <param name="angleTolerance"></param>
        /// <returns>Array of unique vectors</returns>
        public static Vector3d[] GetUniqueVectors(Vector3d[] vectors, double angleTolerance)
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
        #endregion

        #region Color Utilities

        /// <summary>
        /// Linear interpolate between colors
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Color LerpColor(Color a, Color b, double t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;

            return Color.FromArgb((int)(a.R * (1 - t) + b.R * t), (int)(a.G * (1 - t) + b.G * t), (int)(a.B * (1 - t) + b.B * t));

        }

        #endregion

        #region Math Utilities

        /// <summary>
        /// Converts an angle in degrees to radians
        /// </summary>
        /// <param name="angle">The angle to convert (in degrees)</param>
        /// <returns></returns>
        public static double DegreesToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        /// <summary>
        /// Converts an angle in radians to degrees
        /// </summary>
        /// <param name="angle">The angle to convert (in radians)</param>
        /// <returns></returns>
        public static double RadiansToDegrees(double angle)
        {
            return (180 / Math.PI) * angle;
        }

        /// <summary>
        /// Normalizes an array of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static double[] NormalizeRange(double[] values)
        {
            double vMin = values.Min();
            double vMax = values.Max();

            if (vMin > 0 && vMax < 1) return values;

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
        public static List<double> NormalizeRange(List<double> values)
        {
            double vMin = values.Min();
            double vMax = values.Max();

            if (vMin > 0 && vMax < 1) return values;

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
        public static double[][] NormalizeRanges(double[][] values)
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

            for (int i = 0; i < values.Length; i++)
                for (int j = 0; j < values[i].Length; j++)
                    normVal[i][j] = (values[i][j] - vMin[j]) * den[j];

            return normVal;
        }

        //public static double[][] NormalizeRanges(double[][] values)
        //{

        //    double vMin = values[0][0];
        //    double vMax = vMin;
        //    double tMin, tMax;
        //    for (int i = 0; i < values.Length; i++)
        //    {
        //        tMin = values[i].Min();
        //        tMax = values[i].Max();
        //        if (tMin < vMin) vMin = tMin;
        //        if (tMax > vMax) vMax = tMax;
        //    }

        //    if (vMin > 0 && vMax < 1) return values;

        //    double den = 1 / (vMax - vMin);

        //    double[][] normVal = new double[values.Length][];

        //    for (int i = 0; i < values.Length; i++)
        //        for (int j = 0; j < values[i].Length; j++)
        //            normVal[i][j] = (values[i][j] - vMin) * den;

        //    return normVal;
        //}

        /// <summary>
        /// Normalizes a DataTree of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static DataTree<double> NormalizeRanges(DataTree<double> values)
        {

            double[][] valuesArray = ToJaggedArray(values);
            double[][] normValuesArray = NormalizeRanges(valuesArray);

            DataTree<double> normVal = ToDataTree(normValuesArray);

            return normVal;
        }

        //public static DataTree<double> NormalizeRanges(DataTree<double> values)
        //{

        //    double vMin, vMax;
        //    double[] allValues = values.AllData().ToArray();
        //    vMin = allValues.Min();
        //    vMax = allValues.Max();

        //    if (vMin > 0 && vMax < 1) return values;

        //    double den = 1 / (vMax - vMin);

        //    DataTree<double> normVal = new DataTree<double>();//[values.Length][];

        //    for (int i = 0; i < values.BranchCount; i++)
        //        for (int j = 0; j < values.Branches[i].Count; j++)
        //            normVal.Add((values.Branches[i][j] - vMin) * den, values.Path(i));

        //    return normVal;
        //}

        #endregion

        #region Data Utilities

        /// <summary>
        /// Converts a jagged array into a DataTree of the same type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jArray"></param>
        /// <returns></returns>
        public static DataTree<T> ToDataTree<T>(T[][] jArray)
        {
            DataTree<T> data = new DataTree<T>();

            for (int i = 0; i < jArray.Length; i++)
                data.AddRange(jArray[i].Select(d => d).ToList(), new GH_Path(i));

            return data;
        }

        /// <summary>
        /// Converts a list of arrays into a DataTree of the same type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arraysList"></param>
        /// <returns></returns>
        public static DataTree<T> ToDataTree<T>(List<T[]> arraysList)
        {
            DataTree<T> data = new DataTree<T>();

            for (int i = 0; i < arraysList.Count; i++)
                data.AddRange(arraysList[i].Select(d => d).ToList(), new GH_Path(i));

            return data;
        }

        /// <summary>
        /// Converts a DataTree into a jagged array of the same type\nThe array length is equal to the number of branches, regardless of paths
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tree"></param>
        /// <returns></returns>
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
        /// <typeparam name="T"></typeparam>
        /// <param name="tree"></param>
        /// <returns></returns>
        public static List<T[]> ToListOfArrays<T>(DataTree<T> tree)
        {

            List<T[]> arraysList = new List<T[]>();//[tree.BranchCount][];

            for (int i = 0; i < tree.BranchCount; i++)
                arraysList.Add(tree.Branches[i].ToArray());

            return arraysList;
        }

        /// <summary>
        /// Clones a Dictionary and its values - as seen here: https://stackoverflow.com/questions/139592/what-is-the-best-way-to-clone-deep-copy-a-net-generic-dictionarystring-t
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="original"></param>
        /// <returns></returns>
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
        /// Renames a key in a Dictionary - as seen here: https://stackoverflow.com/questions/6499334/best-way-to-change-dictionary-key
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="fromKey"></param>
        /// <param name="toKey"></param>
        /// <returns>true if successful</returns>
        public static bool RenameKey<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey fromKey, TKey toKey)
        {
            TValue value = dictionary[fromKey];
            if (!dictionary.Remove(fromKey))
                return false;
            dictionary[toKey] = value;
            return true;
        }

        #endregion

        #region Diagnostic Utilities

        public static long StartWatch(System.Diagnostics.Stopwatch stopWatch)
        {
            long elapsedTime = stopWatch.ElapsedMilliseconds;
            stopWatch.Restart();
            return elapsedTime;
        }

        #endregion

    }
}
