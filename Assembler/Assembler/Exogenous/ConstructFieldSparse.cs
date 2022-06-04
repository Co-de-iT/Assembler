using System;
using System.Collections.Generic;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    public class ConstructFieldSparse : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructFieldSparse class.
        /// </summary>
        public ConstructFieldSparse()
          : base("Construct Sparse Field", "AFieldSp",
              "Constructs an empty Field from a sparse list of points and optional topology information",
              "Assembler", "Exogenous")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points for Field generation", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Topology", "T", "Topology of neighbour indexes for each point in the list", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Topology Weights", "tW", "Weight of each neighbour connection, in the same topology order" +
                "\nleave empty to use connection length", GH_ParamAccess.tree);
            pManager[1].Optional = true; // Topology is optional
            pManager[2].Optional = true; // transmission coefficients are optional
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Empty Field", GH_ParamAccess.item);
            pManager.AddPointParameter("Field Points", "P", "Field Points", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            List<Point3d> points = new List<Point3d>();
            if (!DA.GetDataList(0, points)) return;
            if (points.Count == 0) return;

            GH_Structure<GH_Integer> GH_topology = new GH_Structure<GH_Integer>();
            DA.GetDataTree(1, out GH_topology);
            DataTree<int> topology = null;

            GH_Structure<GH_Number> GH_transCoeff = new GH_Structure<GH_Number>();
            DA.GetDataTree(2, out GH_transCoeff);
            DataTree<double> transCoeff = null;

            if (!(GH_topology == null || GH_topology.IsEmpty || GH_topology.DataCount==0))
            {
                if (GH_topology.Branches.Count != points.Count)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Topology tree branches must match number of points");
                topology = Utilities.GHS2TreeIntegers(GH_topology);
                if (GH_transCoeff == null || GH_transCoeff.IsEmpty)
                    transCoeff = ComputeTransCoeff(points, topology);
                else transCoeff = Utilities.GHS2TreeDoubles(GH_transCoeff);
            }

            Field f = new Field(points, topology, transCoeff);

            DA.SetData("Field", f);
            DA.SetDataList("Field Points", f.GetGH_Points());
        }

        private DataTree<double> ComputeTransCoeff(List<Point3d> points, DataTree<int> topology)
        {
            DataTree<double> transCoeff = new DataTree<double>();
            double w, totalW;
            List<double> pointWeights;

            for (int i = 0; i < points.Count; i++)
            {
                pointWeights = new List<double>();
                totalW = 0;
                for (int j = 0; j < topology.Branches[i].Count; j++)
                {
                    w = 1 / points[i].DistanceTo(points[topology.Branches[i][j]]);
                    pointWeights.Add(w);
                    totalW += w;
                }
                for (int j = 0; j < pointWeights.Count; j++) pointWeights[j] /= totalW;
                transCoeff.AddRange(pointWeights, new GH_Path(i));
            }

            return transCoeff;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Resources.Field_Sparse;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8C268E65-CE88-4CBD-9E5B-92DD3A6044B2"); }
        }
    }
}