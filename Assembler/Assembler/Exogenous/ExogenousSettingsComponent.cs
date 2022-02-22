using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;
using System.Windows.Forms;

namespace Assembler
{
    public class ExogenousSettingsComponent : GH_Component
    {
        private bool hasContainer;
        private BoundingBox _clip;
        private Mesh _container;
        private List<Mesh> _solids;
        private List<Mesh> _voids;
        //private List<Color> _color;
        private readonly Color containerColor = Color.Black;
        private readonly Color solidColor = Color.FromArgb(115, 124, 148);
        private readonly Color voidColor = Color.FromArgb(146, 51, 51);

        /// <summary>
        /// Initializes a new instance of the ExogeousSettingsComp class.
        /// </summary>
        public ExogenousSettingsComponent()
          : base("Exogenous Settings ", "ExoSet",
              "Collects exogenous related settings",
              "Assembler", "Exogenous")
        {
            hasContainer = GetValue("HasContainer", false);
            UpdateMessage();
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Environment Meshes", "ME", "Closed Meshes as environmental objects" +
                "\nMesh normal direction decides the object type\noutwards: obstacle\ninward: void" +
                "\nNOTE: if the right-click menu 'Use Container' option is active, the first Mesh in a non-empty list will be used as a container", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Environment Mode", "eM",
                "Environment interaction mode" +
                "\n0 - ignore environmental objects" +
                "\n1 - use objects - container collision" +
                "\n2 - use objects - container inclusion",
                GH_ParamAccess.item, 1);
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddNumberParameter("Field scalar Threshold", "fT", "Threshold value for scalar field based criteria - normalized range (0-1)", GH_ParamAccess.item, 0.5);
            pManager.AddBoxParameter("Sandbox", "sB", "Sandbox for focused assemblages (NOT IMPLEMENTED YET)\nif present, Assemblage will grow only inside the Box", GH_ParamAccess.item, Box.Empty);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Exogenous Settings", "ES", "Exogenous Settings for the Assemblage", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // refactor this: since there can only be 1 container, make it the first mesh of the list
            // any other eventual mesh is treated as obstacle (simpler and clearer)
            // just check if its normals are inverted (negative volume)
            // then, refactor ExogenousSettings Struct to make EnvronmentMeshes directly
            // exogenous
            List<Mesh> ME = new List<Mesh>();
            if (!DA.GetDataList("Environment Meshes", ME)) return;
            // check environment meshes and remove nulls and invalids
            int meshCount = ME.Count;
            //int countContainers = 0;
            for (int i = ME.Count - 1; i >= 0; i--)
            {
                if (ME[i] == null || !ME[i].IsValid) ME.RemoveAt(i);
                //if (ME[i].Volume() < 0)
                //{
                //    if (countContainers == 0)
                //    {
                //        _container = ME[i];
                //        countContainers++;
                //    }
                //    else
                //    {
                //        countContainers++;
                //        break;
                //    }
                //}
                //else _obstacles.Add(ME[i]);


            }

            if (ME.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "All input Meshes are null or invalid");

            //if (countContainers > 1)
            //    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "More than one container detected - use only one container mesh\nOnly the first container mesh was retained");

            if (ME.Count != meshCount)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Some Environment Meshes are null or invalid and have been removed from the list");

            int eM = 0;
            DA.GetData("Environment Mode", ref eM);
            Field F = null;
            if (!DA.GetData("Field", ref F)) F = null;
            double fT = 0;
            DA.GetData("Field scalar Threshold", ref fT);
            Box sandbox = Box.Empty;
            if (DA.GetData("Sandbox", ref sandbox))
                if (!sandbox.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Sandbox is invalid and it will be ignored");
                    sandbox = Box.Empty;
                }

            ExogenousSettings ES = new ExogenousSettings(ME, eM, F, fT, sandbox, hasContainer);

            // assign Display geometries
            foreach (MeshEnvironment mEnv in ES.environmentMeshes)
            {
                switch (mEnv.type)
                {
                    case MeshEnvironment.Type.Void: // controls only centroid in/out
                        _voids.Add(mEnv.mesh);
                        break;
                    case MeshEnvironment.Type.Solid:
                        _solids.Add(mEnv.mesh);
                        break;
                    case MeshEnvironment.Type.Container:
                        _container = mEnv.mesh;
                        break;
                }

                _clip.Union(mEnv.mesh.GetBoundingBox(false));
            }

            // output data
            DA.SetData(0, ES);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Use Container", Container_click, true, hasContainer);
            toolStripMenuItem.ToolTipText = "When this option is checked, the first Mesh in the list will be flagged as Container";
            Menu_AppendSeparator(menu);
        }

        private void Container_click(object sender, EventArgs e)
        {
            RecordUndoEvent("Use Container");
            hasContainer = !GetValue("HasContainer", false);
            SetValue("HasContainer", hasContainer);

            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        private void UpdateMessage()
        {
            Message = hasContainer ? "Container" : "";
        }

        /// <summary>
        /// This method will be called once every solution, before any calls to RunScript.
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            _clip = BoundingBox.Empty;
            _solids = new List<Mesh>();
            _voids = new List<Mesh>();
            _container = new Mesh();
        }

        //Return a BoundingBox that contains all the geometry you are about to draw.
        public override BoundingBox ClippingBox
        {
            get { return _clip; }
        }

        //Draw all wires and points in this method.
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (base.Attributes.Selected)
            {
                args.Display.DrawMeshWires(_container, args.WireColour_Selected, args.DefaultCurveThickness);
                foreach (Mesh mOb in _solids)
                    args.Display.DrawMeshWires(mOb, args.WireColour_Selected, args.DefaultCurveThickness);
                foreach (Mesh mVoid in _voids)
                    args.Display.DrawMeshWires(mVoid, args.WireColour_Selected, args.DefaultCurveThickness);
            }
            else
            {
                args.Display.DrawMeshWires(_container, containerColor, 1);
                //for (int i = 0; i < _container.Faces.Count; i++)
                //{
                //    args.Display.DrawArrow(new Line(_container.Faces.GetFaceCenter(i), _container.FaceNormals[i], 1.0), containerColor);
                //}
                foreach (Mesh mOb in _solids)
                {
                    args.Display.DrawMeshWires(mOb, solidColor, 2);
                    //for (int i = 0; i < mOb.Faces.Count; i++)
                    //{
                    //    args.Display.DrawArrow(new Line(mOb.Faces.GetFaceCenter(i), mOb.FaceNormals[i], 1.0), solidColor);
                    //}
                }
                foreach (Mesh mVoid in _voids)
                {
                    args.Display.DrawMeshWires(mVoid, voidColor, 2);
                    //for (int i = 0; i < mVoid.Faces.Count; i++)
                    //{
                    //    args.Display.DrawArrow(new Line(mVoid.Faces.GetFaceCenter(i), mVoid.FaceNormals[i], 1.0), voidColor);
                    //}
                }
            }
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
                return Resources.Exogenous_settings;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("2c752362-8f0c-4044-8f85-9e87b6c4939d"); }
        }
    }
}