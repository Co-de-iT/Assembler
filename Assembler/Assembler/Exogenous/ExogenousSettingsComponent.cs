using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    public class ExogenousSettingsComponent : GH_Component
    {
        private BoundingBox _clip;
        private Mesh _container;
        private List<Mesh> _obstacles;
        //private List<Color> _color;
        private readonly Color containerColor = Color.Black;
        private readonly Color obstacleColor = Color.FromArgb(115, 124, 148);

        /// <summary>
        /// Initializes a new instance of the ExogeousSettingsComp class.
        /// </summary>
        public ExogenousSettingsComponent()
          : base("Exogeous Settings ", "ExoSet",
              "Collects exogenous related settings",
              "Assembler", "Exogenous")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Environment Meshes", "ME", "Closed Meshes as environmental objects\noptional\nMesh normal direction decides the object type\noutwards: obstacle\ninward: container", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Environment Mode", "eM",
                "Environment interaction mode" +
                "\n0 - ignore" +
                "\n1 - collision" +
                "\n2 - inclusion",
                GH_ParamAccess.item, 1);
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddNumberParameter("Field scalar Threshold", "fT", "Threshold value for scalar field based criteria - normalized range (0-1)", GH_ParamAccess.item, 0.5);
            pManager.AddBoxParameter("Sandbox", "sB", "Sandbox for focused assemblages (EXPERIMENTAL)\nif present, Assemblage will grow only inside the Box", GH_ParamAccess.item, Box.Empty);

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
            if (!DA.GetDataList("Environment Meshes", ME)) return;
            // check environment meshes and remove nulls and invalids
            int meshCount = ME.Count;
            int countContainers = 0;
            for (int i = ME.Count - 1; i >= 0; i--)
            {
                if (ME[i] == null || !ME[i].IsValid) ME.RemoveAt(i);
                if (ME[i].Volume() < 0)
                {
                    if (countContainers == 0)
                    {
                        _container = ME[i];
                        countContainers++;
                    }
                    else
                    {
                        countContainers++;
                        break;
                    }
                }
                else _obstacles.Add(ME[i]);

                _clip.Union(ME[i].GetBoundingBox(false));
            }
            if (countContainers > 1)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "More than one container detected - use only one container mesh\nOnly the first container mesh was retained");

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

            ExogenousSettings ES = new ExogenousSettings(ME, eM, F, fT, sandbox);

            DA.SetData(0, ES);
        }


        /// <summary>
        /// This method will be called once every solution, before any calls to RunScript.
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            _clip = BoundingBox.Empty;
            _obstacles = new List<Mesh>();
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
                foreach (Mesh ob in _obstacles)
                    args.Display.DrawMeshWires(ob, args.WireColour_Selected, args.DefaultCurveThickness);
            }
            else
            {
                args.Display.DrawMeshWires(_container, containerColor);
                foreach (Mesh ob in _obstacles)
                    args.Display.DrawMeshWires(ob, obstacleColor, 2);
            }
        }

        /// <summary>
        /// Exposure override for position in the SUbcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.quarternary; }
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