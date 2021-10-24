using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    public class ExtractTopology : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExtractTopology class.
        /// </summary>
        public ExtractTopology()
          : base("ExtractTopology", "AOTopo",
              "Extract connection data from an assemblage",
              "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Handle Occupancy", "hO", "Handle occupancy status\n0 available, 1 connected, -1 occluded", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Neighbour Object index", "nO", "Index of neighbour AssemblyObject\n-1 if Handle is available", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Neighbour Handle index", "nH", "Neighbour AssemblyObject Handle index\n-1 if Handle is available or occluded", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<AssemblyObject> AO = new List<AssemblyObject>();
            Assemblage AOa = null;
            // input data sanity check
            if (!DA.GetData("Assemblage", ref AOa)) return;

            if (AOa == null) return;

            // cast input to AssemblyObject type
            AO = AOa.assemblyObjects;

            GH_Structure<GH_Integer> hOTree = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Integer> nOTree = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Integer> nHTree = new GH_Structure<GH_Integer>();

            GH_Path p;
            for (int i = 0; i < AO.Count; i++)
            {
                p = new GH_Path(0, i);
                foreach (Handle h in AO[i].handles)
                {
                    hOTree.Append(new GH_Integer(h.occupancy), p);
                    nOTree.Append(new GH_Integer(h.neighbourObject), p);
                    nHTree.Append(new GH_Integer(h.neighbourHandle), p);
                }

            }

            DA.SetDataTree(0, hOTree);
            DA.SetDataTree(1, nOTree);
            DA.SetDataTree(2, nHTree);

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
                return Resources.Extract_Topology;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("f39afbef-da84-45b9-810d-2f09fef99d82"); }
        }
    }
}