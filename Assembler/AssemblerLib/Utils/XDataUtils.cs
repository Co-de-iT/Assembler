using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace AssemblerLib.Utils
{
    public static class XDataUtils
    {
        public static List<XData> AssociateXDataToAO(AssemblyObject AO, List<XData> xData)
        {
            List<XData> orientedXData = new List<XData>();

            for (int j = 0; j < xData.Count; j++)
            {
                // if the object does not match XData associated kind go on
                if (!String.Equals(AO.Name, xData[j].AOName)) continue;

                XData xdC = new XData(xData[j]);
                Transform orient = Transform.PlaneToPlane(xdC.ReferencePlane, AO.ReferencePlane);
                xdC.Transform(orient);
                orientedXData.Add(xdC);
            }
            return orientedXData;
        }
    }
}
