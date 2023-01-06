using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using AssemblerLib;

namespace Assembler
{
    public class D_ExtractOffsetMesh : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the D_ExtractOffsetMesh class.
        /// </summary>
        public D_ExtractOffsetMesh()
          : base("Extract Offset Mesh", "AOOMesh",
              "Extract collision mesh from AssemblyObject class\nDEBUG component",
              "Assembler", "Components")
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
            pManager.AddMeshParameter("Mesh", "M", "Offest Mesh in AssemblyObject", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            AssemblyObject ao = null;
            if (!DA.GetData(0, ref ao)) return;

            Mesh m = new Mesh();
            m.CopyFrom(ao.OffsetMesh);
            m.Unweld(0, true);

            DA.SetData(0, m);
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
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("a8b7e490-94b2-4a96-914e-cb471599b6b4"); }
        }
    }
}