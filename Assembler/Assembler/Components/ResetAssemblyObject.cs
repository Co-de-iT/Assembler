using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Assembler
{
    public class ResetAssemblyObject : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ResetAssemblyObject class.
        /// </summary>
        public ResetAssemblyObject()
          : base("Reset AssemblyObject", "AOReset",
              "Resets an AssemblyObject's values to default",
              "Assembler", "Components")
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
            pManager.AddGenericParameter("AssemblyObject", "AO", "An AssemblyObject", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject", "AO", "The reset AssemblyObject", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AssemblyObjectGoo GH_AO = null;
            AssemblyObject AO, AOreset;
            // sanity check on inputs
            if (!DA.GetData(0, ref GH_AO)) return;
            AO = GH_AO.Value;

            // this fixes compatibility issues with saved assemblages prior to version 1.1.9
            // Rotations and RDictionary were null in those cases
            for (int i = 0; i < AO.Handles.Length; i++)
            {
                if (AO.Handles[i].Rotations == null)
                    AO.Handles[i].Rotations = new double[0];
                if (AO.Handles[i].RDictionary == null)
                    AO.Handles[i].RDictionary = new Dictionary<double, int>();
            }

            AOreset = AssemblyObjectUtils.Clone(AO);

            DA.SetData(0, new AssemblyObjectGoo(AOreset));
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
                return Resources.Reset_AO;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("81A630BA-1B6F-423E-AFD5-2535F371507E"); }
        }
    }
}