using Assembler.Properties;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
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
                "\nNOTE: if the right-click menu 'Use Container' option is active, the first Mesh in a non-empty list will be used as a container",
                GH_ParamAccess.list);
            pManager.AddIntegerParameter("Environment Mode", "eM",
                "Environment interaction mode" +
                "\n0 - ignore environmental objects" +
                "\n1 - use objects - container collision" +
                "\n2 - use objects - container inclusion" +
                "\n" +
                "\nattach a Value List for automatic list generation"+
                "\nIf no Mesh is provided in ME input, this will be forced to mode 0",
                GH_ParamAccess.item, 1);
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddNumberParameter("Field Scalar threshold", "Ft", "Threshold value (in normalized 0-1 range) for scalar Field based criteria", GH_ParamAccess.item, 0.5);
            pManager.AddBoxParameter("Sandbox", "sB", "Sandbox for focused assemblages (NOT IMPLEMENTED YET)\nif present, Assemblage will grow only inside the Box", GH_ParamAccess.item, Box.Empty);

            pManager[0].Optional = true;
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
            // exogenous
            List<Mesh> ME = new List<Mesh>();
            if (!DA.GetDataList("Environment Meshes", ME) || ME == null) ME = new List<Mesh>();

            // check environment meshes and remove nulls and invalids
            int meshCount = ME.Count;

            for (int i = ME.Count - 1; i >= 0; i--)
                if (ME[i] == null || !ME[i].IsValid) ME.RemoveAt(i);

            if (ME.Count != meshCount)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Some Environment Meshes are null or invalid and have been removed from the list");

            int eM = 0;
            DA.GetData("Environment Mode", ref eM);
            // if there are no Environment Meshes, set environment Mode to 0 (ignore)
            if (ME.Count == 0) eM = 0;

            Field F = null;
            if (!DA.GetData("Field", ref F)) F = null;
            double fT = 0;
            DA.GetData("Field Scalar threshold", ref fT);
            Box sandbox = Box.Empty;
            if (DA.GetData("Sandbox", ref sandbox))
                if (!sandbox.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Sandbox is invalid and it will be ignored");
                    sandbox = Box.Empty;
                }


            // __________________ autoList __________________

            // variable for the list
            GH_ValueList vList;

            // tries to cast input as list
            try
            {
                vList = (GH_ValueList)Params.Input[1].Sources[0];

                if (!vList.NickName.Equals("Environment Mode"))
                {
                    vList.ClearData();
                    vList.ListItems.Clear();
                    vList.NickName = "Environment Mode";

                    vList.ListItems.Add(new GH_ValueListItem("ignore", "0"));
                    vList.ListItems.Add(new GH_ValueListItem("container collision", "1"));
                    vList.ListItems.Add(new GH_ValueListItem("container inclusion", "2"));

                    vList.ListItems[0].Value.CastTo(out eM);
                }
            }
            catch
            {
                // handlesTree anything that is not a value list
            }

            ExogenousSettings ES = new ExogenousSettings(ME, eM, F, fT, sandbox, hasContainer);

            // assign Display geometries
            foreach (MeshEnvironment mEnv in ES.EnvironmentMeshes)
            {
                switch (mEnv.Type)
                {
                    case MeshEnvironment.EnvType.Void: // controls only centroid in/out
                        _voids.Add(mEnv.Mesh);
                        break;
                    case MeshEnvironment.EnvType.Solid:
                        _solids.Add(mEnv.Mesh);
                        break;
                    case MeshEnvironment.EnvType.Container:
                        _container = mEnv.Mesh;
                        break;
                }

                _clip.Union(mEnv.Mesh.GetBoundingBox(false));
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

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("HasContainer", hasContainer);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("HasContainer", ref hasContainer);
            UpdateMessage();
            return base.Read(reader);
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
            if (Locked) return;

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
                foreach (Mesh mOb in _solids) args.Display.DrawMeshWires(mOb, solidColor, 2);
                foreach (Mesh mVoid in _voids) args.Display.DrawMeshWires(mVoid, voidColor, 2);
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