using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assembler
{
    public class AssociateXData : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AssociateXData class.
        /// </summary>
        public AssociateXData()
          : base("Associate XData", "AO<>XD",
              "Associates XData to a list of AssemblyObjects\nMake sure the XData matches the corresponding AssemblyObject kinds present in the list",
              "Assembler", "Post Processing")
        {
            // this hides the component preview when placed onto the canvas
            // source: http://frasergreenroyd.com/how-to-stop-components-from-automatically-displaying-results-in-grasshopper/
            IGH_PreviewObject prevObj = (IGH_PreviewObject)this;
            prevObj.Hidden = true;
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObjects", "AO", "The tree of AssemblyObjects for XData association", GH_ParamAccess.tree);
            pManager.AddGenericParameter("XData", "XD", "The list of XData to associate", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("XData", "XD", "XData oriented in the AssemblyObjects", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<IGH_Goo> GH_AOs = new GH_Structure<IGH_Goo>();
            List<XData> xD = new List<XData>();

            if (!DA.GetDataTree(0, out GH_AOs)) return;
            if (!DA.GetDataList(1, xD)) return;

            List<AssemblyObject> AOs = new List<AssemblyObject>();
            List<GH_Path> AOPaths = new List<GH_Path>();
            DataTree<XData> XDataTree = new DataTree<XData>();
            AssemblyObjectGoo AOGoo;

            // convert to AssemblyObject List + Path List
            // Cannot do concurrent writing on a Data Tree in a Parallel Loop 
            for (int i = 0; i < GH_AOs.Branches.Count; i++)
                for (int j = 0; j < GH_AOs.Branches[i].Count; j++)
                {
                    AOGoo = GH_AOs.Branches[i][j] as AssemblyObjectGoo;
                    AOs.Add(AOGoo.Value);
                    // make extra paths for each AssemblyObject
                    AOPaths.Add(GH_AOs.Paths[i].AppendElement(j));
                }

            // ensure paths to avoid missing branches in case of no XData to associate
            for (int i = 0; i < GH_AOs.PathCount; i++)
                XDataTree.EnsurePath(GH_AOs.Paths[i].AppendElement(0));

            XData[][] assemblageXD = new XData[AOs.Count][];

            // compare all AssemblyObjects with the list of XData and orient any time a match is found
            Parallel.For(0, AOs.Count, i =>
            {
                // if AssemblyObject is null place null XData
                if (AOs[i] == null)
                    assemblageXD[i] = null;
                else
                {
                    List<XData> orientedXData = new List<XData>();
                    for (int j = 0; j < xD.Count; j++)
                    {
                        // if the object does not match XData's associated kind go on
                        if (!String.Equals(AOs[i].Name, xD[j].AOName)) continue;

                        XData xdC = new XData(xD[j]);
                        Transform orient = Transform.PlaneToPlane(xdC.ReferencePlane, AOs[i].ReferencePlane);
                        xdC.Transform(orient);
                        orientedXData.Add(xdC);
                    }
                    assemblageXD[i] = orientedXData.ToArray();

                    // Cleaner but slower alternative
                    // TODO: why is this slower? Investigate
                    //assemblageXD[i] = XDataUtils.AssociateXDataToAO(AOs[i], xD).ToArray();
                }
            });

            // the output is a Tree as there might be multiple XData associated with the same AssemblyObject type
            // The Branch Path is the AssemblyObject AInd
            for (int i = 0; i < assemblageXD.Length; i++)
                XDataTree.AddRange(assemblageXD[i], AOPaths[i]);

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