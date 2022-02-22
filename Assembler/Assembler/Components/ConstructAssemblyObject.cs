using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Utils;
using Assembler.Properties;
using System.Windows.Forms;
using GH_IO.Serialization;

namespace Assembler
{
    public class ConstructAssemblyObject : GH_Component
    {
        // this should be the rightful implementation (see https://developer.rhino3d.com/api/grasshopper/html/5f6a9f31-8838-40e6-ad37-a407be8f2c15.htm)
        // but it screws up existing components (must be manually replaced in definitions!)
        // so I'm doing it "old school"
        private bool worldZLock = false;
        //public bool Absolute
        //{
        //    get { return absoluteZLock; }
        //    set
        //    {
        //        absoluteZLock = value;
        //        if (absoluteZLock)
        //        {
        //            Message = "World Z Lock";
        //        }
        //        else
        //        {
        //            Message = "";
        //        }
        //    }
        //}

        /// <summary>
        /// Initializes a new instance of the ConstructAssemblyObject class.
        /// </summary>
        public ConstructAssemblyObject()
          : base("Construct AssemblyObject", "AOCon",
              "Construct an Assembly Object from relevant data",
              "Assembler", "Components")
        {
            //Absolute = false;//GetValue("ZLock", false);
            worldZLock = GetValue("ZLockAO", false);
            UpdateMessage();
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The object's unique name", GH_ParamAccess.item);
            pManager.AddMeshParameter("Collision Mesh", "M", "The mesh geometry used for collision checks", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Reference Plane", "P", "The object's reference plane", GH_ParamAccess.item);
            pManager.AddVectorParameter("Direction", "D", "The object's direction vector", GH_ParamAccess.item);
            pManager.AddNumberParameter("Weight", "W", "The object's weight (optional)", GH_ParamAccess.item, 1.0);
            pManager.AddGenericParameter("Handles", "H", "The object's Handles", GH_ParamAccess.list);
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assembly Object", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            int type = 0;
            Mesh cm = new Mesh();
            Plane rp = new Plane();
            Vector3d d = Vector3d.Zero;
            double w = 1.0;
            List<Handle> h = new List<Handle>();

            // input data sanity checks
            if (!DA.GetData("Name", ref name)) return;
            if (!DA.GetData("Collision Mesh", ref cm)) return;
            if (!DA.GetData("Reference Plane", ref rp)) return;
            if (!DA.GetData("Direction", ref d)) return;
            DA.GetData("Weight", ref w);
            if (!DA.GetDataList(5, h)) return;

            // if collision mesh is null return
            if (cm == null) return;
            // if reference plane is null return
            if (rp == null) return;
            // if direction is null or zero return
            if (d == null || d == Vector3d.Zero) return;
            // if Handles are empty return
            if (h.Count == 0) return;

            // cast Handles to array
            Handle[] handles = h.ToArray();

            // construct the AssemblyObject                                                                        v Zlock
            AssemblyObject AO = new AssemblyObject(cm, handles, rp, d, name, type, w, -1, new List<Support>(), 0, worldZLock);

            DA.SetData("Assembly Object", new AssemblyObjectGoo(AO));

        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "World Z lock", ZLock_click, true, worldZLock);// GetValue("Z Lock", false));
            toolStripMenuItem.ToolTipText = "When active (and if the check option is active in the Engine) the object will be placed ony with its reference Plane Z axis parallel to the World Z axis";
            Menu_AppendSeparator(menu);
        }

        private void ZLock_click(object sender, EventArgs e)
        {
            RecordUndoEvent("World Z lock");
            worldZLock = !GetValue("ZLockAO", false);
            SetValue("ZLockAO", worldZLock);
            UpdateMessage();
            ExpireSolution(true);
        }

        private void UpdateMessage()
        {
            Message = worldZLock ? "World Z Lock" : "";
        }

        public override bool Write(GH_IWriter writer)
        {
            // NOTE: the value in between "" is shared AMONG ALL COMPONENTS of a librbary!
            // ZLockAO is accessible (and modifyable) by other components!
            writer.SetBoolean("ZLockAO", worldZLock);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("ZLockAO", ref worldZLock);
            UpdateMessage();
            return base.Read(reader);
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
                return Resources.Construct_AO;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3aa66311-1c61-4923-8cc7-675545b770bf"); }
        }
    }
}