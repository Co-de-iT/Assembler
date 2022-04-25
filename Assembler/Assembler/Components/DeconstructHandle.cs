using System;

using Grasshopper.Kernel;

using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    public class DeconstructHandle : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructHandle class.
        /// </summary>
        public DeconstructHandle()
          : base("Deconstruct Handle", "HanDecon",
              "Deconstruct Handle",
              "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Handle", "H", "Handle to deconstruct", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Sender Plane", "SP", "Sender Plane", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Receiver Planes", "RP", "Receiver Planes", GH_ParamAccess.list);
            pManager.AddNumberParameter("Receiver Planes rotations", "Rr", "Receiver Planes rotations", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Handle Type", "T", "Handle Type", GH_ParamAccess.item);
            pManager.AddNumberParameter("Handle Weight", "W", "Handle Weight", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Handle Occupancy", "hO", "Handle Occupancy status\n-1 occluded\n0 available\n1 connected", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Neighbour Object index", "nO", "Neighbour Object\nindex of neighbour AssemblyObject\n-1 if Handle is available", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Neighbour Handle index", "nH", "Neighbour Handle\nindex of neighbour AssemblyObject's Handle\n-1 if Handle is available or occluded", GH_ParamAccess.item);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Handle h = new Handle();
            
            // get data
            if(!DA.GetData(0, ref h)) return;
            
            // output data
            DA.SetData(0, h.sender);
            DA.SetDataList(1, h.receivers);
            DA.SetDataList(2, h.rRotations);
            DA.SetData(3, h.type);
            DA.SetData(4, h.weight);
            DA.SetData(5, h.occupancy);
            DA.SetData(6, h.neighbourObject);
            DA.SetData(7, h.neighbourHandle);
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
                return Resources.Deconstruct_Handle;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("de32c6be-fff2-4ba3-abc0-c3e9677cab6a"); }
        }
    }
}