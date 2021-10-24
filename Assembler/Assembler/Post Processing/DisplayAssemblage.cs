using Assembler.Properties;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper.GUI.Gradient;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
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
            pManager.AddBooleanParameter("Bypass", "B", "If true, just the untouched collision meshes will output", GH_ParamAccess.item, false);
            pManager[1].Optional = true; // bypass is optional

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
            //GH_Assemblage GH_AOa = new GH_Assemblage();
            Assemblage AOa = null;
            if (!DA.GetData(0, ref AOa)) return;
            //Assemblage AOa = GH_AOa.Value;

            bool byPass = false;
            DA.GetData(1, ref byPass);

            meshes = new Mesh[AOa.assemblyObjects.Count];
            edges = null;
            Color[] colors = new Color[AOa.assemblyObjects.Count];

            if (!byPass)
                SetPreviewData(AOa, displayMode, out colors);
            else
                meshes = AOa.assemblyObjects.AsParallel().Select(ao => ao.collisionMesh).ToArray();

            DA.SetDataList(0, meshes);
            DA.SetDataList(1, edges);
            DA.SetDataList(2, colors);
        }

        public void SetPreviewData(Assemblage AOa, string displayMode, out Color[] cols)
        {
            Mesh joined;
            Color[] colors = new Color[AOa.assemblyObjects.Count];

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
                    if (AOa.objectsDictionary.Count() <= Utilities.AOTypePalette.Length)
                        Parallel.For(0, colors.Length, i =>
                        {
                            colors[i] = Utilities.AOTypePalette[AOa.assemblyObjects[i].type];
                        });
                    // otherwise use random colors
                    else
                        Parallel.For(0, colors.Length, i =>
                        {
                            colors[i] = Color.FromKnownColor(Utilities.colorlist[AOa.assemblyObjects[i].type]);
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
                    foreach (AssemblyObject ao in AOa.assemblyObjects)
                        AOaBBox.Union(ao.collisionMesh.GetBoundingBox(false)); //ao.referencePlane.Origin
                    double minZ = AOaBBox.Min.Z;
                    double maxZ = AOaBBox.Max.Z;
                    double invZSpan = 1 / (maxZ - minZ);
                    Parallel.For(0, colors.Length, i =>
                    {
                        double t = (AOa.assemblyObjects[i].referencePlane.Origin.Z - minZ) * invZSpan;
                        colors[i] = Utilities.zHeightGradient.ColourAt(t);
                    });
                    break;
                case "AO Weights": // AssemblyObject Weight
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = GH_Gradient.GreyScale().ColourAt(AOa.assemblyObjects[i].weight);
                    });
                    break;
                case "Connectedness": // connectedness (n. of non-free handles/total handles)
                    Parallel.For(0, colors.Length, i =>
                    {
                        double connectedness = 1 - (AOa.assemblyObjects[i].handles.Where(h => h.occupancy == 0).Sum(x => 1) / (double)(AOa.assemblyObjects[i].handles.Length));
                        colors[i] = GH_Gradient.Traffic().ColourAt(connectedness);
                    });
                    break;
                case "Orientation": // Orientation
                    Parallel.For(0, colors.Length, i =>
                    {
                        Vector3d v = AOa.assemblyObjects[i].direction;
                        //normal-map like - see https://en.wikipedia.org/wiki/Normal_mapping
                        //colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)(v.Z <= 0 ? 128 : 128 + v.Z * 127));
                        // faux normal map
                        colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)((v.Z * .5 + .5) * 255));
                    });
                    break;
                case "Z Orientation": // Orientation
                    Parallel.For(0, colors.Length, i =>
                    {
                        Vector3d v = AOa.assemblyObjects[i].referencePlane.ZAxis;
                        //normal-map like - see https://en.wikipedia.org/wiki/Normal_mapping
                        //colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)(v.Z <= 0 ? 128 : 128 + v.Z * 127));
                        // faux normal map
                        colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)((v.Z * .5 + .5) * 255));
                    });
                    break;
                case "Local Density": // Local Density (volume of AO+connected neighbours individual BBoxes/volume of their union bounding box)
                    // compute density values over a fixed voulme reference, then remap results on 0-1 scale
                    // idea: scan AOcatalog, find largest component and use a scaled volume of that (like 1x to 3x its diagonal)
                    // best thing would be user choice of the reference volume, but I'm not keen on putting another input on this
                    double[] localDensities = new double[colors.Length];
                    Parallel.For(0, colors.Length, i =>
                    {
                        double localVolumes = AOa.assemblyObjects[i].collisionMesh.GetBoundingBox(false).Volume;
                        BoundingBox localBox;
                        //BoundingBox sumBox = AOa.assemblyObjects[i].collisionMesh.GetBoundingBox(false);
                        for (int j = 0; j < AOa.assemblyObjects[i].handles.Length; j++)
                        {
                            // if Handle is free or occluded go to next handle
                            if (AOa.assemblyObjects[i].handles[j].occupancy == 0 || AOa.assemblyObjects[i].handles[j].occupancy == -1) continue;
                            // else (if connected) compute local connected BBox volume
                            int connectedIndex = AOa.assemblyObjects[i].handles[j].neighbourObject;
                            localBox = AOa.assemblyObjects[connectedIndex].collisionMesh.GetBoundingBox(false);
                            //sumBox.Union(localBox);
                            localVolumes += localBox.Volume;
                        }
                        // include occluded neighbours
                        for (int j = 0; j < AOa.assemblyObjects[i].occludedNeighbours.Count; j++)
                        {
                            int occludedIndex = AOa.assemblyObjects[i].occludedNeighbours[j][0];
                            localBox = AOa.assemblyObjects[occludedIndex].collisionMesh.GetBoundingBox(false);
                            //sumBox.Union(localBox);
                            localVolumes += localBox.Volume;
                        }
                        // computed over a ~15m cube
                        localDensities[i] = Math.Min(localVolumes * 0.0002, 1);
                        colors[i] = Utilities.densityGradient.ColourAt(localDensities[i]);
                    });
                    // write a "normalize with limits" function
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

            // assign colors to meshes
            Parallel.For(0, AOa.assemblyObjects.Count, i =>
           {
               Mesh m = new Mesh();
               m.CopyFrom(AOa.assemblyObjects[i].collisionMesh);
               m.Unweld(0, true);
               m.VertexColors.Clear();
               for (int j = 0; j < m.Vertices.Count; j++)
                   m.VertexColors.Add(colors[i]);
               meshes[i] = m;

           });

            joined = new Mesh();
            joined.Append(meshes);
            edges = Utilities.GetSihouette(joined);
            cols = colors;
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
            ToolStripMenuItem toolStripMenuItem10 = Menu_AppendItem(menu, "Local Density", LocalDensity_Click, true, GetValue("OutputType", "Objects") == "Local Density");
            toolStripMenuItem10.ToolTipText = "Volume of each AssemblyObject and its connected or occluded neighbours' Bounding Boxes / their cumulative Bounding Box";
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
            // NOTE: the value in between "" is shared AMONG ALL COMPONENTS of a librbary!
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