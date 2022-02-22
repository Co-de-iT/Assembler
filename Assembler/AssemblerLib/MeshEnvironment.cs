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

        public enum Type : ushort { Void = 0, Solid = 1, Container = 2 } // cannot use negative values with ushort
        public Type type;

        //private Polyline[] intersections, overlaps;
        //private Mesh overlapsMesh;

        /// <summary>
        /// Constructs a <see cref="MeshEnvironment"/> from a Mesh and an explicit type
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="type"></param>
        public MeshEnvironment(Mesh mesh, int type)
        {
            this.mesh = mesh;
            this.mesh.RebuildNormals();
            switch (type)
            {
                case 0: // void
                    if (this.mesh.Volume() < 0)
                        this.mesh.Flip(true, true, true);
                    break;
                case 1: // solid
                    goto case 0;
                case 2: // container
                    if (this.mesh.Volume() > 0)
                        this.mesh.Flip(true, true, true);
                    break;
                default: // any other value is converted to solid
                    type = 1;
                    break;
            }

            this.type = (Type)type;
        }

        /// <summary>
        /// Constructs a <see cref="MeshEnvironment"/> from a Mesh - can create only solids or voids according to mesh volume
        /// </summary>
        /// <param name="mesh"></param>
        public MeshEnvironment(Mesh mesh) : this(mesh, mesh.Volume() > 0 ? 1 : 0)
        { }

        /// <summary>
        /// Constructs a <see cref="MeshEnvironment"/> from another (duplicate method)
        /// </summary>
        /// <param name="other"></param>
        public MeshEnvironment(MeshEnvironment other)
        {
            mesh = new Mesh();
            mesh.CopyFrom(other.mesh);
            type = other.type;
        }

        /// <summary>
        /// Check if point P is inside an obstacle or void, or outside a container
        /// </summary>
        /// <param name="P"></param>
        /// <returns>True if a point is invalid (inside an obstacle or void, or outside a container)</returns>
        public bool IsPointInvalid(Point3d P)
        {
            bool result = mesh.IsPointInside(P, 0, false);
            if (type == Type.Container) result = !result;

            return result;
        }


        /// <summary>
        /// Checks for collision with a given Mesh
        /// </summary>
        /// <param name="otherMesh"></param>
        /// <returns>true if collision happens</returns>
        public bool CollisionCheck(Mesh otherMesh)
        {
            return Rhino.Geometry.Intersect.Intersection.MeshMeshFast(otherMesh, mesh).Length > 0;

            // this might be a candidate upgrade, but it's slower than the above
            //return Rhino.Geometry.Intersect.MeshClash.Search(m, mesh, Utilities.RhinoAbsoluteTolerance, 0).Length > 0;

            // this is impossibly slow (like, 20x slower than the obsolete method)
            //return Rhino.Geometry.Intersect.Intersection.MeshMesh(new Mesh[] { m, mesh }, Utilities.RhinoAbsoluteTolerance, out intersections, false, out overlaps, false,
            //   out overlapsMesh, null, System.Threading.CancellationToken.None, null);
        }

    }

    ///// <summary>
    ///// Checks if point P is inside by checking angle of projection vector with face normal
    ///// </summary>
    ///// <param name="P">the point to check</param>
    ///// <param name="maxDist">maximum distance for inclusion check</param>
    ///// <returns>true if point is inside the mesh</returns>
    ///// <remarks>BUGGY: FALSE NEGATIVES AROUND MESH 90DEG CORNERS BECAUSE OF CLOSESTMESHPOINT WHICH RETURNS A FUNNY FACE INDEX</remarks>
    ///// <remarks>A proposed solution (by Oskar Stahlberg) is to check for edge cases (point on edge) and evaluate point position with respect to</remarks>
    ///// <remarks>bisecting plane of conflicting faces - common edge = X axis (midpoint as origin), normal cross product = Y axis</remarks>
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

}
