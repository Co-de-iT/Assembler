using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;

namespace Assembler
{
    public class ConstructFieldN : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructFieldN class.
        /// </summary>
        public ConstructFieldN()
           : base("Construct Field N", "AFieldN",
              "Constructs an empty Field from a 3D geometry, using its BoundingBox\nN cells along largest BoudingBox dimension",
              "Assembler", "Exogenous")
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
            pManager.AddGeometryParameter("Geometry", "G", "Geometry for Field generation\nInput any geometry that has a 3D Bounding Box", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "P", "Reference plane for Bounding Box Orientation\n" +
                "if the geometry is a Box, its orientation will be taken and this input will be ignored", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("N Cells", "N", "Number of cells along largest dimension", GH_ParamAccess.item, 10);

            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Empty Field", GH_ParamAccess.item);
            pManager.AddVectorParameter("Field cells dimensions", "Fd", "Vector with number of cells in XYZ direction", GH_ParamAccess.item);
            pManager.AddPointParameter("Field Points", "P", "Field Points", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_GeometricGoo gb = null;
            if (!DA.GetData("Geometry", ref gb)) return;
            if (gb == null) return;
            Plane plane = Plane.WorldXY;
            DA.GetData("Plane", ref plane);

            int nCells = 1;
            DA.GetData("N Cells", ref nCells);
            if (nCells <= 0) nCells = 10;

            BoundingBox bbox;
            Box fieldBox = Box.Empty;
            Field f;

            if (gb.TypeName == "Box")
            {
                Box b = Box.Empty;
                GeometryBase g = GH_Convert.ToGeometryBase(gb);
                GH_Convert.ToBox_Primary(gb, ref b);
                if (!b.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Box is invalid, cannot crete Field");
                    return;
                }
                fieldBox = b;
            }
            else
            {
                bbox = gb.GetBoundingBox(Transform.PlaneToPlane(plane, Plane.WorldXY));
                if (!bbox.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Geometry is planar in the given Plane, cannot crete Field");
                    return;
                }
                fieldBox = new Box(plane, bbox);
            }


            f = new Field(fieldBox, nCells);

            Vector3d fieldCellsDims = new Vector3d(f.Nx, f.Ny, f.Nz);

            DA.SetData("Field", f);
            DA.SetData("Field cells dimensions", fieldCellsDims);
            DA.SetDataList("Field Points", f.GetGH_Points());
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
                return Resources.Field_From_Geometry_N;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("82A182B8-7311-4ADA-8071-CCF4C7A240DE"); }
        }
    }
}