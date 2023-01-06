using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Assembler
{
    public class H_ConstructCompositeAO : GH_Component
    {
        private bool worldZLockComp;

        /// <summary>
        /// Initializes a new instance of the ConstructCompositeAO class.
        /// </summary>
        public H_ConstructCompositeAO()
          : base("Construct Composite AssemblyObject", "AOComCon",
              "Construct a composite Assembly Object",
              "Assembler", "Components")
        {
            worldZLockComp = GetValue("ZLockComp", false);
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
            pManager.AddGenericParameter("Children AO", "AOc", "The children AssemblyObjects forming the composite object", GH_ParamAccess.list);
            pManager.AddGenericParameter("Handles", "H", "The object's Handles\nIf no input is given, Handles are automatically calculated", GH_ParamAccess.list);
            pManager[4].Optional = true;
            pManager[6].Optional = true;
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
            List<AssemblyObjectGoo> GH_AOchildren = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOchildren = new List<AssemblyObject>();
            List<Handle> h = new List<Handle>();

            // input data sanity checks
            if (!DA.GetData("Name", ref name)) return;
            if (!DA.GetData("Collision Mesh", ref cm)) return;
            if (!DA.GetData("Reference Plane", ref rp)) return;
            if (!DA.GetData("Direction", ref d)) return;
            DA.GetData("Weight", ref w);
            if (!DA.GetDataList(5, GH_AOchildren)) return;

            AOchildren = GH_AOchildren.Select(ao => ao.Value).ToList();

            DA.GetDataList(6, h);

            // if collision mesh is null return
            if (cm == null) return;
            // if reference plane is null return
            if (rp == null) return;
            // if Direction is null or zero return
            if (d == null || d == Vector3d.Zero) return;
            // if children are null or empty return
            if (AOchildren == null || AOchildren.Count == 0) return;

            AssemblyObject AO;
            if (h != null && h.Count > 0)
            {
                // cast Handles to array
                Handle[] handles = h.Select(ha => (Handle)ha).ToArray();
                AO = new AssemblyObject(cm, handles, rp, d, name, type, w, worldZLockComp, AOchildren);
            }
            else
                AO = new AssemblyObject(cm, rp, d, name, type, w, worldZLockComp, AOchildren);

            DA.SetData("Assembly Object", new AssemblyObjectGoo(AO));
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "World Z lock", ZLock_click, true, worldZLockComp);
            toolStripMenuItem.ToolTipText = "When active (and if the check option is active in the Engine) the object will be placed ony with its reference Plane Z axis parallel to the World Z axis";
            Menu_AppendSeparator(menu);
        }

        private void ZLock_click(object sender, EventArgs e)
        {
            RecordUndoEvent("World Z lock");
            worldZLockComp = !GetValue("ZLockComp", false);
            SetValue("ZLockComp", worldZLockComp);
            UpdateMessage();
            ExpireSolution(true);
        }

        private void UpdateMessage()
        {
            Message = worldZLockComp ? "World Z Lock" : "";
        }

        public override bool Write(GH_IWriter writer)
        {
            // NOTE: the value in between "" is shared AMONG ALL COMPONENTS of a librbary!
            // ZLockComp is accessible (and modifyable) by other components!
            writer.SetBoolean("ZLockComp", worldZLockComp);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("ZLockComp", ref worldZLockComp);
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
                return Resources.Construct_compositeAO;
            }
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
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("634a73cf-7fcf-4013-8fee-7f3eb474a2c8"); }
        }
    }
}