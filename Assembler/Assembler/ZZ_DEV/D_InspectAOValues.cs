using System;
using System.Collections.Generic;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Assembler
{
    public class D_InspectAOValues : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public D_InspectAOValues()
          : base("Inspect AO values", "IAOval",
              "Inspect an AssemblyObject receiver and sender values (Debug)",
              "Assembler", "Components")
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
            pManager.AddNumberParameter("Receiver Value", "rV", "", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sender Value", "sV", "", GH_ParamAccess.item);
            pManager.AddIntegerParameter("iWeight", "iW", "", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AssemblyObjectGoo GH_AO = null;
            AssemblyObject AO;
            // sanity check on inputs
            if (!DA.GetData("Assembly Object", ref GH_AO)) return;
            AO = GH_AO.Value;

            DA.SetData(0, AO.ReceiverValue);
            DA.SetData(1, AO.SenderValue);
            DA.SetData(2, AO.IWeight);
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
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("F354C5D4-4849-4264-B897-E1ED8E77A9D5"); }
        }
    }
}