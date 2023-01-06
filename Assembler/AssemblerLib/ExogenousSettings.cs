using System.Collections.Generic;
using Rhino.Geometry;

namespace AssemblerLib
{
    /// <summary>
    /// A structure that manages settings for Exogenous elements (environment, Field)
    /// </summary>
    public struct ExogenousSettings
    {
        /// <summary>
        /// List of environment Meshes
        /// </summary>
        public List<MeshEnvironment> EnvironmentMeshes { get; private set; }
        /// <summary>
        /// interaction mode with the environment Meshes
        /// <list type="bullet">
        /// <item><description>0 - ignore objects</description></item>
        /// <item><description>1 - container collision</description></item>
        /// <item><description>2 - container inclusion</description></item>
        /// </list>
        /// </summary>
        public readonly int EnvironmentMode;
        /// <summary>
        /// <see cref="AssemblerLib.Field"/> used by the Assemblage
        /// </summary>
        public Field Field { get; }
        /// <summary>
        /// <see cref="AssemblerLib.Field"/> threshold for scalar values
        /// </summary>
        public readonly double FieldScalarThreshold;
        /// <summary>
        /// Sandbox - NOT IMPLEMENTED YET
        /// </summary>
        public Box SandBox;

        /// <summary>
        /// Constructs an ExogenousSettings instance from required parameters
        /// </summary>
        /// <param name="meshes"></param>
        /// <param name="EnvironmentMode">sets the <see cref="EnvironmentMode"/></param>
        /// <param name="Field">Field used by the Assemblage</param>
        /// <param name="FieldScalarThreshold">threshold for scalar values</param>
        /// <param name="SandBox">NOT IMPLEMENTED YET - use a Box.Unset here in case you need to call this constructor</param>
        /// <param name="hasContainer">True if the first mesh in the meshes list should be considered as a container</param>
        public ExogenousSettings(List<Mesh> meshes, int EnvironmentMode, Field Field, double FieldScalarThreshold, Box SandBox, bool hasContainer)
        {
            // invalid meshes should be filtered out before sending the list to this struct
            EnvironmentMeshes = new List<MeshEnvironment>();

            // if there is a container, add it to the environment meshes list and remove it from the meshes list
            if (hasContainer && meshes.Count > 0)
            {
                if (meshes[0].Volume() > 0) meshes[0].Flip(true, true, true);
                EnvironmentMeshes.Add(new MeshEnvironment(meshes[0], 2));
                meshes.RemoveAt(0);
            }

            // label the rest of the meshes
            int label;
            for (int i = 0; i < meshes.Count; i++)
            {
                // Volume value separates void (<0) from solids (>0)
                label = meshes[i].Volume() < 0 ? 0 : 1;
                EnvironmentMeshes.Add(new MeshEnvironment(meshes[i], label));
            }

            // if the mesh list is empty, force EnvironmentMode to 0
            this.EnvironmentMode = EnvironmentMeshes.Count == 0 ? 0 : EnvironmentMode;
            this.Field = Field;
            this.FieldScalarThreshold = FieldScalarThreshold;
            this.SandBox = SandBox;
        }
    }
}
