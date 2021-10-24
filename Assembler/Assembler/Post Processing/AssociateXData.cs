using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper;
using AssemblerLib;
using Assembler.Properties;
using Assembler.Utils;
using System.Linq;
using System.ComponentModel;

namespace Assembler
{
    public class AssociateXData : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AssociateXData class.
        /// </summary>
        public AssociateXData()
          : base("Associate XData", "AOXD",
              "Associates XData to a list of AssemblyObjects\nMake sure the XData matches the corresponding AssemblyObject types present in the list",
              "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObjects", "AO", "The list of AssemblyObjects for XData association", GH_ParamAccess.list);
            pManager.AddGenericParameter("XData", "XD", "The list of XData to associate", GH_ParamAccess.list);
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
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOs = new List<AssemblyObject>();
            List<XData> xD = new List<XData>();

            if (!DA.GetDataList(0, GH_AOs)) return;
            if (!DA.GetDataList(1, xD)) return;

            //if (GH_AOs == null)
            //{
            //    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No AssemblyObject provided");
            //    return;
            //}
            //if (xD == null)
            //{
            //    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No XData provided");
            //    return;
            //}

            AOs = GH_AOs.Select(ao => ao.Value).ToList();

            XData[][] assemblageXD = new XData[AOs.Count][];

            // compare all AssemblyObjects with the list of XData ad orient any time a match is found
            Parallel.For(0, AOs.Count, i=>
            //for (int i = 0; i < AOs.Count; i++)
            {
                // if AssemblyObject is null assign a null and continue
                if (AOs[i] == null)
                {
                    assemblageXD[i] = new XData[0];
                    //continue;
                }
                else
                {
                    List<XData> orientedXData = new List<XData>();

                    for (int j = 0; j < xD.Count; j++)
                    {

                        // if the object does not match XData associated type go on
                        if (!String.Equals(AOs[i].name, xD[j].AOName)) continue;

                        XData xdC = new XData(xD[j]);
                        Transform orient = Transform.PlaneToPlane(xdC.refPlane, AOs[i].referencePlane);
                        xdC.Transform(orient);
                        orientedXData.Add(xdC);

                    }
                    assemblageXD[i] = orientedXData.ToArray();
                }
            });

            // the output is a Tree as there might be multiple XData associated with the same AssemblyObject type
            DataTree<XData> XDataTree = Utilities.ToDataTree(assemblageXD);

            DA.SetDataTree(0, XDataTree);
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
                return Resources.Associate_XData;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8f52411b-e335-4467-9d05-59b14f301e3f"); }
        }
    }
}