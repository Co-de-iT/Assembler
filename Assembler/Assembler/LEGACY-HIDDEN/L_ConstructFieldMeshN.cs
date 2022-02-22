using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    [Obsolete]
    public class L_ConstructFieldMeshN : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructFieldMeshN class.
        /// </summary>
        public L_ConstructFieldMeshN()
          : base("Construct Field form Mesh N - LEGACY", "AFieldMN",
              "Constructs an empty Field from a Mesh\nSingle N subdivision for the largest Box size\nLEGACY version",
              "Assembler", "Exogenous")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh that contains the Field", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "P", "Reference plane for Bounding Box Orientation", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("N Cells", "N", "Number of cells along largest dimension", GH_ParamAccess.item, 10);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Empty Field", GH_ParamAccess.item);
            pManager.AddPointParameter("Field Points", "P", "Field Points", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh M = new Mesh();
            if (!DA.GetData("Mesh", ref M)) return;
            Plane P = new Plane();
            DA.GetData("Plane", ref P);
            int nCells = 1;
            DA.GetData("N Cells", ref nCells);
            if (nCells <= 0) nCells = 10;

            BoundingBox bbox = M.GetBoundingBox(P);
            Box box = new Box(P, bbox);

            Field f = new Field(box,nCells);// new Field(M,P, nCells);

            DA.SetData("Field", f);
            DA.SetDataList("Field Points", f.GetGH_Points());
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
                return Resources.Field_N_OLD;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("fb0d2895-77f7-4f71-b692-b5458f405f7e"); }
        }
    }
}