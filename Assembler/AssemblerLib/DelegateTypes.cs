using System.Collections.Generic;

namespace AssemblerLib
{
    #region delegate types

    // . . . delegate types for sender-receiver computation and selection

    /// <summary>
    /// Delegate type for environment container behavior (collision or inclusion)
    /// </summary>
    /// <param name="sO"></param>
    /// <param name="EnvironmentMeshes"></param>
    /// <returns>True if an <see cref="AssemblyObject"/> is invalid (clash detected or other invalidating condition), False if valid</returns>
    public delegate bool EnvironmentClashMethod(AssemblyObject sO, List<MeshEnvironment> EnvironmentMeshes);
    
    /// <summary>
    /// Delegate type for computing candidates (sender) values
    /// </summary>
    /// <param name="Assemblage">The Assemblage to work on</param>
    /// <param name="candidates">The list of candidates to compute</param>
    /// 
    /// <returns>array of values associated with the candidates</returns>
    public delegate T[] ComputeCandidatesValuesMethod<T>(Assemblage Assemblage, List<AssemblyObject> candidates);

    /// <summary>
    /// Delegate type for computing a single receiver value
    /// </summary>
    /// <param name="Assemblage">The Assemblage to work on</param>
    /// <param name="receiver">The receiver to compute</param>
    /// 
    /// <returns>value computed for the receiver object</returns>
    public delegate T ComputeReceiverMethod<T>(Assemblage Assemblage, AssemblyObject receiver);
    
    /// <summary>
    /// Delegate type for choosing winner index from sender values
    /// </summary>
    /// <param name="values">a collection of values, as array or list</param>
    /// <returns>index of winner candidate</returns>
    public delegate int SelectWinnerMethod<T>(T[] values);

    #endregion delegate types
}
