﻿using AssemblerLib.Utils;
using Rhino.Geometry;
using System.Collections.Generic;

namespace AssemblerLib
{
    /// <summary>
    /// A structure that manages settings for Exogenous elements (environment, Field)
    /// </summary>
    public struct ExogenousSettings
    {
        /// <summary>
        /// Environment modes enumerable
        /// <list type="bullet">
        /// <item><description>0 - Ignore objects</description></item>
        /// <item><description>1 - Container collision</description></item>
        /// <item><description>2 - Container inclusion</description></item>
        /// <item><description>-1 - Custom mode (requires user implementation)</description></item>
        /// </list>
        /// </summary>
        //public enum EnvironmentModes : int { Custom = -1, Ignore = 0, ContainerCollision = 1, ContainerInclusion = 2 };
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
        public readonly EnvironmentModes EnvironmentMode;
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

        public EnvironmentClashMethod environmentClash;

        public ExogenousSettings(List<Mesh> meshes, int EnvironmentMode, Field Field, double FieldScalarThreshold, Box SandBox, bool hasContainer) : this(meshes, EnvironmentMode, Field, FieldScalarThreshold, SandBox, hasContainer, null)
        { }

        /// <summary>
        /// Constructs an ExogenousSettings instance from required parameters
        /// </summary>
        /// <param name="meshes"></param>
        /// <param name="EnvironmentMode">sets the <see cref="EnvironmentMode"/></param>
        /// <param name="Field">Field used by the Assemblage</param>
        /// <param name="FieldScalarThreshold">threshold for scalar values</param>
        /// <param name="SandBox">NOT IMPLEMENTED YET - use a Box.Unset here in case you need to call this constructor</param>
        /// <param name="hasContainer">True if the first mesh in the meshes list should be considered as a container</param>
        public ExogenousSettings(List<Mesh> meshes, int EnvironmentMode, Field Field, double FieldScalarThreshold, Box SandBox, bool hasContainer, EnvironmentClashMethod customEnvironmentClash)
        {
            // invalid meshes should be filtered out before sending the list to this struct
            EnvironmentMeshes = new List<MeshEnvironment>();

            // if there is a container, add it to the environment meshes list and remove it from the meshes list
            if (hasContainer && meshes.Count > 0)
            {
                // this check is already in the MeshEnvironment constructor
                //if (meshes[0].Volume() > 0) meshes[0].Flip(true, true, true);
                EnvironmentMeshes.Add(new MeshEnvironment(meshes[0], EnvironmentType.Container));
                meshes.RemoveAt(0);
            }

            // check the rest of the meshes and assign environment types
            EnvironmentType envType;
            for (int i = 0; i < meshes.Count; i++)
            {
                // Volume value separates void (<0) from solids (>0)
                envType = meshes[i].Volume() < 0 ? EnvironmentType.Void : EnvironmentType.Solid;//0 : 1;
                EnvironmentMeshes.Add(new MeshEnvironment(meshes[i], envType));
            }

            // if the mesh list is empty, force EnvironmentMode to Ignore
            this.EnvironmentMode = EnvironmentMeshes.Count == 0 ? EnvironmentModes.Ignore : (EnvironmentModes)EnvironmentMode;
            this.Field = Field;
            this.FieldScalarThreshold = FieldScalarThreshold;
            this.SandBox = SandBox;
            //environmentClash = customEnvironmentClash;

            switch (this.EnvironmentMode)
            {
                case EnvironmentModes.Custom:
                    // custom mode - method assigned in scripted component or iterative mode
                    environmentClash = customEnvironmentClash;
                    break;
                case EnvironmentModes.Ignore:
                    environmentClash = (sO, EnvironmentMeshes) => false;
                    break;
                case EnvironmentModes.ContainerCollision:
                    environmentClash = ComputingRSMethods.EnvironmentClashCollision;
                    break;
                case EnvironmentModes.ContainerInclusion:
                    environmentClash = ComputingRSMethods.EnvironmentClashInclusion;
                    break;
                default: // default is inclusion
                    goto case EnvironmentModes.ContainerInclusion;
            }
        }

        public void AssignCustomMethod(EnvironmentClashMethod customEnvironmentClash)
        {
            if (customEnvironmentClash != null)
                this.environmentClash = customEnvironmentClash;
        }
    }
}
