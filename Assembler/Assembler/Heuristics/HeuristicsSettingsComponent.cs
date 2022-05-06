using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Special;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    public class HeuristicsSettingsComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the HeuristicsSettings class.
        /// </summary>
        public HeuristicsSettingsComponent()
          : base("Heuristics Settings", "HeuSet",
              "Collects Heuristics related settings",
              "Assembler", "Heuristics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // heuristics
            pManager.AddTextParameter("Heuristics Set", "HeS", "Heuristics Set\n" +
                "If you plan to use more than one Heuristics Set, use an Entwine component" +
                "\nThe branch used is decided by the current Heuristics parameter" +
                "\nor by a Field with iWeights when Field mode is activated",
                GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Current Heuristics", "cH", "index of current Heuristics Set branch to use from the Tree above", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Heuristics Mode", "HeM", "Heuristics Mode selector" +
                "\n0 - manual - via cH parameter" +
                "\n1 - Field driven - via Field iWeights",
                GH_ParamAccess.item, 0);
            // criteria selectors
            pManager.AddIntegerParameter("Receiver Selection Mode", "RsM",
                "Receiver selection criteria" +
                "\n0 - random" +
                "\n1 - scalar field nearest" +
                "\n2 - scalar field interpolated" +
                "\n3 - dense packing - minimum sum of connected AssemblyObjects' weights" +
                "\n" +
                "\nattach a Value List for automatic list generation",
                GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Sender (Rule) Selection Mode", "SsM",
                "Sender (Rule) selection criteria" +
                "\n0 - random" +
                "\n1 - scalar field nearest" +
                "\n2 - scalar field interpolated" +
                "\n3 - vector field nearest" +
                "\n4 - vector field interpolated" +
                "\n5 - vector field bidirectional nearest" +
                "\n6 - vector field bidirectional interpolated" +
                "\n7 - minimum local bounding box volume" +
                "\n8 - minimum local bounding box diagonal" +
                "\n9 - weighted random choice" + 
                "\n" +
                "\nattach a Value List for automatic list generation",
                GH_ParamAccess.item, 0);

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
            pManager.AddGenericParameter("Heuristics Settings", "HS", "Heuristics Settings for the Assemblage", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // heuristics
            GH_Structure<GH_String> HeuSets;
            if (!DA.GetDataTree(0, out HeuSets)) return;
            if (HeuSets.IsEmpty || HeuSets == null || HeuSets.Branches[0].Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one Heuristic string");
                return;
            }
            List<string> HeS = new List<string>();
            for (int i = 0; i < HeuSets.Branches.Count; i++)
            {
                HeS.Add(string.Join(",", HeuSets.Branches[i].Select(s => s.Value).ToList()));
            }

            int cH = 0;
            DA.GetData("Current Heuristics", ref cH);
            int HeM = 0;
            DA.GetData("Heuristics Mode", ref HeM);

            // criteria selectors
            int Rsm = 0, Ssm = 0;
            DA.GetData("Receiver Selection Mode", ref Rsm);
            DA.GetData("Sender (Rule) Selection Mode", ref Ssm);

            // __________________ autoList - Receiver Selection Mode __________________

            // variable for the list
            GH_ValueList vListRec;
            // tries to cast input as list
            try
            {
                vListRec = (GH_ValueList)Params.Input[3].Sources[0];

                if (!vListRec.NickName.Equals("Receiver selection mode"))
                {
                    vListRec.ClearData();
                    vListRec.ListItems.Clear();
                    vListRec.NickName = "Receiver selection mode";

                    vListRec.ListItems.Add(new GH_ValueListItem("Random", "0"));
                    vListRec.ListItems.Add(new GH_ValueListItem("Scalar Field nearest", "1"));
                    vListRec.ListItems.Add(new GH_ValueListItem("Scalar Field interpolated", "2"));
                    vListRec.ListItems.Add(new GH_ValueListItem("Dense Packing", "3"));

                    vListRec.ListItems[0].Value.CastTo(out Rsm);
                }
            }
            catch
            {
                // handles anything that is not a value list
            }

            // __________________ autoList - Sender (rule) Selection Mode __________________

            // variable for the list
            GH_ValueList vListSen;
            // tries to cast input as list
            try
            {

                vListSen = (GH_ValueList)Params.Input[4].Sources[0];

                if (!vListSen.NickName.Equals("Sender selection mode"))
                {
                    vListSen.ClearData();
                    vListSen.ListItems.Clear();
                    vListSen.NickName = "Sender selection mode";

                    vListSen.ListItems.Add(new GH_ValueListItem("Random", "0"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Scalar Field nearest", "1"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Scalar Field interpolated", "2"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Vector Field > nearest", "3"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Vector Field > interpolated", "4"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Vector Field <> nearest", "5"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Vector Field <> interpolated", "6"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Minimum local AABB volume", "7"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Minimum local AABB diagonal", "8"));
                    vListSen.ListItems.Add(new GH_ValueListItem("Weighted Random Choice", "9"));

                    vListSen.ListItems[0].Value.CastTo(out Ssm);
                }
            }
            catch
            {
                // handles anything that is not a value list
            }

            HeuristicsSettings HS = new HeuristicsSettings(HeS, cH, HeM, Rsm, Ssm);

            DA.SetData(0, HS);
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
                return Resources.Heuristics_Settings;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("2a4c7e4d-79e6-4086-980f-5e5eac15aad7"); }
        }
    }
}