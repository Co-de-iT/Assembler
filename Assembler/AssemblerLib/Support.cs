using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;

namespace AssemblerLib
{
    /// <summary>
    /// Support class for embedded structural consistency
    /// </summary>
    public struct Support
    {
        /// <summary>
        /// Support line
        /// </summary>
        public Line line;

        /// <summary>
        /// initial length of the support
        /// </summary>
        public readonly double initLength;

        /// <summary>
        /// connected flag - true if support intersects a nearby geometry
        /// </summary>
        public bool connected;

        /// <summary>
        /// Index of neighbour AssemblyObject connected by the support; -1 if free, -2 if connected to other entities (i.e. an external object)
        /// </summary>
        public int neighbourObject;

        /// <summary>
        /// Construct a support from another Support (deep copy)
        /// </summary>
        /// <param name="other"></param>
        public Support(Support other)
        {
            line = other.line;
            initLength = other.initLength;
            connected = other.connected;
            neighbourObject = other.neighbourObject;
        }

        /// <summary>
        /// Construct a support from origin point, direction vector and length
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="length"></param>
        public Support(Point3d origin, Vector3d direction, double length)
        {
            line = new Line(origin, direction, length);
            initLength = length;
            connected = false;
            neighbourObject = -1;
        }

        /// <summary>
        /// Construct a support from a line
        /// </summary>
        /// <param name="line"></param>
        public Support(Line line)
        {
            this.line = line;
            initLength = line.Length;
            connected = false;
            neighbourObject = -1;
        }

        /// <summary>
        /// Resets support to initial length value and resets connectivity data
        /// </summary>
        public void Reset()
        {
            if (line.Length != initLength)
                line.To = line.From + (line.UnitTangent * initLength);
            connected = false;
            neighbourObject = -1;
        }

        /// <summary>
        /// Transform Support using a generic Transformation
        /// </summary>
        /// <param name="xForm"></param>
        public void Transform(Transform xForm)
        {
            line.Transform(xForm);
        }

        // MOVED TO UTILITIES

        ///// <summary>
        ///// Check intersection with a list of Meshes
        ///// </summary>
        ///// <param name="meshes"></param>
        ///// <returns></returns>
        //public bool Intersect(List<Mesh> meshes)
        //{
        //    int[] faceIds;
        //    Point3d[] intPts;
        //    Vector3d dir = line.UnitTangent;
        //    double minD;
        //    foreach (Mesh m in meshes)
        //    {
        //        intPts = Intersection.MeshLine(m, line, out faceIds);
        //        // if intersections are found resize support line to intersection point and return true
        //        if (intPts.Length > 0)
        //        {
        //            minD = double.MaxValue;
        //            for (int i = 0; i < intPts.Length; i++)
        //                minD = Math.Min(minD, line.From.DistanceToSquared(intPts[i]));
        //            dir *= minD;
        //            line = new Line(line.From, line.From + dir);
        //            return true;
        //        }
        //    }
        //    return false;
        //}

        ///// <summary>
        ///// Check intersection with a list of AssemblyObjects
        ///// </summary>
        ///// <param name="neighbours"></param>
        ///// <returns></returns>
        //public bool Intersect(List<AssemblyObject> neighbours)
        //{
        //    int[] faceIds;
        //    Point3d[] intPts;
        //    Vector3d dir = line.Direction;
        //    dir.Unitize();
        //    double minD;
        //    foreach (AssemblyObject AO in neighbours)
        //    {
        //        intPts = Intersection.MeshLine(AO.collisionMesh, line, out faceIds);
        //        // if intersections are found resize support line to intersection point and return true
        //        if (intPts.Length > 0)
        //        {
        //            minD = double.MaxValue;
        //            for (int i = 0; i < intPts.Length; i++)
        //                minD = Math.Min(minD, line.From.DistanceToSquared(intPts[i]));
        //            dir *= minD;
        //            line = new Line(line.From, line.From + dir);
        //            neighbourObject = AO.AInd;
        //            return true;
        //        }
        //    }
        //    return false;
        //}

        ///// <summary>
        ///// Check intersection with a list of EnvMeshes
        ///// </summary>
        ///// <param name="envMeshes"></param>
        ///// <returns></returns>
        //public bool Intersect(List<MeshEnvironment> envMeshes)
        //{
        //    int[] faceIds;
        //    Point3d[] intPts;
        //    Vector3d dir = line.Direction;
        //    dir.Unitize();
        //    double minD;
        //    foreach (MeshEnvironment mE in envMeshes)
        //    {
        //        intPts = Intersection.MeshLine(mE.mesh, line, out faceIds);
        //        // if intersections are found resize support line to intersection point and return true
        //        if (intPts.Length > 0)
        //        {
        //            minD = double.MaxValue;
        //            for (int i = 0; i < intPts.Length; i++)
        //                minD = Math.Min(minD, line.From.DistanceToSquared(intPts[i]));
        //            dir *= minD;
        //            line = new Line(line.From, line.From + dir);
        //            neighbourObject = -2;
        //            return true;
        //        }
        //    }
        //    return false;
        //}
    }
}
