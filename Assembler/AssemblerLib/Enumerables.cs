namespace AssemblerLib
{
    /// <summary>
    /// Heuristics modes enumerable type. Indicates the Heuristic Set selection mode during an Assemblage
    /// <list type="bullet">
    /// <item><description>0 - Manual mode</description></item>
    /// <item><description>1 - Field driven</description></item>
    /// </list>
    /// </summary>
    public enum HeuristicModes : int { Manual, Field };

    /// <summary>
    /// Environment modes enumerable type
    /// <list type="bullet">
    /// <item><description>0 - Ignore objects</description></item>
    /// <item><description>1 - Container collision</description></item>
    /// <item><description>2 - Container inclusion</description></item>
    /// <item><description>-1 - Custom - requires scripting a custom method or using the iterative engine</description></item>
    /// </list>
    /// </summary>
    public enum EnvironmentModes : int { Custom = -1, Ignore = 0, ContainerCollision = 1, ContainerInclusion = 2 };
    
    /// <summary>
    /// EnvironmentMesh type enumerable
    /// <list type="bullet">
    /// <item><description>-1 - Container</description></item>
    /// <item><description>0 - Void</description></item>
    /// <item><description>1 - Solid</description></item>
    /// </list>
    /// </summary>
    public enum EnvironmentType : int { Container = -1, Void = 0, Solid = 1 }
    
    // TODO: use this for Handle Occupancy (requires a looong refactoring though)
    /// <exclude>Eclude from documentation</exclude>
    public enum OccupancyStatus : int { Occluded = -1, Available = 0, Connected = 1, Contact = 2 }

}
