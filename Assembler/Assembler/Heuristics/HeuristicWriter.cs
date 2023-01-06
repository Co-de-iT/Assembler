using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Assembler
{
    public class HeuristicWriter : GH_Component
    {
        private bool noSelfObjectConnection;
        private bool noSelfHandleConnection;
        private bool crossTypeConnection;

        /// <summary>
        /// Initializes a new instance of the HeuristicWriter class.
        /// </summary>
        public HeuristicWriter()
          : base("Heuristics Writer", "HeuWri",
              "Generates Heuristics Set for an AssemblyObjects Set",
              "Assembler", "Heuristics")
        {
            noSelfObjectConnection = GetValue("noSO", false);
            noSelfHandleConnection = GetValue("noSH", false);
            crossTypeConnection = GetValue("xHT", false);
            UpdateMessage();
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddTextParameter("Handle Compatibility", "Hc", "Set of Handle type possible relations\nex. 1<2 means type 1 Handles can receive type 2 Handles\nif this input is used, Self-Handle and Cross-Type options will be ignored", GH_ParamAccess.list);

            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Heuristics Set", "HeS", "Heuristics Set", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOs = new List<AssemblyObject>();
            // sanity check on inputs
            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;

            if (GH_AOs == null || GH_AOs.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one valid AssemblyObject");

            AOs = GH_AOs.Where(a=>a!=null).Select(ao => ao.Value).ToList();

            if (AOs.Count == 0)
                 AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one valid AssemblyObject");

            List<string> Hc = new List<string>();

            if (!DA.GetDataList(1, Hc)) Hc = null;

            List<string> Heuristics;

            if (Hc == null || Hc.Count == 0)
            {
                Heuristics = WriteRules(AOs.ToArray(), noSelfObjectConnection, noSelfHandleConnection, crossTypeConnection);
            }
            else
            {
                DataTree<int> HandlesCompatibilityTree = BuildHandleCompatibilityTree(Hc);
                Heuristics = WriteRules(AOs.ToArray(), HandlesCompatibilityTree, noSelfObjectConnection);
            }

            DA.SetDataList(0, Heuristics);

        }

        private DataTree<int> BuildHandleCompatibilityTree(List<string> Hc)
        {
            HashSet<int> pool = new HashSet<int>();
            DataTree<int> HCorr = new DataTree<int>();

            string[][] lines = Hc.Select(s => s.Split('<')).ToArray();

            // fill HashSet
            for (int i = 0; i < lines.Length; i++)
            {
                pool.Add(Convert.ToInt32(lines[i][0]));
            }

            List<int> types = pool.ToList();

            // ensure paths and add self-type
            for (int i = 0; i < types.Count; i++)
                HCorr.EnsurePath(new GH_Path(types[i]));

            int rI, sI;

            for (int i = 0; i < lines.Length; i++)
            {
                rI = Convert.ToInt32(lines[i][0]);
                sI = Convert.ToInt32(lines[i][1]);
                if (!HCorr.Branch(new GH_Path(rI)).Contains(sI))
                    HCorr.Add(sI, new GH_Path(rI));
            }

            return HCorr;
        }

        private bool IsCompatible(int typeR, int typeS, DataTree<int> HandCorr)
        {
            if (!HandCorr.PathExists(new GH_Path(typeR))) return false;

            return (HandCorr.Branch(new GH_Path(typeR)).Contains(typeS));
        }

        private List<string> WriteRules(AssemblyObject[] components, bool noSelfObject, bool noSelfHandle, bool crossType)
        {
            List<string> hsList = new List<string>();
            string hs;

            /*
             rOi - receiver object index
             rHi - receiver handle index
             sOi - sender object index
             sHi - sender handle index
             rRi - receiver rotation index
             */
            // scan all components as potential receivers
            for (int rOi = 0; rOi < components.Length; rOi++)
            {
                // scan all handlesTree
                for (int rHi = 0; rHi < components[rOi].Handles.Length; rHi++)
                {
                    // scan all components as potential senders
                    for (int sOi = 0; sOi < components.Length; sOi++)
                    {
                        // exclude self if selfObject is False
                        if (rOi == sOi && noSelfObject) continue;
                        // scan potential sender handlesTree
                        for (int sHi = 0; sHi < components[sOi].Handles.Length; sHi++)
                        {
                            // exclude handlesTree attaching to self if selfHType is False
                            if (rOi == sOi && rHi == sHi && noSelfHandle) continue;
                            // if handle types match or cross type is True generate heuristic
                            if (components[rOi].Handles[rHi].Type == components[sOi].Handles[sHi].Type || crossType)
                            {
                                // consider all receiver rotations
                                for (int rRi = 0; rRi < components[rOi].Handles[rHi].Receivers.Length; rRi++)
                                {
                                    hs = WriteRuleString(components, rOi, rHi, sOi, sHi, rRi);
                                    hsList.Add(hs);
                                }
                            }
                        }
                    }
                }
            }

            return hsList;
        }

        private List<string> WriteRules(AssemblyObject[] components, DataTree<int> HandCorr, bool noSelfObject)
        {
            List<string> hsList = new List<string>();
            string hs;

            /*
             rOi - receiver object index
             rHi - receiver handle index
             sOi - sender object index
             sHi - sender handle index
             rRi - receiver rotation index
             */
            // scan all components as potential receivers
            for (int rOi = 0; rOi < components.Length; rOi++)
            {
                // scan all handlesTree
                for (int rHi = 0; rHi < components[rOi].Handles.Length; rHi++)
                {
                    // scan all components as potential senders
                    for (int sOi = 0; sOi < components.Length; sOi++)
                    {
                        // exclude self if selfObject is False
                        if (rOi == sOi && noSelfObject) continue;
                        // scan potential sender handlesTree
                        for (int sHi = 0; sHi < components[sOi].Handles.Length; sHi++)
                        {
                            // if handle types are compatible according to the Compatibility rules
                            if (IsCompatible(components[rOi].Handles[rHi].Type, components[sOi].Handles[sHi].Type, HandCorr))
                            {
                                // consider all receiver rotations
                                for (int rRi = 0; rRi < components[rOi].Handles[rHi].Receivers.Length; rRi++)
                                {
                                    hs = WriteRuleString(components, rOi, rHi, sOi, sHi, rRi);
                                    hsList.Add(hs);
                                }
                            }
                        }
                    }
                }
            }

            return hsList;
        }

        private static string WriteRuleString(AssemblyObject[] components, int rOi, int rHi, int sOi, int sHi, int rRi)
        {
            return $"{components[rOi].Name}|{rHi}={components[rOi].Handles[rHi].Rotations[rRi]}<{components[sOi].Name}|{sHi}%1";
        }



        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "no self-AO", NoSO_Click, true, noSelfObjectConnection);
            toolStripMenuItem.ToolTipText = "Prevents an AssemblyObject from connecting with itself";
            ToolStripMenuItem toolStripMenuItem2 = Menu_AppendItem(menu, "no self-Handle", NoSH_Click, true, noSelfHandleConnection);
            toolStripMenuItem2.ToolTipText = "Prevents a Handle from connecting with itself";
            ToolStripMenuItem toolStripMenuItem3 = Menu_AppendItem(menu, "allow cross-Handle type", HandleXtype_Click, true, crossTypeConnection);
            toolStripMenuItem3.ToolTipText = "Allow Handle cross-type connection (ignore Handle types)";
            Menu_AppendSeparator(menu);
        }

        private void NoSO_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("no self-AO");
            noSelfObjectConnection = !GetValue("noSO", false);
            SetValue("noSO", noSelfObjectConnection);
            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        private void NoSH_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("no self-Handle");
            noSelfHandleConnection = !GetValue("noSH", false);
            SetValue("noSH", noSelfHandleConnection);
            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        private void HandleXtype_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("allow cross-Handle type");
            crossTypeConnection = !GetValue("xHT", false);
            SetValue("xHT", crossTypeConnection);
            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("noSO", noSelfObjectConnection);
            writer.SetBoolean("noSH", noSelfHandleConnection);
            writer.SetBoolean("xHT", crossTypeConnection);

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("noSO", ref noSelfObjectConnection);
            reader.TryGetBoolean("noSH", ref noSelfHandleConnection);
            reader.TryGetBoolean("xHT", ref crossTypeConnection);
            UpdateMessage();
            return base.Read(reader);
        }

        private void UpdateMessage()
        {
            string message = "";
            if (noSelfObjectConnection)
                message += "-sAO\n";
            if (noSelfHandleConnection)
                message += "-sH\n";
            if (crossTypeConnection)
                message += "X Ht";

            Message = message;
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
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
                return Resources.Heuristics_Writer;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("95c79d28-a359-4b1d-8a20-ee4aa7c6f91f"); }
        }
    }
}