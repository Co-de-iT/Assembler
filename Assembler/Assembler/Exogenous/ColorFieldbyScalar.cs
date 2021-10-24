using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;
using System.Threading;
using System.Windows.Forms;
using GH_IO.Serialization;

namespace Assembler
{
    public class ColorFieldbyScalar : GH_Component
    {
        private bool blend;
        private PointCloud _cloud;
        private BoundingBox _clip;
        private int thickness;

        /// <summary>
        /// Initializes a new instance of the ColorFieldbyScalar class.
        /// </summary>
        public ColorFieldbyScalar()
          : base("Color Field by Scalar", "AFColS",
              "Generates Field Point colors by scalar values",
              "Assembler", "Exogenous")
        {
            blend = GetValue("FieldValueBlend", false);
            UpdateMessage();
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "C", "List of Colors for scalars\nonly 2 colors are needed", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Index", "i", "Index of scalar value to sample\n0 (default) for single scalar value per Field point", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Threshold", "T", "Threshold for Allocation\nif Blend option is true this value is ingored", GH_ParamAccess.item, 0.5);
            //pManager.AddBooleanParameter("Blend", "B", "Blends weight values or assigns them according to the threshold", GH_ParamAccess.item, false);
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

            List<Color> C = new List<Color>();
            if (!DA.GetDataList(1, C)) return;
            if (C.Count != 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "You must provide 2 Colors");
                return;
            }
            int ind = 0;
            DA.GetData("Index", ref ind);

            if (fCol.tensors[0].scalar == null || ind > fCol.tensors[0].scalar.Length)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Field does not have scalar values at specified index");
                return;
            }

            double thres = 0.5;
            DA.GetData("Threshold", ref thres);

            //bool blend = false;
            //DA.GetData("Blend", ref blend);

            fCol.GenerateScalarColors(C[0], C[1], ind, thres, blend);

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

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Blend values", Blend_Click, true, blend);
            toolStripMenuItem.ToolTipText = "Blends (interpolates) values";
            Menu_AppendSeparator(menu);
        }

        private void Blend_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Blend values");
            blend = !GetValue("FieldValueBlend", false);
            SetValue("FieldValueBlend", blend);
            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("FieldValueBlend", blend);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("FieldValueBlend", ref blend);
            UpdateMessage();
            return base.Read(reader);
        }

        private void UpdateMessage()
        {
            string message = "";
            if (blend)
                message += "blend";

            Message = message;
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
                return Resources.Color_Field_scalars;
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
            get { return new Guid("4728e8c6-1670-4d75-8f6c-30654ac32568"); }
        }
    }
}