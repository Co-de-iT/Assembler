using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Assembler
{
	/*
	 Here I was trying to get a "rendered" style preview (like the Custom Preview component)
	but it seems to be too hard a task... (requires installing a Rhino custom made plugin to tap
	into the render pipeline)
	 */

  //  public class D_AssCustomPreview : GH_Component
  //  {
		
  //      private List<GH_CustomPreviewItem> _items;

  //      private BoundingBox _boundingBox;
  //      /// <summary>
  //      /// Initializes a new instance of the AssCustomPreview class.
  //      /// </summary>
  //      public D_AssCustomPreview()
  //        : base("AssCustomPreview", "ASS_Cprev",
  //            "Test for Custom Preview Replica",
  //            "Assembler", "ZZ_Test")
  //      {
		//	ViewportFilter = string.Empty;
		//	IncludeInRender = true;
		//}

		///// <summary>
		///// Iterate over all the custom display items in this component.
		///// </summary>
		///// <returns></returns>
		//public IEnumerable<GH_CustomPreviewItem> DisplayItems => _items;

		///// <summary>
		///// Gets or sets whether the custom geometry inside this component ought to be included in a render pipeline.
		///// </summary>
		//public bool IncludeInRender
		//{
		//	get;
		//	set;
		//}

		//public override BoundingBox ClippingBox => _boundingBox;

		///// <summary>
		///// Gets or sets a string filter (supporting wildcards) that limits whether geometry is drawn in a specific viewport.
		///// </summary>
		//public string ViewportFilter
		//{
		//	get;
		//	set;
		//}

		//public override bool IsBakeCapable => _items.Count > 0;

		//protected override void RegisterInputParams(GH_InputParamManager pManager)
		//{
		//	pManager.AddGeometryParameter("Geometry", "G", "Geometry to preview", GH_ParamAccess.item);
		//	pManager.HideParameter(0);
		//	Param_OGLShader param_OGLShader = new Param_OGLShader();
		//	param_OGLShader.SetPersistentData(new GH_Material(Color.Plum));
		//	pManager.AddParameter(param_OGLShader, "Material", "M", "The material override", GH_ParamAccess.item);
		//}

		//protected override void RegisterOutputParams(GH_OutputParamManager pManager)
		//{
		//}

		//public override void ClearData()
		//{
		//	base.ClearData();
		//	_items = null;
		//}

		///// <summary>
		///// Override this method if you want to be called 
		///// before the first call to SolveInstance.
		///// </summary>
		//protected override void BeforeSolveInstance()
		//{
		//	_items = new List<GH_CustomPreviewItem>();
		//	_boundingBox = BoundingBox.Empty;
		//}

		//protected override void SolveInstance(IGH_DataAccess DA)
		//{
		//	IGH_GeometricGoo destination = null;
		//	GH_Material destination2 = null;
		//	if (DA.GetData(0, ref destination) && DA.GetData(1, ref destination2) && destination.IsValid)
		//	{
		//		if (!(destination is IGH_PreviewData))
		//		{
		//			AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, destination.TypeName + " does not support previews");
		//		}
		//		else if (destination2.Value != null)
		//		{
		//			GH_CustomPreviewItem item = default(GH_CustomPreviewItem);
		//			item.Geometry = (IGH_PreviewData)destination;
		//			item.Shader = destination2.Value;
		//			item.Colour = destination2.Value.Diffuse;
		//			item.Material = destination2;
		//			_items.Add(item);
		//			_boundingBox.Union(destination.Boundingbox);
		//		}
		//	}
		//}

		//public override void DrawViewportWires(IGH_PreviewArgs args)
		//{
		//	if (_items != null && !args.Document.IsRenderMeshPipelineViewport(args.Display) && (string.IsNullOrEmpty(ViewportFilter) ))
		//	{
		//		if (base.Attributes.Selected)
		//		{
		//			GH_PreviewWireArgs args2 = new GH_PreviewWireArgs(args.Viewport, args.Display, args.WireColour_Selected, args.DefaultCurveThickness);
		//			foreach (GH_CustomPreviewItem item in _items)
		//			{
		//				if (!(item.Geometry is GH_Mesh) || CentralSettings.PreviewMeshEdges)
		//				{
		//					item.Geometry.DrawViewportWires(args2);
		//				}
		//			}
		//		}
		//		else
		//		{
		//			foreach (GH_CustomPreviewItem item2 in _items)
		//			{
		//				if (!(item2.Geometry is GH_Mesh) || CentralSettings.PreviewMeshEdges)
		//				{
		//					GH_PreviewWireArgs args3 = new GH_PreviewWireArgs(args.Viewport, args.Display, item2.Colour, args.DefaultCurveThickness);
		//					item2.Geometry.DrawViewportWires(args3);
		//				}
		//			}
		//		}
		//	}
		//}

		//public override void DrawViewportMeshes(IGH_PreviewArgs args)
		//{
		//	if (_items != null && !args.Document.IsRenderMeshPipelineViewport(args.Display) && (string.IsNullOrEmpty(ViewportFilter)))
		//	{
		//		if (base.Attributes.Selected)
		//		{
		//			GH_PreviewMeshArgs args2 = new GH_PreviewMeshArgs(args.Viewport, args.Display, args.ShadeMaterial_Selected, args.MeshingParameters);
		//			foreach (GH_CustomPreviewItem item in _items)
		//			{
		//				item.Geometry.DrawViewportMeshes(args2);
		//			}
		//		}
		//		else
		//		{
		//			foreach (GH_CustomPreviewItem item2 in _items)
		//			{
		//				GH_PreviewMeshArgs args3 = new GH_PreviewMeshArgs(args.Viewport, args.Display, item2.Shader, args.MeshingParameters);
		//				item2.Geometry.DrawViewportMeshes(args3);
		//			}
		//		}
		//	}
		//}

		//public override bool Write(GH_IWriter writer)
		//{
		//	writer.SetString("ViewportFilter", ViewportFilter);
		//	writer.SetBoolean("IncludeInRender", IncludeInRender);
		//	return base.Write(writer);
		//}

		//public override bool Read(GH_IReader reader)
		//{
		//	ViewportFilter = string.Empty;
		//	if (reader.ItemExists("ViewportFilter"))
		//	{
		//		ViewportFilter = reader.GetString("ViewportFilter");
		//	}
		//	base.Message = ViewportFilter;
		//	IncludeInRender = true;
		//	if (reader.ItemExists("IncludeInRender"))
		//	{
		//		IncludeInRender = reader.GetBoolean("IncludeInRender");
		//	}
		//	return base.Read(reader);
		//}

		//protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
		//{
		//	GH_DocumentObject.Menu_MoveItem(GH_DocumentObject.Menu_AppendItem(menu, "Render", RenderClick, enabled: true, IncludeInRender), true, "Preview");
		//	GH_DocumentObject.Menu_AppendItem(menu, "Viewport Filter");
		//	GH_DocumentObject.Menu_AppendTextItem(menu, ViewportFilter, ViewportFilterKeyDown, null, lockOnFocus: false);
		//}

		//private void RenderClick(object sender, EventArgs e)
		//{
		//	if (IncludeInRender)
		//	{
		//		RecordUndoEvent("Render Exclusion");
		//	}
		//	else
		//	{
		//		RecordUndoEvent("Render Inclusion");
		//	}
		//	IncludeInRender = !IncludeInRender;
		//	CustomRenderMeshProvider.DocumentBasedMeshesChanged(RhinoDoc.ActiveDoc);
		//}

		//private void ViewportFilterKeyDown(GH_MenuTextBox sender, KeyEventArgs e)
		//{
		//	if (e.KeyCode == Keys.Return)
		//	{
		//		RecordUndoEvent("Filter: " + sender.Text);
		//		ViewportFilter = sender.Text;
		//		base.Message = sender.Text;
		//		base.Attributes.ExpireLayout();
		//		Instances.RedrawAll();
		//	}
		//}

		//[Obsolete]
		//public override void AppendRenderGeometry(GH_RenderArgs args)
		//{
		//	List<GH_CustomPreviewItem> items = _items;
		//	if (items != null)
		//	{
		//		items = new List<GH_CustomPreviewItem>(items);
		//		if (items.Count != 0)
		//		{
		//			foreach (GH_CustomPreviewItem item in items)
		//			{
		//				item.PushToRenderPipeline(args);
		//			}
		//		}
		//	}
		//}

		//public override void BakeGeometry(RhinoDoc doc, ObjectAttributes att, List<Guid> objectIds)
		//{
		//	if (_items != null && _items.Count != 0)
		//	{
		//		if (att == null)
		//		{
		//			att = doc.CreateDefaultAttributes();
		//		}
		//		foreach (GH_CustomPreviewItem item in _items)
		//		{
		//			Guid guid = item.PushToRhinoDocument(doc, att);
		//			if (guid != Guid.Empty)
		//			{
		//				objectIds.Add(guid);
		//			}
		//		}
		//	}
		//}

		///// <summary>
		///// Exposure override for position in the Subcategory (options primary to septenary)
		///// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
		///// </summary>
		//public override GH_Exposure Exposure
		//{
		//	get { return GH_Exposure.hidden; }
		//}

		///// <summary>
		///// Provides an Icon for the component.
		///// </summary>
		//protected override System.Drawing.Bitmap Icon
  //      {
  //          get
  //          {
  //              //You can add image files to your project resources and access them like this:
  //              // return Resources.IconForThisComponent;
  //              return null;
  //          }
  //      }

  //      /// <summary>
  //      /// Gets the unique ID for this component. Do not change this ID after release.
  //      /// </summary>
  //      public override Guid ComponentGuid
  //      {
  //          get { return new Guid("4e74d471-42da-4987-b21d-d5b01d87c6bc"); }
  //      }
  //  }
}