using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AssemblerLib.Utils
{
    public static class MeshUtils
    {
        // BUG: this does not work as intended - using Rhino Mesh class method instead
        /// <summary>
        /// Checks if a point P is inside a Mesh by checking the number of intersections of a line
        /// from a point outside to the test point
        /// even number of intersections is outside, odd is inside.
        /// see [this thread](https://twitter.com/OskSta/status/1491716992931356672) for this and more methods
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="testPoint"></param>
        /// <returns>True if point is inside, False otherwise</returns>
        /// <exclude>Exclude from documentation</exclude>
        public static bool IsPointInMesh(Mesh mesh, Point3d testPoint)
        {
            // this point must be OUTSIDE of the Mesh
            Point3d from = (Point3d)(mesh.Vertices[0] + mesh.Normals[0]);
            // even number of intersections: point is outside, otherwise point is inside
            return (Intersection.MeshLine(mesh, new Line(from, testPoint), out _).Length % 2 != 0);
        }

        /// <summary>
        /// Improves Mesh Offset using a bespoke method for Mesh normal calculation, based on face angle weighing
        /// see [here](https://stackoverflow.com/questions/25100120/how-does-blender-calculate-vertex-normals)
        /// and [here](http://www.bytehazard.com/articles/vertnorm.html)
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
        /// <param name="angleTolerance">The angle tolerance in radians (ignore edges whose faces meet at an angle larger than this) - default is Math.PI * 0.25 = 45°</param>
        /// <returns>Edges as an array of GH_Lines</returns>
        public static GH_Line[] GetSilhouette(Mesh mesh, double angleTolerance = Math.PI * 0.25)
        {
            ConcurrentBag<GH_Line> lines = new ConcurrentBag<GH_Line>();

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
    }
}
