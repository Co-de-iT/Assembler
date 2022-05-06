using System.Collections.Generic;
using Rhino.Geometry;

namespace AssemblerLib
{
    /// <summary>
    /// A class that manages settings for Exogenous elements (environment, Field)
    /// </summary>
    public struct ExogenousSettings
    {
        /// <summary>
        /// List of environment Meshes
        /// </summary>
        public readonly List<MeshEnvironment> environmentMeshes;
        /// <summary>
        /// interaction mode with the environment Meshes
        /// <list type="bullet">
        /// <item>0 - ignore objects</item>
        /// <item>1 - container collision</item>
        /// <item>2 - container inclusion</item>
        /// </list>
        /// </summary>
        public readonly int environmentMode;
        /// <summary>
        /// Field used by the Assemblage
        /// </summary>
        public readonly Field field;
        /// <summary>
        /// Field threshold for scalar values
        /// </summary>
        public readonly double fieldScalarThreshold;
        /// <summary>
        /// Sandbox - NOT IMPLEMENTED YET
        /// </summary>
        public Box sandBox;

        /// <summary>
        /// Constructs an ExogenousSettings instance from required parameters
        /// </summary>
        /// <param name="meshes"></param>
        /// <param name="environmentMode">sets the <see cref="environmentMode"/></param>
        /// <param name="field">Field used by the Assemblage</param>
        /// <param name="fieldScalarThreshold">threshold for scalar values</param>
        /// <param name="sandBox">NOT IMPLEMENTED YET - use a Box.Unset here in case you need to call this constructor</param>
        /// <param name="hasContainer">True if the first mesh in the meshes list should be considered as a container</param>
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
