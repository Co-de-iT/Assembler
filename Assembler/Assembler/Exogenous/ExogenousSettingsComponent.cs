using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Assembler
{
    public class ExogenousSettingsComponent : GH_Component
    {
        private BoundingBox _clip;
        private Mesh _container;
        private List<Mesh> _solids;
        private List<Mesh> _voids;
        private Color containerColor = Color.Black;
        private Color solidColor;// = Color.FromArgb(115, 124, 148);
        private Color voidColor;// = Color.FromArgb(146, 51, 51);

        /// <summary>
        /// Initializes a new instance of the ExogeousSettingsComponent class.
        /// </summary>
        public ExogenousSettingsComponent()
          : base("Exogenous Settings ", "ExoSet",
              "Collects exogenous related settings\n" +
                "Previews Environment Meshes as follows:\n" +
                "solids: slate blue, light gray (ignore mode)`\n" +
                "voids: brick red, light gray (ignore mode)\n" +
                "container: black (collision mode), dark gray (inclusion mode), light gray (ignore mode)",
              "Assembler", "Exogenous")
        {
            Params.ParameterSourcesChanged += new GH_ComponentParamServer.ParameterSourcesChangedEventHandler(ParamSourceChanged);
            ExpireSolution(true);
        }

        // SOURCE: https://discourse.mcneel.com/t/automatic-update-of-valuelist-only-when-connected/152879/6?u=ale2x72
        // works much better as it does not clog the solver with exceptions if a list of numercal values is connected
        private void ParamSourceChanged(object sender, GH_ParamServerEventArgs e)
        {
            if ((e.ParameterSide == GH_ParameterSide.Input) && (e.ParameterIndex == 1))
            {
                foreach (IGH_Param source in e.Parameter.Sources)
                {
                    if (source is Grasshopper.Kernel.Special.GH_ValueList)
                    {
                        Grasshopper.Kernel.Special.GH_ValueList vList = source as Grasshopper.Kernel.Special.GH_ValueList;

                        if (!vList.NickName.Equals("Environment Mode"))
                        {
                            vList.ClearData();
                            vList.ListItems.Clear();
                            vList.NickName = "Environment Mode";

                            vList.ListItems.Add(new GH_ValueListItem("ignore", "0"));
                            vList.ListItems.Add(new GH_ValueListItem("container collision", "1"));
                            vList.ListItems.Add(new GH_ValueListItem("container inclusion", "2"));

                            vList.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.DropDown; // change this for a different mode (DropDown is the default)
                            vList.ExpireSolution(true);
                        }
                    }
                }
            }
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
                "\nattach a Value List for automatic list generation" +
                "\nIf no Mesh is provided in ME input, this will be forced to mode 0",
                GH_ParamAccess.item, 1);
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddNumberParameter("Field Scalar threshold", "Ft", "Threshold value (in normalized 0-1 range) for scalar Field based criteria", GH_ParamAccess.item, 0.5);
            //pManager.AddBoxParameter("Sandbox", "sB", "Sandbox for focused assemblages (NOT IMPLEMENTED YET)\nif present, Assemblage will grow only inside the Box", GH_ParamAccess.item, Box.Empty);
            pManager.AddBooleanParameter("Use Container", "C", "Set to True to flag the first Mesh in the list as a Container", GH_ParamAccess.item, false);

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

            string removedWhy;
            bool remove;
            for (int i = ME.Count - 1; i >= 0; i--)
            {
                remove = false;
                removedWhy = "";
                if (ME[i] == null)
                {
                    remove = true;
                    removedWhy = "null";
                }
                else if (!ME[i].IsValid)
                {
                    remove = true;
                    removedWhy = "invalid";
                }
                else if (!ME[i].IsClosed)
                {
                    remove = true;
                    removedWhy = "open";
                }
                if (remove)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Environment Mesh {i} is {removedWhy} and has been removed");
                    ME.RemoveAt(i);
                }
            }

            solidColor = Color.FromArgb(115, 124, 148);
            voidColor = Color.FromArgb(146, 51, 51);

            int eM = 0;
            DA.GetData("Environment Mode", ref eM);
            // if there are no Environment Meshes, set environment Mode to 0 (ignore)
            if (ME.Count == 0) eM = 0;
            // keep eM within limits
            if (eM < -1) eM = -1;
            if (eM > 2) eM = 2;
            switch (eM)
            {
                case -1: // custom
                    containerColor = Color.White;
                    break;
                case 0: // ignore
                    containerColor = Color.LightGray;
                    solidColor = Color.LightGray;
                    voidColor = Color.LightGray;
                    break;
                case 1: // container collision
                    containerColor = Color.Black;
                    break;
                case 2: // container inclusion
                    containerColor = Color.FromArgb(80, 80, 80);
                    break;
            }

            Field F = null;
            if (!DA.GetData("Field", ref F)) F = null;
            double fT = 0;
            DA.GetData("Field Scalar threshold", ref fT);
            Box sandbox = Box.Empty;
            bool useContainer = false;
            DA.GetData("Use Container", ref useContainer);
            // Update Message
            Message = useContainer ? "Container" : "";

            // EXP: experimental. Yet to be implemented
            //if (DA.GetData("Sandbox", ref sandbox))
            //    if (!sandbox.IsValid)
            //    {
            //        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Sandbox is invalid and it will be ignored");
            //        sandbox = Box.Empty;
            //    }

            ExogenousSettings ES = new ExogenousSettings(ME, eM, F, fT, sandbox, useContainer);

            // assign Display geometries
            foreach (MeshEnvironment mEnv in ES.EnvironmentMeshes)
            {
                switch (mEnv.Type)
                {
                    case EnvironmentType.Void: // controls only centroid in/out
                        _voids.Add(mEnv.Mesh);
                        break;
                    case EnvironmentType.Solid:
                        _solids.Add(mEnv.Mesh);
                        break;
                    case EnvironmentType.Container:
                        _container = mEnv.Mesh;
                        break;
                }

                _clip.Union(mEnv.Mesh.GetBoundingBox(false));
            }

            // output data
            DA.SetData(0, ES);
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
                args.Display.DrawMeshWires(_container, containerColor, 2);
                foreach (Mesh mOb in _solids) args.Display.DrawMeshWires(mOb, solidColor, 3);
                foreach (Mesh mVoid in _voids) args.Display.DrawMeshWires(mVoid, voidColor, 3);
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
                return Resources.Exogenous_Settings;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("60A81FB4-F9F7-4C3F-A6BC-997B52FCAD00"); }
        }
    }
}