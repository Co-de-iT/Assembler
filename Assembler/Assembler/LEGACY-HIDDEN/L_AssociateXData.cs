using Assembler.Properties;
using AssemblerLib;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Assembler
{
    [Obsolete]
    public class L_AssociateXData : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AssociateXData class.
        /// </summary>
        public L_AssociateXData()
          : base("Associate XData - LEGACY", "AOaXD",
              "Associates XData to an Assemblage\nLEGACY version",
              "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            //pManager.AddGenericParameter("Assemblage", "AOa", "The list of AssemblyObjects in the Assemblage", GH_ParamAccess.list);
            pManager.AddGenericParameter("XData", "XD", "The list of XData to associate to the Assemblage", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("XData", "XD", "XData oriented in the Assemblage", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "This is an obsolete component - replace it with a new version from the Ribbon");

            List<AssemblyObject> AO = new List<AssemblyObject>();
            Assemblage AOa = null;
            List<XData> xD = new List<XData>();
            if (!DA.GetData(0, ref AOa)) return;
            //if (!DA.GetDataList(0, AO)) return;
            if (!DA.GetDataList(1, xD)) return;

            DataTree<XData> XDataTree = new DataTree<XData>();

            AO = AOa.AssemblyObjects.AllData();

            //XData[][] assemblageXD = new XData[AO.Count][];

            XData xdC;
            Transform orient;
            List<XData> orientedXData;
            // compare all AssemblyObjects with the list of XData ad orient any time a match is found
            for(int i=0; i< AO.Count; i++)
            {
                orientedXData = new List<XData>();
                for (int j = 0; j < xD.Count; j++)
                {
                    // if the object does not match XData associated type go on
                    //if (AOa[i].type != xD[j].objectType) continue;
                    if (!String.Equals(AO[i].Name, xD[j].AOName)) continue;

                    xdC = new XData(xD[j]);
                    orient = Transform.PlaneToPlane(xdC.ReferencePlane, AO[i].ReferencePlane);
                    xdC.Transform(orient);
                    orientedXData.Add(xdC);
                    
                }
                //assemblageXD[i] = orientedXData.ToArray();
                XDataTree.AddRange(orientedXData, new GH_Path(AO[i].AInd));
            }

            // the output is a Tree as there might be multiple XData associated with the same AssemblyObject type
            //XDataTree = Utilities.ToDataTree(assemblageXD);

            DA.SetDataTree(0, XDataTree);
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
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Resources.Associate_XData_OLD;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("e8e6fcb0-cfc3-43cb-90ee-1c5ecfc6a884"); }
        }
    }
}