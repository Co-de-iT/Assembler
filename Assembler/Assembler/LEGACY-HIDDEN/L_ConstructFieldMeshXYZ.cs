using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    [Obsolete]
    public class L_ConstructFieldMeshXYZ : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructFieldMeshXYZ class.
        /// </summary>
        public L_ConstructFieldMeshXYZ()
          : base("Construct Field from Mesh XYZ - LEGACY", "AFieldMXYZ",
              "Constructs an empty Field from a Mesh\nIndividual Resolutions for Box XYZ sizes\nLEGACY version",
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
            pManager.AddIntegerParameter("N Cells X", "Nx", "Number of cells along X dimension", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("N Cells Y", "Ny", "Number of cells along Y dimension", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("N Cells Z", "Nz", "Number of cells along Z dimension", GH_ParamAccess.item, 10);
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
            int nCX = 1;
            DA.GetData("N Cells X", ref nCX);
            if (nCX <= 0) nCX = 10;
            int nCY = 1;
            DA.GetData("N Cells Y", ref nCY);
            if (nCY <= 0) nCY = 10;
            int nCZ = 1;
            DA.GetData("N Cells Z", ref nCZ);
            if (nCZ <= 0) nCZ = 10;

            BoundingBox bbox = M.GetBoundingBox(P);
            Box box = new Box(P, bbox);

            Field f = new Field(box, nCX, nCY, nCZ); //new Field(M, P, nCX, nCY, nCZ);

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
                return Resources.Field_XYZ_OLD;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3ed892db-dc8f-49a0-bbf7-19a2c6ac9dd3"); }
        }
    }
}