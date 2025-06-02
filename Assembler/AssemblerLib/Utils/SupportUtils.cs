using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Utils
{
    /// <exclude>Exclude from documentation</exclude>
    public static class SupportUtils
    {

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
            Point3d[] intPts;
            //Vector3d dir = s.line.Direction;
            //dir.Unitize();
            //double minD;
            foreach (AssemblyObject AO in neighbours)
            {
                intPts = Intersection.MeshLine(AO.CollisionMesh, s.Line, out _);
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
            List<Mesh> meshes = envMeshes.Select(em => em.Mesh).ToList();
            return SupportIntersect(s, meshes);
        }

        /// <summary>
        /// Check intersection of a Support with a list of Meshes
        /// </summary>
        /// <param name="s"></param>
        /// <param name="meshes"></param>
        /// <returns></returns>
        internal static bool SupportIntersect(Support s, List<Mesh> meshes)
        {
            Point3d[] intPts;
            Vector3d dir = s.Line.Direction;
            dir.Unitize();
            double minD;
            foreach (Mesh m in meshes)
            {
                intPts = Intersection.MeshLine(m, s.Line, out _);
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
    }
}
