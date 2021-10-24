using System.Collections.Generic;
using Rhino.Geometry;

namespace AssemblerLib
{
    public struct ExogenousSettings
    {
        public List<Mesh> environmentMeshes;
        public int environmentMode;
        public Field field;
        public double fieldScalarThreshold;
        public Box sandBox;

        public ExogenousSettings(List<Mesh> environmentMeshes, int environmentMode, Field field, double fieldScalarThreshold, Box sandBox)
        {
            this.environmentMeshes = environmentMeshes;
            this.environmentMode = environmentMode;
            this.field = field;
            this.fieldScalarThreshold = fieldScalarThreshold;
            this.sandBox = sandBox;
        }
    }
}
