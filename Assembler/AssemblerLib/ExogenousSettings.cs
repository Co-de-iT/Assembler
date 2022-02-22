using System.Collections.Generic;
using Rhino.Geometry;

namespace AssemblerLib
{
    public struct ExogenousSettings
    {
        public readonly List<MeshEnvironment> environmentMeshes;
        public readonly int environmentMode;
        public readonly Field field;
        public readonly double fieldScalarThreshold;
        public Box sandBox;

        public ExogenousSettings(List<Mesh> meshes, int environmentMode, Field field, double fieldScalarThreshold, Box sandBox, bool hasContainer)
        {
            // invalid meshes should be filtered out before sending the list to this struct
            environmentMeshes = new List<MeshEnvironment>();

            // if there is a container, add it to the environment meshes list and remove it from the meshes list
            if (hasContainer && meshes.Count > 0)
            {
                if (meshes[0].Volume() > 0) meshes[0].Flip(true, true, true);
                environmentMeshes.Add(new MeshEnvironment(meshes[0], 2));
                meshes.RemoveAt(0);
            }

            // label the rest of the meshes
            int label;
            for (int i = 0; i < meshes.Count; i++)
            {
                // Volume value separates void (<0) from solids (>0)
                label = meshes[i].Volume() < 0 ? 0 : 1;
                environmentMeshes.Add(new MeshEnvironment(meshes[i], label));
            }

            // if the mesh list is empty, force environmentMode to 0
            this.environmentMode = environmentMeshes.Count == 0 ? 0 : environmentMode;
            this.field = field;
            this.fieldScalarThreshold = fieldScalarThreshold;
            this.sandBox = sandBox;
        }
    }
}
