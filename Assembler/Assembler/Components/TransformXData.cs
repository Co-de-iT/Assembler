using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    public class TransformXData : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the TransformXData class.
        /// </summary>
        public TransformXData()
          : base("Transform XData", "XDX",
              "Apply a transformation to an XData item",
              "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("XData", "XD", "Extended Data associated to an AssemblyObject after the assemblage", GH_ParamAccess.item);
            pManager.AddTransformParameter("Transformation", "X", "The Transformation to apply", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("XData", "XD", "Extended Data Transformed", GH_ParamAccess.item);
            
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            XData xd = null, xdT;
            Transform X = new Transform();
            if (!DA.GetData(0, ref xd)) return;
            if (!DA.GetData("Transformation", ref X)) return;

            xdT = new XData(xd);

            xdT.Transform(X);

            DA.SetData(0, xdT);
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
                return Resources.Transform_XData;
            }
        }

        /// <summary>
        /// Exposure override for position in the SUbcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.tertiary; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("01966d06-64ae-4c4a-9424-e204d5f672c8"); }
        }
    }
}