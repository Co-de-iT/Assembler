using System;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace AssemblerLib
{

    /// <summary>
    /// this class can give more properties to the environment objects
    /// </summary>
    public class MeshEnvironment
    {
        /// <summary>
        /// Mesh
        /// </summary>
        public Mesh mesh;

        public enum Type : ushort { Solid = 0, Void = 1, Container = 2 }// or: Container 0, Solid 1, Void -1, or: Container -1, Void 0, Solid 1
        public Type type;

        /// <summary>
        /// Constructs a MeshEnvironment from a Mesh
        /// </summary>
        /// <param name="mesh"></param>
        public MeshEnvironment(Mesh mesh)
        {
            this.mesh = mesh;
            this.mesh.RebuildNormals();
            type = mesh.Volume() > 0 ? Type.Solid : Type.Container;
        }

        /// <summary>
        /// Constructs a MeshEnvironment from a Mesh and an explicit type
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="type"></param>
        public MeshEnvironment(Mesh mesh, int type) : this(mesh)
        {
            if (type > 0 && type < 3) this.type = (Type)type;
        }

        public MeshEnvironment(MeshEnvironment other)
        {
            mesh = new Mesh();
            mesh.CopyFrom(other.mesh);
            type = other.type;
        }

        /// <summary>
        /// Check if point P is inside an obstacle or outside a container
        /// </summary>
        /// <param name="P"></param>
        /// <returns>True if a point is invalid (inside an obstacle or outside a container)</returns>
        public bool IsPointInvalid(Point3d P)
        {
            bool result = mesh.IsPointInside(P, 0, false);
            if (type == Type.Container) result = !result;

            return result;
        }

        ///// <summary>
        ///// Checks if point P is inside by checking angle of projection vector with face normal
        ///// </summary>
        ///// <param name="P">the point to check</param>
        ///// <param name="maxDist">maximum distance for inclusion check</param>
        ///// <returns>true if point is inside the mesh</returns>
        ///// <remarks>BUGGY: FALSE NEGATIVES AROUND MESH 90DEG CORNERS BECAUSE OF CLOSESTMESHPOINT WHICH RETURNS A FUNNY FACE INDEX</remarks>
        //public bool IsPointInsideOLD(Point3d P, double maxDist)
        //{
        //    // increase maxDist until mP is not null anymore or maxDist is 1000 times the initial one
        //    double initDist = maxDist;
        //    double limitDist = initDist * 1000;
        //    MeshPoint mP;// = null;

        //    //while (mP == null || maxDist < limitDist)
        //    //{
        //    //    mP = mesh.ClosestMeshPoint(P, maxDist);
        //    //    maxDist *= 2;
        //    //}

        //    // the loop above can (should?) be rewritten as
        //    do
        //    {
        //        mP = mesh.ClosestMeshPoint(P, maxDist);
        //        maxDist *= 2;
        //    }
        //    while (mP == null && maxDist < limitDist); // && makes more sense than || (should keep loop if both are true, break otherwise)

        //    if (mP == null) return false;
        //    return Vector3d.VectorAngle(faceNormals[mP.FaceIndex], mP.Point - P) < (Math.PI * 0.5);
        //}

        //public bool IsPointInside(Point3d P, double maxDist)
        //{
        //    // to use this, enclosures must be flagged as such (with a true/false)
        //    // mesh.IsPointInside(P, Utilities.tol, false);
        //    MeshPoint mP = mesh.ClosestMeshPoint(P, maxDist);
        //    if (mP == null) return false;
        //    return Vector3d.VectorAngle(mesh.FaceNormals[mP.FaceIndex], mP.Point-P) < (Math.PI*0.5);
        //}

        /// <summary>
        /// Checks for collision with a given Mesh
        /// </summary>
        /// <param name="m"></param>
        /// <returns>true if collision happens</returns>
        public bool CollisionCheck(Mesh m)
        {
            return Rhino.Geometry.Intersect.Intersection.MeshMeshFast(m, mesh).Length > 0;
        }

    }



}
