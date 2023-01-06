using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Utils;
using Assembler.Properties;
using System.Windows.Forms;
using GH_IO.Serialization;

namespace Assembler
{
    [Obsolete]
    public class L_ConstructAssemblyObject : GH_Component
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
        /// Initializes a new instance of the ConstructAssemblyObject_OLD class.
        /// </summary>
        public L_ConstructAssemblyObject()
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
            pManager.AddPlaneParameter("Reference Plane", "P", "The object's reference plane\nif unspecified, it is set to an XY Plane in the mesh volume centroid", GH_ParamAccess.item);
            pManager.AddVectorParameter("Direction", "D", "The object's direction vector\ndefault: X direction vector", GH_ParamAccess.item, Vector3d.XAxis);
            pManager.AddNumberParameter("Weight", "W", "The object's weight\n(optional)", GH_ParamAccess.item, 1.0);
            pManager.AddGenericParameter("Handles", "H", "The object's Handles", GH_ParamAccess.list);
            pManager[2].Optional = true;
            //pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            int type = 0;
            Mesh collisionMesh = new Mesh();
            Plane referencePlane = new Plane();
            Vector3d directionVector = Vector3d.Zero;
            double weight = 1.0;
            List<Handle> handlesList = new List<Handle>();

            // input data sanity checks
            if (!DA.GetData("Name", ref name)) return;
            if (!DA.GetData("Collision Mesh", ref collisionMesh)) return;

            // if collision mesh is null return
            if (collisionMesh == null || !collisionMesh.IsValid || !collisionMesh.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Collision Mesh is null, open or invalid");
                return;
            }

            // if no reference plane is set or if null set it to XY plane in volume centroid
            if (!DA.GetData("Reference Plane", ref referencePlane) || referencePlane == null)
            {
                Point3d centroid = VolumeMassProperties.Compute(collisionMesh).Centroid;
                referencePlane = new Plane(centroid, Vector3d.ZAxis);
            }

            DA.GetData("Direction", ref directionVector);
            // if Direction is null or zero return
            if (directionVector == null || directionVector == Vector3d.Zero)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Direction vector is zero or invalid");
                return;
            }

            DA.GetData("Weight", ref weight);
            if (!DA.GetDataList(5, handlesList)) return;
            // if Handles are empty return
            if (handlesList.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No Handles supplied");
                return;
            }

            // purge nulls from Handles list
            handlesList = Utilities.PurgeNullHandlesFromList(handlesList);

            // cast Handles to array
            Handle[] handlesArray = handlesList.ToArray();

            // construct the AssemblyObject                                                                                         v iWeight v Zlock
            AssemblyObject AO = new AssemblyObject(collisionMesh, handlesArray, referencePlane, directionVector, name, type, weight, -1, worldZLock);

            DA.SetData(0, new AssemblyObjectGoo(AO));

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
            get { return new Guid("3aa66311-1c61-4923-8cc7-675545b770bf"); }
        }
    }
}