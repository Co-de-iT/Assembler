using Assembler.Utils;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class D_SetSupports : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public D_SetSupports()
          : base("Set Supports", "AOSetSup",
              "Sets supports for an AssemblyObject",
              "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assembly Object", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
            pManager.AddLineParameter("Support Lines", "S", "Lines representing AssemblyObject's Supports", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Minimum suport number", "n", "Minimun number of connected supports to consider the object stable", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assembly Object", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AssemblyObjectGoo GH_AO = null;
            AssemblyObject newAO, AO = null;
            // sanity check on inputs
            if (!DA.GetData("Assembly Object", ref GH_AO)) return;
            AO = GH_AO.Value;
            List<Line> supportLines = new List<Line>();
            if (!DA.GetDataList(1, supportLines)) return;
            if (supportLines.Count < 1) return;
            int minSupports = 1;
            if (!DA.GetData(2, ref minSupports)) return;

            // limit minSupports to the maximum number of provided lines
            minSupports = Math.Min(minSupports, supportLines.Count);

            // create a new AO to avoid retroactive changes (AO is passed to this component as reference)
            newAO = AssemblyObjectUtils.Clone(AO);
            
            if (SupportUtils.SetSupports(newAO, supportLines, minSupports))
                DA.SetData(0, new AssemblyObjectGoo(newAO));
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
                return null;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.hidden; }
            //get { return GH_Exposure.primary; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("0e2a151f-4007-47b5-8c2d-2ae935e35cc5"); }
        }
    }
}