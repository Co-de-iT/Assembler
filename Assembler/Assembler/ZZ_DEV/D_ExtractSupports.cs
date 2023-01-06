using Assembler.Utils;
using AssemblerLib;
using GH_IO.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class D_ExtractSupports : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExtractSupports class.
        /// </summary>
        public D_ExtractSupports()
          : base("ExtractSupports", "AOXtrSup",
              "Extract Supports from an AssemblyObject",
              "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assembly Object", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Support Lines", "S", "Lines representing AssemblyObject's Supports", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Minimum suport number", "n", "Minimun number of connected supports to consider the object stable", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AssemblyObjectGoo GH_AO = null;
            AssemblyObject AO;
            if (!DA.GetData(0, ref GH_AO)) return;

            AO = GH_AO.Value;


            List<Line> lines = new List<Line>();

            foreach (Support s in AO.supports)
                lines.Add(s.Line);

            DA.SetDataList(0, lines);
            DA.SetData(1, AO.minSupports);
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
            //get { return GH_Exposure.secondary; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("6a84f29a-d24a-4f24-a819-744acec607c5"); }
        }
    }
}