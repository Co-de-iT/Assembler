using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
// <Custom using>
using System.Linq;
using System.Threading.Tasks;
using Rhino.Collections;
using System.Windows.Forms;
// </Custom using>


/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance6 : GH_ScriptInstance
{
    #region Utility functions
    /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
    /// <param name="text">String to print.</param>
    private void Print(string text) { __out.Add(text); }
    /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
    /// <param name="format">String format.</param>
    /// <param name="args">Formatting parameters.</param>
    private void Print(string format, params object[] args) { __out.Add(string.Format(format, args)); }
    /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj)); }
    /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj, string method_name) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj, method_name)); }
    #endregion

    #region Members
    /// <summary>Gets the current Rhino document.</summary>
    private RhinoDoc RhinoDocument;
    /// <summary>Gets the Grasshopper document that owns this script.</summary>
    private GH_Document GrasshopperDocument;
    /// <summary>Gets the Grasshopper script component that owns this script.</summary>
    private IGH_Component Component;
    /// <summary>
    /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
    /// Any subsequent call within the same solution will increment the Iteration count.
    /// </summary>
    private int Iteration;
    #endregion

    /// <summary>
    /// This procedure contains the user code. Input parameters are provided as regular arguments, 
    /// Output parameters as ref arguments. You don't have to assign output parameters, 
    /// they will have a default value.
    /// </summary>
    private void RunScript(DataTree<string> AOt, List<Plane> OP, DataTree<Plane> HP, ref object cM, ref object A)
    {
        // <Custom code> 

        if (netStig == null) netStig = new NetworkStigmergy(AOt);

        cM = netStig.connMap;
        A = netStig.connections.Select(c => c.neighbourH).ToList();

        // </Custom code> 
    }

    // <Custom additional code> 

    public NetworkStigmergy netStig;

    public class NetworkStigmergy
    {
        DataTree<int?> neighComp = new DataTree<int?>();
        DataTree<int?> neighType = new DataTree<int?>();
        DataTree<int> neighHand = new DataTree<int>();
        public DataTree<int> connMap = new DataTree<int>();
        public List<Connection> connections;

        public NetworkStigmergy(DataTree<string> AOt)
        {
            connections = new List<Connection>();
            for (int i = 0; i < AOt.BranchCount; i++)
            {
                for (int j = 0; j < AOt.Branches[i].Count; j++)
                {
                    int[] data = AOt.Branches[i][j].Split(new[] { '|', '=' }).Select(x => Convert.ToInt32(x)).ToArray();
                    // i - index of current component
                    // j - index of current handle
                    // data[0] = neighbour component index
                    // data[1] = neighbour component type
                    // data[2] = neighbour component handle index
                    Connection connection = new Connection(this, i, j);
                    connections.Add(connection);
                    connMap.Add(connections.Count - 1, AOt.Paths[i]);
                    if (data.Length == 3)
                    {
                        neighComp.Add(data[0], AOt.Paths[i]);
                        neighType.Add(data[1], AOt.Paths[i]);
                        neighHand.Add(data[2], AOt.Paths[i]);
                    }
                    else
                    {
                        neighComp.Add(null, AOt.Paths[i]);
                        neighType.Add(null, AOt.Paths[i]);
                        neighHand.Add(data[0], AOt.Paths[i]);
                    }
                }
            }

            FindConnectionNeighbours();

        }


        void FindConnectionNeighbours()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                // extract connection O index and H index
                int O = connections[i].O;
                int H = connections[i].H;

                // . . . . neighbours on the side of O
                //
                //
                // see connection map at path O for O neighbours
                int[] ind = connMap.Branches[O].ToArray();
                foreach (int otherO in ind)
                    if (otherO != O)
                    {
                        connections[i].neighbourO.Add(otherO);
                        connections[otherO].neighbourO.Add(i);
                    }
                // . . . . neighbour on the side of H
                //
                //
                // see neighComp tree at path O and index H for H neighbour
                int? nC = neighComp.Branches[O][H];
                // if there's no neighbour the value is -1
                if (nC == null) connections[i].neighbourH = -1;
                else 
                {
                    // find neighbour handle index
                    int nH = neighHand.Branches[O][H];
                    // find connection index
                    int neighConn = connMap.Branches[(int)nC][nH];
                    connections[i].neighbourH = neighConn;
                    connections[neighConn].neighbourH = i;
                }
            }
        }
    }

    public class Connection
    {
        NetworkStigmergy nS;
        public int O; // component index
        public int H; // handle index
        public List<int> neighbourO; // list of neighbour connections indices
        public int neighbourH; // index of neighbour connection (-1 for no neighbours)
        public double wA, wB, weight;

        public Connection(NetworkStigmergy nS, int o, int h)
        {
            this.nS = nS;
            O = o;
            H = h;
            neighbourO = new List<int>();
            neighbourH = -1;
            wA = 0;
            wB = 0;
            weight = 0;
        }

        void ComputeWeight()
        {


        }
    }
    // </Custom additional code> 

    private List<string> __err = new List<string>(); //Do not modify this list directly.
    private List<string> __out = new List<string>(); //Do not modify this list directly.
    private RhinoDoc doc = RhinoDoc.ActiveDoc;       //Legacy field.
    private IGH_ActiveObject owner;                  //Legacy field.
    private int runCount;                            //Legacy field.

    public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA)
    {
        //Prepare for a new run...
        //1. Reset lists
        this.__out.Clear();
        this.__err.Clear();

        this.Component = owner;
        this.Iteration = iteration;
        this.GrasshopperDocument = owner.OnPingDocument();
        this.RhinoDocument = rhinoDocument as Rhino.RhinoDoc;

        this.owner = this.Component;
        this.runCount = this.Iteration;
        this.doc = this.RhinoDocument;

        //2. Assign input parameters
        DataTree<string> AOt = null;
        if (inputs[0] != null)
        {
            AOt = GH_DirtyCaster.CastToTree<string>(inputs[0]);
        }

        List<Plane> OP = null;
        if (inputs[1] != null)
        {
            OP = GH_DirtyCaster.CastToList<Plane>(inputs[1]);
        }
        DataTree<Plane> HP = null;
        if (inputs[2] != null)
        {
            HP = GH_DirtyCaster.CastToTree<Plane>(inputs[2]);
        }



        //3. Declare output parameters
        object A = null;


        //4. Invoke RunScript
        RunScript(AOt, OP, HP, ref A);

        try
        {
            //5. Assign output parameters to component...
            if (A != null)
            {
                if (GH_Format.TreatAsCollection(A))
                {
                    IEnumerable __enum_A = (IEnumerable)(A);
                    DA.SetDataList(1, __enum_A);
                }
                else
                {
                    if (A is Grasshopper.Kernel.Data.IGH_DataTree)
                    {
                        //merge tree
                        DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(A));
                    }
                    else
                    {
                        //assign direct
                        DA.SetData(1, A);
                    }
                }
            }
            else
            {
                DA.SetData(1, null);
            }

        }
        catch (Exception ex)
        {
            this.__err.Add(string.Format("Script exception: {0}", ex.Message));
        }
        finally
        {
            //Add errors and messages... 
            if (owner.Params.Output.Count > 0)
            {
                if (owner.Params.Output[0] is Grasshopper.Kernel.Parameters.Param_String)
                {
                    List<string> __errors_plus_messages = new List<string>();
                    if (this.__err != null) { __errors_plus_messages.AddRange(this.__err); }
                    if (this.__out != null) { __errors_plus_messages.AddRange(this.__out); }
                    if (__errors_plus_messages.Count > 0)
                        DA.SetDataList(0, __errors_plus_messages);
                }
            }
        }
    }
}