﻿using Assembler.Properties;
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
            Params.ParameterSourcesChanged += new GH_ComponentParamServer.ParameterSourcesChangedEventHandler(ParamSourceChanged);
        }

        // SOURCE: https://discourse.mcneel.com/t/automatic-update-of-valuelist-only-when-connected/152879/6?u=ale2x72
        // works much better as it does not clog the solver with exceptions if a list of numercal values is connected
        private void ParamSourceChanged(object sender, GH_ParamServerEventArgs e)
        {
            if ((e.ParameterSide == GH_ParameterSide.Input) && (e.ParameterIndex == 3))
            {
                foreach (IGH_Param source in e.Parameter.Sources)
                {
                    if (source is Grasshopper.Kernel.Special.GH_ValueList)
                    {
                        Grasshopper.Kernel.Special.GH_ValueList vListReceiver = source as Grasshopper.Kernel.Special.GH_ValueList;

                        if (!vListReceiver.NickName.Equals("Receiver selection mode"))
                        {
                            vListReceiver.ClearData();
                            vListReceiver.ListItems.Clear();
                            vListReceiver.NickName = "Receiver selection mode";

                            vListReceiver.ListItems.Add(new GH_ValueListItem("Random", "0"));
                            vListReceiver.ListItems.Add(new GH_ValueListItem("Scalar Field nearest", "1"));
                            vListReceiver.ListItems.Add(new GH_ValueListItem("Scalar Field interpolated", "2"));
                            vListReceiver.ListItems.Add(new GH_ValueListItem("Dense Packing", "3"));

                            vListReceiver.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.DropDown; // change this for a different mode (DropDown is the default)
                            vListReceiver.ExpireSolution(true);
                        }
                    }
                }
            }
            if ((e.ParameterSide == GH_ParameterSide.Input) && (e.ParameterIndex == 4))
            {
                foreach (IGH_Param source in e.Parameter.Sources)
                {
                    if (source is Grasshopper.Kernel.Special.GH_ValueList)
                    {
                        Grasshopper.Kernel.Special.GH_ValueList vListSender = source as Grasshopper.Kernel.Special.GH_ValueList;

                        if (!vListSender.NickName.Equals("Sender selection mode"))
                        {
                            vListSender.ClearData();
                            vListSender.ListItems.Clear();
                            vListSender.NickName = "Sender selection mode";

                            vListSender.ListItems.Add(new GH_ValueListItem("Random", "0"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Scalar Field nearest", "1"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Scalar Field interpolated", "2"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Vector Field > nearest", "3"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Vector Field > interpolated", "4"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Vector Field <> nearest", "5"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Vector Field <> interpolated", "6"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Minimum local AABB volume", "7"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Minimum local AABB diagonal", "8"));
                            vListSender.ListItems.Add(new GH_ValueListItem("Weighted Random Choice", "9"));

                            vListSender.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.DropDown; // change this for a different mode (DropDown is the default)
                            vListSender.ExpireSolution(true);
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
            GH_Structure<GH_String> HeuristicsGHStruct;
            if (!DA.GetDataTree(0, out HeuristicsGHStruct)) return;
            if (HeuristicsGHStruct.IsEmpty || HeuristicsGHStruct == null || HeuristicsGHStruct.Branches[0].Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one Heuristic string");
                return;
            }
            List<string> HeuristicsStrings = new List<string>();
            for (int i = 0; i < HeuristicsGHStruct.Branches.Count; i++)
            {
                HeuristicsStrings.Add(string.Join(",", HeuristicsGHStruct.Branches[i].Select(s => s.Value).ToList()));
            }

            int currentHeuristics = 0;
            DA.GetData("Current Heuristics", ref currentHeuristics);
            int HeuristicsMode = 0;
            DA.GetData("Heuristics Mode", ref HeuristicsMode);

            // criteria selectors
            int ReceiverSelectionMode = 0, SenderSelectionMode = 0;
            DA.GetData("Receiver Selection Mode", ref ReceiverSelectionMode);
            DA.GetData("Sender (Rule) Selection Mode", ref SenderSelectionMode);

            HeuristicsSettings HS = new HeuristicsSettings(HeuristicsStrings, currentHeuristics, HeuristicsMode, ReceiverSelectionMode, SenderSelectionMode);

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