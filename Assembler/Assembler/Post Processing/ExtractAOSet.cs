using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    public class ExtractAOSet : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExtractAOSet class.
        /// </summary>
        public ExtractAOSet()
          : base("Extract AOSet", "AOaSet",
              "Extracts the set of unique AssemblyObjects kinds used in an Assemblage",
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
            pManager.AddGenericParameter("AssemblyObject Set", "AOs", "The set of AssemblyObjects used in tha Assemblage", GH_ParamAccess.list);
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
            //if (!DA.GetDataList("Assemblage", AO)) return;
            if (!DA.GetData("Assemblage", ref AOa)) return;

            if (AOa == null) return;

            List<AssemblyObjectGoo> GH_AOset = AOa.AOSet.Select(ao => new AssemblyObjectGoo(ao)).ToList();

            DA.SetDataList(0, GH_AOset);
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
                return Resources.Extract_AOSet;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("941ef5c9-ab33-40e8-97b0-ba8d07835896"); }
        }
    }
}