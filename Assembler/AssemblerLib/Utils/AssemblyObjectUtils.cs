using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Utils
{
    public static class AssemblyObjectUtils
    {

        /// <summary>
        /// Performs a check for World Z-Axis orientation of the AssemblyObject
        /// </summary>
        /// <param name="AO">the <see cref="AssemblyObject"/> to check</param>
        /// <returns>true if the Z axis of the object Reference Plane is oriented along the World Z</returns>
        /// <exclude>Exclude from documentation</exclude>
        public static bool AbsoluteZCheck(AssemblyObject AO) => AO.ReferencePlane.ZAxis * Vector3d.ZAxis == 1;

        /// <summary>
        /// Performs a check for World Z-Axis orientation of the AssemblyObject, with a tolerance
        /// </summary>
        /// <param name="AO">the <see cref="AssemblyObject"/> to check</param>
        /// <param name="tol">the tolerance to respect</param>
        /// <returns>true if the Z axis of the object Reference Plane is oriented along the World Z under the given tolerance</returns>
        public static bool AbsoluteZCheck(AssemblyObject AO, double tol) => 1 - (AO.ReferencePlane.ZAxis * Vector3d.ZAxis) <= tol;

        /// <summary>
        /// Resets an AssemblyObject to "factory values"
        /// </summary>
        /// <param name="AO">the <see cref="AssemblyObject"/> to reset</param>
        /// <returns>A reset AssemblyObject</returns>
        /// <param name="resetTopology"></param><param name="resetReceiverValue"></param><param name="resetSenderValue"></param>
        public static AssemblyObject Reset(AssemblyObject AO, bool resetTopology = true, bool resetReceiverValue = true, bool resetSenderValue = true)
        {
            AssemblyObject AOreset;

            // this fixes compatibility issues with saved assemblages prior to version 1.1.9
            // Rotations and RDictionary were null in those cases
            for (int i = 0; i < AO.Handles.Length; i++)
            {
                if (AO.Handles[i].Rotations == null)
                    AO.Handles[i].Rotations = new double[0];
                if (AO.Handles[i].RDictionary == null)
                    AO.Handles[i].RDictionary = new Dictionary<double, int>();
            }

            if (resetTopology) AOreset = Clone(AO);
            else AOreset = CloneWithConnectivityAndValues(AO);

            if (resetReceiverValue) AOreset.ReceiverValue = double.NaN;
            if (resetSenderValue) AOreset.SenderValue = double.NaN;

            return AOreset;
        }

        /// <summary>
        /// Clones an <see cref="AssemblyObject"/> as an asset, resetting connectivity information and Sender/Receiver values
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
            Handle[] handles = AO.Handles.Select(h => HandleUtils.Clone(ref h)).ToArray();

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
        /// <remarks>Useful for previous assemblages</remarks>
        public static AssemblyObject CloneWithConnectivity(AssemblyObject AO)
        {
            // make a fresh new clone
            AssemblyObject AOcloneConnect = Clone(AO);

            for (int i = 0; i < AOcloneConnect.Handles.Length; i++)
                AOcloneConnect.Handles[i] = HandleUtils.CloneWithConnectivity(ref AO.Handles[i]);

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
        /// Duplicates an <see cref="AssemblyObject"/> preserving connectivity and Sender/Receiver values
        /// </summary>
        /// <param name="AO">The Original <see cref="AssemblyObject"/></param>
        /// <returns>A duplicated AssemblyObject with the same connectivity and sender/receiver values of the source</returns>
        /// <remarks>Useful for previous assemblages and the Goo wrapper</remarks>
        public static AssemblyObject CloneWithConnectivityAndValues(AssemblyObject AO)
        {
            // make a fresh new clone
            AssemblyObject AOcloneConnectValues = CloneWithConnectivity(AO);

            AOcloneConnectValues.ReceiverValue = AO.ReceiverValue;
            AOcloneConnectValues.SenderValue = AO.SenderValue;

            return AOcloneConnectValues;
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
            double offsetTol = Constants.RhinoAbsoluteTolerance * 2.5;
            // do NOT use the standard Mesh Offset method
            AO.OffsetMesh = MeshUtils.MeshOffsetWeightedAngle(AO.CollisionMesh, offsetTol);
        }
    }
}
