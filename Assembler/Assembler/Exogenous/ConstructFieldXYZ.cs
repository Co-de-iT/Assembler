using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;

namespace Assembler
{
    public class ConstructFieldXYZ : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructFieldXYZ class.
        /// </summary>
        public ConstructFieldXYZ()
          : base("Construct Field XYZ", "AFieldXYZ",
              "Constructs an empty Field from a 3D geometry, using its BoundingBox\nIndividual n. of cells along BoundingBox XYZ dimensions",
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
            pManager.AddIntegerParameter("N Cells X", "Nx", "Number of cells along X dimension", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("N Cells Y", "Ny", "Number of cells along Y dimension", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("N Cells Z", "Nz", "Number of cells along Z dimension", GH_ParamAccess.item, 10);

            pManager[1].Optional = true;
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
            IGH_GeometricGoo gb = null;
            if (!DA.GetData("Geometry", ref gb)) return;
            if (gb == null) return;
            Plane plane = Plane.WorldXY;
            DA.GetData("Plane", ref plane);

            int nCX = 1;
            DA.GetData("N Cells X", ref nCX);
            if (nCX <= 0) nCX = 10;
            int nCY = 1;
            DA.GetData("N Cells Y", ref nCY);
            if (nCY <= 0) nCY = 10;
            int nCZ = 1;
            DA.GetData("N Cells Z", ref nCZ);
            if (nCZ <= 0) nCZ = 10;

            BoundingBox bbox;
            Box fieldBox = Box.Empty;
            Field f;

            if (gb.TypeName == "Box")
            {
                Box b = Box.Empty;
                GeometryBase g = GH_Convert.ToGeometryBase(gb);
                GH_Convert.ToBox_Primary(gb, ref b);
                if(!b.IsValid)
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


            f = new Field(fieldBox, nCX, nCY, nCZ);

            DA.SetData("Field", f);
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
                return Resources.Field_From_Geometry_XYZ;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8c148f33-6b52-4351-a10b-cdbf87639770"); }
        }
    }
}