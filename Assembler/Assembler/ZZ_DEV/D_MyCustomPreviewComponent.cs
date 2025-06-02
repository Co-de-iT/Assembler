using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Assembler
{
    // SOURCE: https://discourse.mcneel.com/t/grasshopper-raytraced-display-pipeline/141408/7
    public class D_MyCustomPreviewComponent : GH_CustomPreviewComponent
    {
        private List<GH_CustomPreviewItem> _items;
        private BoundingBox _boundingBox;
        public override BoundingBox ClippingBox => _boundingBox;
        public override bool IsBakeCapable => _items.Count > 0;
        /// <summary>
        /// Initializes a new instance of the D_MyCustomPreviewComponent class.
        /// </summary>
        public D_MyCustomPreviewComponent()
          : base()
        {
            Name = "D_MyCustomPreviewComponent";
            NickName = "D_MyCPV";
            Category = "Assembler";
            SubCategory = "Z_experimental";
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometries", "G", "Geometries to display", GH_ParamAccess.item);
            pManager.HideParameter(0);
            Param_OGLShader param_OGLShader = new Param_OGLShader();
            param_OGLShader.SetPersistentData(new GH_Material(Color.Plum));
            pManager.AddParameter(param_OGLShader, "Material", "M", "The material override", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("done", "D", "True when preview is done", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_GeometricGoo geometry = null;
            GH_Material material = null;
            if (DA.GetData(0, ref geometry) && DA.GetData(1, ref material) && geometry.IsValid)
            {
                if (!(geometry is IGH_PreviewData))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, geometry.TypeName + " does not support previews");
                }
                else if (material.Value != null)
                {
                    GH_CustomPreviewItem item = default(GH_CustomPreviewItem);

                    // this does not preview in any mode
                    //GH_Surface b = (GH_Surface)geometry;
                    //item.Geometry = b;

                    GH_Mesh gm = (GH_Mesh)geometry;
                    item.Geometry = gm;
                    //IGH_GeometricGoo gg = (IGH_GeometricGoo)gm;
                    //item.Geometry = new GH_Mesh(m);
                    //IGH_PreviewData pd = (IGH_PreviewData)gg;
                    //item.Geometry = (IGH_PreviewData)geometry;

                    item.Shader = material.Value;
                    item.Colour = material.Value.Diffuse;
                    item.Material = material;

                    _items.Add(item);
                    _boundingBox.Union(geometry.Boundingbox);
                }
            }

            DA.SetData(0, true);
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (this.Locked || _items.Count == 0)
                return;
            if (this.Attributes.Selected)
            {
                GH_PreviewMeshArgs args2 = new GH_PreviewMeshArgs(args.Viewport, args.Display, args.ShadeMaterial_Selected, args.MeshingParameters);
                foreach (GH_CustomPreviewItem item in _items)
                    item.Geometry.DrawViewportMeshes(args2);
                return;
            }
            foreach (GH_CustomPreviewItem item in _items)
            {
                GH_PreviewMeshArgs args2 = new GH_PreviewMeshArgs(args.Viewport, args.Display, item.Shader, args.MeshingParameters);
                item.Geometry.DrawViewportMeshes(args2);
            }
        }
        [Obsolete]
        public override void AppendRenderGeometry(GH_RenderArgs args)
        {
            if (_items != null && _items.Count != 0)
                foreach (GH_CustomPreviewItem item in _items)
                    item.PushToRenderPipeline(args);
        }

        protected override void BeforeSolveInstance()
        {
            _items = new List<GH_CustomPreviewItem>();
            _boundingBox = BoundingBox.Empty;
            base.BeforeSolveInstance();
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
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("070EF045-895B-439D-AAE8-11EEE9CBA7B4"); }
        }
    }
}