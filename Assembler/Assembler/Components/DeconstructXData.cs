﻿using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using System;

namespace Assembler
{
    public class DeconstructXData : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructXData class.
        /// </summary>
        public DeconstructXData()
          : base("Deconstruct XData", "XDDecon",
              "Deconstructs an XData item",
              "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("XData", "XD", "Extended Data associated to an AssemblyObject Type", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        { 
            pManager.AddTextParameter("Label", "L", "Label for the data", GH_ParamAccess.item);
            pManager.AddTextParameter("AssemblyObject Name reference", "N", "AssemblyObject name to which XData is associated", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Reference Plane", "P", "Reference plane for extended data", GH_ParamAccess.item);
            pManager.AddGenericParameter("Data", "D", "Data", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            XData xd = null;
            if (!DA.GetData(0, ref xd)) return;

            DA.SetData(0, xd.label);
            DA.SetData(1, xd.AOName);
            DA.SetData(2, xd.ReferencePlane);
            DA.SetDataList(3, xd.Data);
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
                return Resources.Deconstruct_XData;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("6F1C531D-33E9-44F2-912A-086CD7717526"); }
        }
    }
}