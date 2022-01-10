using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.GUI.Base;


// <Custom using> 
using Rhino.Geometry.Intersect;
using SharpNav;
using SharpNav.Geometry;
// </Custom using> 

/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
    #region Utility functions
    /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
    /// <param name="text">String to print.</param>
    private void Print(string text) { /* Implementation hidden. */ }
    /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
    /// <param name="format">String format.</param>
    /// <param name="args">Formatting parameters.</param>
    private void Print(string format, params object[] args) { /* Implementation hidden. */ }
    /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj) { /* Implementation hidden. */ }
    /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
    #endregion

    #region Members
    /// <summary>Gets the current Rhino document.</summary>
    private readonly RhinoDoc RhinoDocument;
    /// <summary>Gets the Grasshopper document that owns this script.</summary>
    private readonly GH_Document GrasshopperDocument;
    /// <summary>Gets the Grasshopper script component that owns this script.</summary>
    private readonly IGH_Component Component;
    /// <summary>
    /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
    /// Any subsequent call within the same solution will increment the Iteration count.
    /// </summary>
    private readonly int Iteration;
    #endregion

    /// <summary>
    /// This procedure contains the user code. Input parameters are provided as regular arguments,
    /// Output parameters as ref arguments. You don't have to assign output parameters,
    /// they will have a default value.
    /// </summary>
    private void RunScript(List<Mesh> M, double a, ref object nM, ref object cnM, ref object tM)
    {
        // <Custom code> 
        triangulatedMeshes = new List<Mesh>();
        foreach (Mesh mesh in M)
        {
            Mesh triMesh = Triangulate(mesh);
            triangulatedMeshes.Add(triMesh);
        }

        tM = triangulatedMeshes;
        navMesh = GenerateNavMesh(triangulatedMeshes, a);
        nM = navMesh;
        cnM = CleanNavMesh(navMesh);
        // </Custom code> 
    }

    // <Custom additional code> 
    Mesh navMesh;
    List<Mesh> triangulatedMeshes;

    public Mesh Triangulate(Mesh x)
    {
        //var navigMesh = NavMesh.Generate()

        Mesh tMesh = new Mesh();
        int facecount = x.Faces.Count;
        for (int i = 0; i < facecount; i++)
        {
            var mf = x.Faces[i];
            if (mf.IsQuad)
            {
                double dist1 = x.Vertices[mf.A].DistanceTo(x.Vertices[mf.C]);
                double dist2 = x.Vertices[mf.B].DistanceTo(x.Vertices[mf.D]);
                if (dist1 > dist2)
                {
                    tMesh.Faces.AddFace(mf.A, mf.B, mf.D);
                    tMesh.Faces.AddFace(mf.B, mf.C, mf.D);
                }
                else
                {
                    tMesh.Faces.AddFace(mf.A, mf.B, mf.C);
                    tMesh.Faces.AddFace(mf.A, mf.C, mf.D);
                }
            }
            else
            {
                tMesh.Faces.AddFace(mf.A, mf.B, mf.C);
            }
        }

        tMesh.Vertices.AddVertices(x.Vertices);
        tMesh.RebuildNormals();
        tMesh.UnifyNormals();
        return tMesh;
    }

    public Mesh GenerateNavMesh(List<Mesh> triangMeshes, double angleTol)
    {
        Mesh navMesh = new Mesh();
        Mesh current;
        List<int> faceIndices;
        for (int i = 0; i < triangMeshes.Count; i++)
        {
            current = new Mesh();
            current.CopyFrom(triangMeshes[i]);

            faceIndices = new List<int>();
            for (int j = 0; j < current.Faces.Count; j++)
            {
                // if face normal is upwards within tolerance add face to navMesh
                if (Vector3d.VectorAngle(Vector3d.ZAxis, current.FaceNormals[j]) < angleTol)
                {
                    faceIndices.Add(j);
                }

            }

            navMesh.Append(current.Faces.ExtractFaces(faceIndices));

        }

        navMesh.Vertices.Align(0.1);
        navMesh.Weld(Math.PI);
        navMesh.RebuildNormals();
        navMesh.UnifyNormals();


        return navMesh;
    }

    public Mesh CleanNavMesh(Mesh navMeshDirty)
    {
        Mesh navMesh = new Mesh();
        navMesh.CopyFrom(navMeshDirty);

        // verify intersections
        List<int> removedFaces = new List<int>();
        for (int i = 0; i < navMesh.Faces.Count; i++)
        {
            Point3d faceCenter = navMesh.Faces.GetFaceCenter(i) + (Vector3d)navMesh.FaceNormals[i] * 0.0001;
            int[] faceIDs;
            Intersection.MeshLine(navMesh, new Line(faceCenter, navMesh.FaceNormals[i], 2), out faceIDs);

            if (faceIDs.Length > 0) removedFaces.Add(i);
        }

        navMesh.Faces.ExtractFaces(removedFaces);

        return navMesh;
    }

    // </Custom additional code> 
}