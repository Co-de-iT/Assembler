using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib
{
    /// <summary>
    /// A simple Xtra/Xtended data container
    /// </summary>
    public class XData
    {
        /// <summary>
        /// generic data container as list
        /// </summary>
        public List<object> data;
        /// <summary>
        /// label for the XData
        /// </summary>
        public readonly string label;
        /// <summary>
        /// Reference Plane for XData
        /// </summary>
        public Plane refPlane;
        /// <summary>
        /// AssemblyObject name for XData association
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
        /// <param name="data"></param>
        /// <param name="label"></param>
        /// <param name="refPlane"></param>
        /// <param name="AOName"></param>
        public XData(List<object> data, string label, Plane refPlane, string AOName)
        {
            this.data = data;
            this.label = label;
            this.refPlane = refPlane;
            this.AOName = AOName;
        }

        /// <summary>
        /// Duplicate Method
        /// </summary>
        /// <param name="otherXData"></param>
        public XData(XData otherXData)
        {
            label = otherXData.label;
            refPlane = otherXData.refPlane;
            AOName = otherXData.AOName;
            object[] dArray = new object[otherXData.data.Count];
            otherXData.data.CopyTo(dArray);
            data = dArray.ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="otherXDataCopy"></param>
        /// <returns></returns>
        public XData XCopy(XData otherXDataCopy)
        {
            XData xd;
            object[] dArray = new object[otherXDataCopy.data.Count];
            otherXDataCopy.data.CopyTo(dArray);
            xd = new XData(dArray.ToList(), otherXDataCopy.label, otherXDataCopy.refPlane, otherXDataCopy.AOName);
            return xd;
        }

        /// <summary>
        /// Apply a transformation to the XData
        /// </summary>
        /// <param name="xForm"></param>
        public void Transform(Transform xForm)
        {
            refPlane.Transform(xForm);
            List<object> tData = new List<object>();

            GeometryBase g, gT;
            Point3d p;
            Vector3d v;
            Plane pl;
            for (int i = 0; i < data.Count; i++)
            {

                g = data[i] as GeometryBase;
                if (g == null)
                {
                    // since Point3d, Vector3d & Plane are Structures, the as GeometryBase cast doesn't catch them (returns null)
                    // However, they need to be transformed, so....
                    if (data[i] is Point3d)
                    {
                        p = (Point3d)data[i];
                        p.Transform(xForm);
                        tData.Add(p);
                    }
                    else if (data[i] is Vector3d)
                    {
                        v = (Vector3d)data[i];
                        v.Transform(xForm);
                        tData.Add(v);
                    }
                    else if (data[i] is Plane)
                    {
                        pl = (Plane)data[i];
                        pl.Transform(xForm);
                        tData.Add(pl);
                    }
                    else tData.Add(data[i]);
                }
                else
                {
                    gT = g.Duplicate();
                    gT.Transform(xForm);
                    tData.Add(gT);
                }
            }

            data = tData;
        }
    }
}
