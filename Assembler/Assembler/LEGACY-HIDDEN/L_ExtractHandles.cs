using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System;

namespace Assembler
{
    [Obsolete]
    public class L_ExtractHandles : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExtractSenderHandlesPlanes class.
        /// </summary>
        public L_ExtractHandles()
          : base("Extract Handles", "AOeHandles",
              "Extract essential Handle information for an AssemblyObject",
             "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject", "AO", "input AssemblyObject", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Handles Sender Planes", "SP", "Sender Planes of each Handle in the AssemblyObject", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Handles Types", "T", "Type of each Handle in the AssemblyObject", GH_ParamAccess.list);
            pManager.AddNumberParameter("Handles Weights", "W", "Weight of each Handle in the AssemblyObject", GH_ParamAccess.list);
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

            GH_Plane[] hSPlanes = new GH_Plane[AO.Handles.Length];
            GH_Integer[] hTypes = new GH_Integer[AO.Handles.Length];
            GH_Number[] hWeights = new GH_Number[AO.Handles.Length];

            for (int i = 0; i < AO.Handles.Length; i++)
            {
                hSPlanes[i] = new GH_Plane(AO.Handles[i].SenderPlane);
                hTypes[i] = new GH_Integer(AO.Handles[i].Type);
                hWeights[i] = new GH_Number(AO.Handles[i].Weight);
            }

            DA.SetDataList(0, hSPlanes);
            DA.SetDataList(1, hTypes);
            DA.SetDataList(2, hWeights);
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
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Resources.L_Extract_Sender_Handles_Planes;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8f3c72d5-1256-4798-b28f-2703cefb7ffc"); }
        }
    }
}