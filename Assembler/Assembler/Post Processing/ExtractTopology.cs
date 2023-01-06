using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class ExtractTopology : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExtractTopology class.
        /// </summary>
        public ExtractTopology()
          : base("ExtractTopology", "AOaTopo",
              "Extract connection data from an Assemblage",
              "Assembler", "Post Processing")
        {
            // this hides the component preview when placed onto the canvas
            // source: http://frasergreenroyd.com/how-to-stop-components-from-automatically-displaying-results-in-grasshopper/
            IGH_PreviewObject prevObj = (IGH_PreviewObject)this;
            prevObj.Hidden = true;
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
            pManager.AddIntegerParameter("Handle Occupancy", "hO", "Handle Occupancy status\n-1 occluded\n0 available\n1 connected", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Neighbour Object index", "nO", "Neighbour Object\nindex of neighbour AssemblyObject (connected or occluding)\n-1 if Handle is available", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Neighbour Handle index", "nH", "Neighbour Handle\nindex of neighbour AssemblyObject's connected Handle\n-1 if Handle is available or occluded", GH_ParamAccess.tree);
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
            AO = AOa.AssemblyObjects.AllData();

            GH_Structure<GH_Integer> hOTree = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Integer> nOTree = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Integer> nHTree = new GH_Structure<GH_Integer>();

            GH_Path p;
            for (int i = 0; i < AO.Count; i++)
            {
                //Topology main path is the object AInd
                p = new GH_Path(0, AO[i].AInd);
                foreach (Handle h in AO[i].Handles)
                {
                    hOTree.Append(new GH_Integer(h.Occupancy), p);
                    nOTree.Append(new GH_Integer(h.NeighbourObject), p);
                    nHTree.Append(new GH_Integer(h.NeighbourHandle), p);
                }

            }

            DA.SetDataTree(0, hOTree);
            DA.SetDataTree(1, nOTree);
            DA.SetDataTree(2, nHTree);

        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.tertiary; }
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