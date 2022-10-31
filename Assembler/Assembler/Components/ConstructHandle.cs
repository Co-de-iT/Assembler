using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class ConstructHandle : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructHandle class.
        /// </summary>
        public ConstructHandle()
          : base("ConstructHandle", "HandCon",
              "Construct a Handle",
               "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Polyline", "LP", "L-shaped Polyline", GH_ParamAccess.item);
            pManager.AddNumberParameter("Rotation angles", "R", "Rotation angles in degrees - as List", GH_ParamAccess.list, new List<double> { 0.0 });
            pManager.AddIntegerParameter("Type", "T", "Handle type", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Weight", "W", "Handle Weight", GH_ParamAccess.item, 1.0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Handle", "H", "Handle", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // get data
            Handle handle;
            Curve polylineAsCurve = null;
            Polyline polyLineL;
            List<double> rotationsList = new List<double>();
            int type = 0;
            double weight = 1.0;

            // sanity checks
            if (!DA.GetData(0, ref polylineAsCurve)) return;
            if (!DA.GetDataList(1, rotationsList)) return;
            DA.GetData(2, ref type);
            DA.GetData(3, ref weight);

            if (polylineAsCurve == null)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide an L-shaped polyline");

            if (rotationsList == null || rotationsList.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Please specify one or more rotations");
                return;
            }

            if (!polylineAsCurve.TryGetPolyline(out polyLineL))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please feed an L-shaped polyline");
                return;
            }

            if (polyLineL.Count != 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Polyline must have 3 points and be L-shaped");
                return;
            }

            // create handle
            handle = new Handle(polyLineL, type, weight, rotationsList);

            // output handle
            DA.SetData(0, handle);
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
                return Resources.Construct_Handle;
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
            get { return new Guid("C779AD92-86A3-4790-8107-7AC95FCB2E6B"); }
        }
    }
}