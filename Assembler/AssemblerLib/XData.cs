using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib
{
    /// <summary>
    /// A simple Xtra/Xtended Data container
    /// </summary>
    public class XData
    {
        /// <summary>
        /// generic data container as list
        /// </summary>
        public List<object> Data;
        /// <summary>
        /// label for the XData
        /// </summary>
        public readonly string label;
        /// <summary>
        /// Reference Plane for XData
        /// </summary>
        public Plane ReferencePlane;
        /// <summary>
        /// AssemblyObject Name for XData association
        /// </summary>
        public readonly string AOName;

        /*
         FUTURE IMPLEMENTATION

        List <XData> children;
        string parent;

        To implement hierarchical data
         */

        /// <summary>
        /// Constructs an Xdata item
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="Label"></param>
        /// <param name="ReferencePlane"></param>
        /// <param name="AOName"></param>
        public XData(List<object> Data, string Label, Plane ReferencePlane, string AOName)
        {
            this.Data = Data;
            this.label = Label;
            this.ReferencePlane = ReferencePlane;
            this.AOName = AOName;
        }

        /// <summary>
        /// Duplicate Method
        /// </summary>
        /// <param name="otherXData"></param>
        public XData(XData otherXData)
        {
            label = otherXData.label;
            ReferencePlane = otherXData.ReferencePlane;
            AOName = otherXData.AOName;
            object[] dArray = new object[otherXData.Data.Count];
            otherXData.Data.CopyTo(dArray);
            Data = dArray.ToList();
        }

        /// <summary>
        /// Apply a transformation to the XData
        /// </summary>
        /// <param name="xForm">Transformation to apply</param>
        public void Transform(Transform xForm)
        {
            ReferencePlane.Transform(xForm);
            List<object> tData = new List<object>();

            GeometryBase g, gT;

            for (int i = 0; i < Data.Count; i++)
            {

                g = Data[i] as GeometryBase;
                if (g == null)
                {
                    // since Point3d, Vector3d, Line & Plane are Structures, the as GeometryBase cast doesn't catch them (returns null)
                    // However, they need to be transformed, so....
                    if (Data[i] is Point3d pd)
                    {
                        pd.Transform(xForm);
                        tData.Add(pd);
                    }
                    else if (Data[i] is Vector3d vd)
                    {
                        vd.Transform(xForm);
                        tData.Add(vd);
                    }
                    else if (Data[i] is Plane pld)
                    {
                        pld.Transform(xForm);
                        tData.Add(pld);
                    }
                    else if (Data[i] is Line ld)
                    {
                        ld.Transform(xForm);
                        tData.Add(ld);
                    }
                    else tData.Add(Data[i]);
                }
                else
                {
                    gT = g.Duplicate();
                    gT.Transform(xForm);
                    tData.Add(gT);
                }
            }

            Data = tData;
        }

        public override string ToString()
        {
            return string.Format("XData {0} . AO {1} . {2} data object(s)", label, AOName, Data.Count);
        }
    }
}
