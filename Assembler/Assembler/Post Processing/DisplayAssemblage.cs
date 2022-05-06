﻿using Assembler.Properties;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper.GUI.Gradient;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Assembler
{
    public class DisplayAssemblage : GH_Component
    {

        private string displayMode;

        private Mesh[] meshes;
        private GH_Line[] edges;
        private Color[] colors;

        /// <summary>
        /// Initializes a new instance of the DisplayAssemblage class.
        /// </summary>
        public DisplayAssemblage()
          : base("Display Assemblage", "AOaDisp",
              "Display Assemblage with a set of modes",
              "Assembler", "Post Processing")
        {
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Colors only", "C", "If true, just the Colors will output", GH_ParamAccess.item, false);
            pManager[1].Optional = true; // Colors only is optional

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Colored Collision Meshes", "CM", "The Collision Meshes, colored by selected mode", GH_ParamAccess.list);
            pManager.AddLineParameter("Edges", "E", "Collision Mesh Edges for display", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "The colors by selected mode", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData(0, ref AOa)) return;

            bool colOnly = false;
            DA.GetData(1, ref colOnly);

            meshes = colOnly? null : new Mesh[AOa.assemblyObjects.BranchCount];
            edges = null;
            colors = new Color[AOa.assemblyObjects.BranchCount];

            //if (!byPass)
            SetPreviewData(AOa, displayMode, colOnly);
            //else
            //    meshes = AOa.assemblyObjects.AsParallel().Select(ao => ao.collisionMesh).ToArray();

            DA.SetDataList(0, meshes);
            DA.SetDataList(1, edges);
            DA.SetDataList(2, colors);
        }

        public void SetPreviewData(Assemblage AOa, string displayMode, bool colOnly)
        {
            Mesh joined;
            //Color[] colors = new Color[AOa.assemblyObjects.BranchCount];

            switch (displayMode)
            {
                case "Objects": // just objects
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = Color.White;
                    });
                    break;
                case "AO Types": // by type
                    // use type palette if number of types is smaller than the number of colors in the palette
                    if (AOa.AOSet.Length <= Utilities.AOTypePalette.Length)
                        Parallel.For(0, colors.Length, i =>
                        {
                            colors[i] = Utilities.AOTypePalette[AOa.assemblyObjects.Branches[i][0].type];
                        });
                    // otherwise use random colors
                    else
                        Parallel.For(0, colors.Length, i =>
                        {
                            colors[i] = Color.FromKnownColor(Utilities.colorlist[AOa.assemblyObjects.Branches[i][0].type]);
                        });
                    break;
                case "Occupancy": // occupancy
                    // saturated 112,117,57 (dark green) - available white - unreachable 194,199,137 (light green)
                    int[] availableObjects = AOa.ExtractAvailableObjects().Select(i => i.Value).ToArray();
                    int[] unreachableObjects = AOa.ExtractUnreachableObjects().Select(i => i.Value).ToArray();

                    Parallel.For(0, colors.Length, i =>
                    {
                        if (availableObjects.Contains(i)) colors[i] = Color.White;
                        else if (unreachableObjects.Contains(i)) colors[i] = Color.FromArgb(194, 199, 137);
                        else
                            colors[i] = Color.FromArgb(112, 117, 57);
                    });

                    break;
                case "Sequence": // sequence
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = Utilities.historyGradient.ColourAt(i / ((double)colors.Length));
                    });
                    break;
                case "Z Value": // zHeight
                    BoundingBox AOaBBox = BoundingBox.Empty;
                    for(int i=0; i< AOa.assemblyObjects.BranchCount; i++)
                        AOaBBox.Union(AOa.assemblyObjects.Branches[i][0].collisionMesh.GetBoundingBox(false)); //ao.referencePlane.Origin
                    //foreach (AssemblyObject ao in AOa.assemblyObjects.AllData())
                    //    AOaBBox.Union(ao.collisionMesh.GetBoundingBox(false)); //ao.referencePlane.Origin
                    double minZ = AOaBBox.Min.Z;
                    double maxZ = AOaBBox.Max.Z;
                    double invZSpan = 1 / (maxZ - minZ);
                    Parallel.For(0, colors.Length, i =>
                    {
                        double t = (AOa.assemblyObjects.Branches[i][0].referencePlane.Origin.Z - minZ) * invZSpan;
                        colors[i] = Utilities.zHeightGradient.ColourAt(t);
                    });
                    break;
                case "AO Weights": // AssemblyObject Weight
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = GH_Gradient.GreyScale().ColourAt(AOa.assemblyObjects.Branches[i][0].weight);
                    });
                    break;
                case "Connectedness": // connectedness (n. of non-free handles/total handles)
                    Parallel.For(0, colors.Length, i =>
                    {
                        double connectedness = 1 - (AOa.assemblyObjects.Branches[i][0].handles.Where(h => h.occupancy == 0).Sum(x => 1) / (double)(AOa.assemblyObjects.Branches[i][0].handles.Length));
                        colors[i] = GH_Gradient.Traffic().ColourAt(connectedness);
                    });
                    break;
                case "Orientation": // Orientation
                    Parallel.For(0, colors.Length, i =>
                    {
                        Vector3d v = AOa.assemblyObjects.Branches[i][0].direction;
                        //normal-map like - see https://en.wikipedia.org/wiki/Normal_mapping
                        //colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)(v.Z <= 0 ? 128 : 128 + v.Z * 127));
                        // faux normal map
                        colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)((v.Z * .5 + .5) * 255));
                    });
                    break;
                case "Z Orientation": // Orientation
                    Parallel.For(0, colors.Length, i =>
                    {
                        Vector3d v = AOa.assemblyObjects.Branches[i][0].referencePlane.ZAxis;
                        //normal-map like - see https://en.wikipedia.org/wiki/Normal_mapping
                        //colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)(v.Z <= 0 ? 128 : 128 + v.Z * 127));
                        // faux normal map
                        colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)((v.Z * .5 + .5) * 255));
                    });
                    break;
                case "Receiver Values":
                    double[] rValues = AOa.assemblyObjects.AllData().Select(ao => ao.receiverValue).ToArray();
                    double min = rValues.Min();
                    double max = rValues.Max();
                    // avoid division by 0
                    double factor = min == max ? 0 : 1 / (max - min);
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = Utilities.receiverValuesGradient.ColourAt((AOa.assemblyObjects.Branches[i][0].receiverValue - min) * factor);
                    });
                    break;
                case "Local Density": // Local Density
                    // volume of AO+connected/occluding neighbours individual Collision meshes/volume of twice the largest object in the AOset
                    // compute density values over a fixed voulme reference, then remap results on 0-1 scale
                    // best thing would be user choice of the reference volume, but I'm not keen on putting another input on this

                    // Find volumes for AOset Collision Meshes and keep largest
                    double[] AOsetCollisionVolumes = new double[AOa.AOSet.Length];
                    double AOCollisionMaxVolume = 0;
                    for (int i = 0; i < AOa.AOSet.Length; i++)
                    {
                        AOsetCollisionVolumes[i] = AOa.AOSet[i].collisionMesh.Volume();
                        if (AOsetCollisionVolumes[i] > AOCollisionMaxVolume)
                            AOCollisionMaxVolume = AOsetCollisionVolumes[i];
                    }

                    double referenceVolume = 1 / (AOCollisionMaxVolume * 8);// equals to a box twice the scale
                    double[] localDensities = new double[colors.Length];
                    Parallel.For(0, colors.Length, i =>
                    {
                        // get collision volume for the present object
                        double localVolumes = AOsetCollisionVolumes[AOa.assemblyObjects.Branches[i][0].type];

                        // get collision volume for connected or occluding neighbours
                        for (int j = 0; j < AOa.assemblyObjects.Branches[i][0].handles.Length; j++)
                        {
                            // if Handle is free (consider also occluding objects) go to next handle
                            if (AOa.assemblyObjects.Branches[i][0].handles[j].occupancy == 0) continue;

                            // else (if connected or occluded) add other object collision volume
                            int connectedIndex = AOa.assemblyObjects.Branches[i][0].handles[j].neighbourObject;
                            int connectedType = AOa.assemblyObjects[new GH_Path(connectedIndex),0].type;

                            localVolumes += AOsetCollisionVolumes[connectedType];
                        }

                        localDensities[i] = localVolumes * referenceVolume;
                        colors[i] = Utilities.densityGradient.ColourAt(localDensities[i]);
                    });
                    // write a "normalize with limits" function?
                    //double[] normalizedDensities = Utilities.NormalizeRange(localDensities);
                    //Parallel.For(0, colors.Length, i =>
                    //{
                    //    colors[i] = Utilities.densityGradient.ColourAt(normalizedDensities[i]);
                    //});
                    break;
                // possible other display modes:

                // Valence (n. of handles)?
                default:
                    goto case "Objects";
            }

            if (colOnly) return;

            // assign colors to meshes
            Parallel.For(0, AOa.assemblyObjects.BranchCount, i =>
           {
               Mesh m = new Mesh();
               m.CopyFrom(AOa.assemblyObjects.Branches[i][0].collisionMesh);
               m.Unweld(0, true);
               m.VertexColors.Clear();
               for (int j = 0; j < m.Vertices.Count; j++)
                   m.VertexColors.Add(colors[i]);
               meshes[i] = m;

           });

            joined = new Mesh();
            joined.Append(meshes);
            edges = Utilities.GetSihouette(joined);
            //cols = colors;
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Objects", Objects_Click, true, GetValue("OutputType", "Objects") == "Objects");
            toolStripMenuItem.ToolTipText = "Just white. Approved by The Thin White Duke";
            ToolStripMenuItem toolStripMenuItem2 = Menu_AppendItem(menu, "AO Types", Type_Click, true, GetValue("OutputType", "Objects") == "AO Types");
            toolStripMenuItem2.ToolTipText = "Colors assigned by AssemblyObject type";
            ToolStripMenuItem toolStripMenuItem3 = Menu_AppendItem(menu, "Occupancy", Occupancy_Click, true, GetValue("OutputType", "Objects") == "Occupancy");
            toolStripMenuItem3.ToolTipText = "white - available, light green - unreachable, dark green - saturated";
            ToolStripMenuItem toolStripMenuItem4 = Menu_AppendItem(menu, "Sequence", Sequence_Click, true, GetValue("OutputType", "Objects") == "Sequence");
            toolStripMenuItem4.ToolTipText = "black - older < blue shades > white - younger";
            ToolStripMenuItem toolStripMenuItem5 = Menu_AppendItem(menu, "Z Value", ZValue_Click, true, GetValue("OutputType", "Objects") == "Z Value");
            toolStripMenuItem5.ToolTipText = "Pink gradient by World Z value of Assemblyobject reference plane origin";
            ToolStripMenuItem toolStripMenuItem6 = Menu_AppendItem(menu, "AO Weights", OWeights_Click, true, GetValue("OutputType", "Objects") == "AO Weights");
            toolStripMenuItem6.ToolTipText = "AssemblyObjects weights in grayscale gradient";
            ToolStripMenuItem toolStripMenuItem7 = Menu_AppendItem(menu, "Connectedness", Connectedness_Click, true, GetValue("OutputType", "Objects") == "Connectedness");
            toolStripMenuItem7.ToolTipText = "Percentage of free handles green (free) to red (fully occupied) gradient";
            ToolStripMenuItem toolStripMenuItem8 = Menu_AppendItem(menu, "Orientation", Orientation_Click, true, GetValue("OutputType", "Objects") == "Orientation");
            toolStripMenuItem8.ToolTipText = "Faux-Normal-style color map for objects direction vector";
            ToolStripMenuItem toolStripMenuItem9 = Menu_AppendItem(menu, "Z Orientation", ZOrientation_Click, true, GetValue("OutputType", "Objects") == "Z Orientation");
            toolStripMenuItem9.ToolTipText = "Faux-Normal-style color map for objects reference plane Z vector";
            ToolStripMenuItem toolStripMenuItem10 = Menu_AppendItem(menu, "Receiver Values", ReceiverValues_Click, true, GetValue("OutputType", "Objects") == "Receiver Values");
            toolStripMenuItem10.ToolTipText = "Receiver value of each AssemblyObject - white: minimum, dark red: maximum";
            ToolStripMenuItem toolStripMenuItem11 = Menu_AppendItem(menu, "Local Density", LocalDensity_Click, true, GetValue("OutputType", "Objects") == "Local Density");
            toolStripMenuItem11.ToolTipText = "Volume of each AssemblyObject and its connected or occluded neighbours' Bounding Boxes / their cumulative Bounding Box";
            Menu_AppendSeparator(menu);
        }

        private void Objects_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Objects");
            SetValue("OutputType", "Objects");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void Type_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("AO Types");
            SetValue("OutputType", "AO Types");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void Occupancy_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Occupancy");
            SetValue("OutputType", "Occupancy");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void Sequence_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Sequence");
            SetValue("OutputType", "Sequence");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void ZValue_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Z Value");
            SetValue("OutputType", "Z Value");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void OWeights_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("AO Weights");
            SetValue("OutputType", "AO Weights");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void Connectedness_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Connectedness");
            SetValue("OutputType", "Connectedness");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void Orientation_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Orientation");
            SetValue("OutputType", "Orientation");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void ZOrientation_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Z Orientation");
            SetValue("OutputType", "Z Orientation");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }

        private void ReceiverValues_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Receiver Values");
            SetValue("OutputType", "Receiver Values");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }

        private void LocalDensity_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Local Density");
            SetValue("OutputType", "Local Density");
            displayMode = GetValue("OutputType", "Objects");
            UpdateMessage();
            ExpireSolution(true);
        }

        public override bool Write(GH_IWriter writer)
        {
            // NOTE: the value in between "" is shared AMONG ALL COMPONENTS of a library!
            // OutputType is accessible (and modifyable) by other components!
            writer.SetString("OutputType", displayMode);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {

            reader.TryGetString("OutputType", ref displayMode);
            UpdateMessage();
            return base.Read(reader);
        }

        private void UpdateMessage()
        {
            Message = displayMode;
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
                return Resources.Display_Assemblage;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("09104f07-118f-46e5-8bb5-d101105897bf"); }
        }
    }
}