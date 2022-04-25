using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    [Obsolete]
    public class H_ColorFieldbyAttractors : GH_Component
    {
        private PointCloud _cloud;
        private BoundingBox _clip;
        private int thickness;

        /// <summary>
        /// Initializes a new instance of the ColorFieldbyAttractors class.
        /// </summary>
        public H_ColorFieldbyAttractors()
          : base("Color Field by Attractors", "AFColA",
              "Generates Field Point colors by attractor points",
              "Assembler", "Exogenous")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddPointParameter("Attractor Points", "A", "Attractor points (2 or more)", GH_ParamAccess.list);
            pManager.AddColourParameter("Attractor Colors", "C", "List of Colors for attractors\none color per attractor point", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Blend", "B", "Blends RGB values or assigns to the closest Attractor", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Colored Field", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "C", "Field Colors", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Field f = null, fCol;
            if (!DA.GetData(0, ref f)) return;

            fCol = new Field(f);

            List<Point3d> A = new List<Point3d>();
            if (!DA.GetDataList("Attractor Points", A)) return;

            List<Color> C = new List<Color>();
            if (!DA.GetDataList("Attractor Colors", C)) return;

            if (C.Count != A.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Number of Attractor Points and Colors must match");
                return;
            }

            bool blend = false;
            DA.GetData("Blend", ref blend);

            fCol.GenerateColorsByAttractors(C, A, blend);

            _cloud = new PointCloud();
            _cloud.AddRange(fCol.GetPoints(), fCol.colors);
            _clip = _cloud.GetBoundingBox(false);

            DA.SetData(0, fCol);
            DA.SetDataList(1, fCol.colors);

        }

        /// <summary>
        /// This method will be called once every solution, before any calls to RunScript.
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            _clip = BoundingBox.Empty;
            _cloud = null;
        }

        //Return a BoundingBox that contains all the geometry you are about to draw.
        public override BoundingBox ClippingBox
        {
            get { return _clip; }
        }

        //Draw all wires and points in this method.
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_cloud == null)
                return;
            if (base.Attributes.Selected) thickness = 4; else thickness = 2;

            args.Display.DrawPointCloud(_cloud, thickness);
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
                return Resources.Color_Field_attractors;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.hidden; }//teriary
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("bac51b12-bae3-46c4-bc2c-fccf3349e80c"); }
        }
    }
}