using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Assembler.Utils
{
    /*
     see these links to implement persistent parameter:
    https://gist.github.com/petrasvestartas/0d4bc6b8b2926f488ef36bd327f273ad
    https://gist.github.com/petrasvestartas/352e7e948411a9145ab7e1575505c2d1
     */
    public class AssemblyObjectGoo : GH_GeometricGoo<AssemblyObject>, IGH_PreviewData, IGH_BakeAwareObject
    {

        #region constructors
        public AssemblyObjectGoo()
        {
            m_value = new AssemblyObject();
        }

        public AssemblyObjectGoo(AssemblyObject AO)
        {
            if (AO == null)
                AO = new AssemblyObject();
            //Value = AO;
            m_value = AO;
            // check this: I used Value here but the examples I've seen use m_value. What's the difference? Investigate
            // see https://discourse.mcneel.com/t/gh-geometricgoo-constructor-advice-needed/93803
            // see Boat.cs example by David Rutten here:
            // https://www.grasshopper3d.com/forum/topics/custom-data-type-gh-geometricgoo-or-gh-goo?commentId=2985220%3AComment%3A586611
        }

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            return DuplicateAssemblyObject();
        }

        public AssemblyObjectGoo DuplicateAssemblyObject()
        {
            return new AssemblyObjectGoo(Value == null ? new AssemblyObject() : Utilities.CloneWithConnectivity(Value));
        }

        #endregion

        #region properties

        private Guid ReferenceGuid;

        public override Guid ReferenceID
        {
            get
            {
                return ReferenceGuid;
            }
            set
            {
                ReferenceGuid = value;
            }
        }

        public override bool IsValid
        {
            get
            {
                if (Value == null || Value.collisionMesh == null) return false;
                return true;
            }
        }

        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null) { return "No internal AssemblyObject instance"; }
                else { return string.Empty; }
            }
        }

        public override string ToString()
        {
            if (Value == null) return "Null AssemblyObject";
            return "AssemblyObject " + Value.name + ", with " + Value.handles.Length + " Handles";
        }

        public override string TypeName
        {
            get { return "AssemblyObject"; }
        }

        public override string TypeDescription
        {
            get { return "Defines an AssemblyObject for the Assembler plug-in"; }
        }

        public override BoundingBox Boundingbox
        {
            get
            {
                if (Value == null) { return BoundingBox.Empty; }
                return Value.collisionMesh.GetBoundingBox(false);
            }
        }

        public override BoundingBox GetBoundingBox(Transform xform)
        {
            if (Value == null) { return BoundingBox.Empty; }
            return Value.collisionMesh.GetBoundingBox(xform);
        }



        #endregion

        #region casting methods

        public override bool CastTo<Q>(out Q target)
        {

            // Cast to AssemblyObject
            if (typeof(Q).IsAssignableFrom(typeof(AssemblyObject)))
            {
                if (Value == null)
                    target = default(Q);
                else
                    target = (Q)(object)Value;
                return true;
            }

            // Cast to Mesh
            if (typeof(Q).IsAssignableFrom(typeof(Mesh)))
            {
                if (Value == null)
                    target = default(Q);
                else
                    target = (Q)(object)Value.collisionMesh.DuplicateShallow();
                return true;
            }

            // Todo: cast to Plane and/or point (reference Plane), vector (direction)

            target = default(Q);
            return false;
            //return base.CastTo<Q>(out target);
        }

        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }

            //Cast from AssemblyObject
            if (typeof(AssemblyObject).IsAssignableFrom(source.GetType()))
            {
                Value = (AssemblyObject)source;
                return true;
            }

            return false;
        }

        #endregion

        #region transformation methods

        public override IGH_GeometricGoo Transform(Transform xform)
        {
            if (Value == null) return null;
            if (Value.collisionMesh == null) return null;

            AssemblyObject AOclone = Utilities.CloneWithConnectivity(Value);
            AOclone.Transform(xform);
            return (new AssemblyObjectGoo(AOclone));
        }

        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region drawing methods
        public BoundingBox ClippingBox
        {
            get { return Boundingbox; }
        }

        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {
            Mesh m = new Mesh();
            m.CopyFrom(Value.collisionMesh);
            m.Unweld(0, true);
            args.Pipeline.DrawMeshShaded(m, new Rhino.Display.DisplayMaterial(Color.White, 0.7));
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            args.Pipeline.DrawMeshWires(Value.collisionMesh, Color.Black, 1);
            args.Pipeline.DrawLine(Value.referencePlane.Origin, Value.referencePlane.Origin + Value.referencePlane.XAxis, Color.Red, 1);
            args.Pipeline.DrawLine(Value.referencePlane.Origin, Value.referencePlane.Origin + Value.referencePlane.YAxis, Color.Green, 1);
        }

        #endregion

        #region (de)serialization methods

        // things that must be (de)serialized - [w] serialized - [r] deserialized
        //
        // [w][r] collisionMesh
        // [w][r] offsetMesh
        // [w][r] referencePlane
        // [w][r] handles
        // [w][r] direction
        // [w][r] type
        // [w][r] name
        // [w][r] aInd
        // [w][r] occludedNeighbours
        // [w][r] weight
        // [w][r] iWeight
        // [w][r] supports
        // [w][r] minSupports
        // [w][r] supported
        // [w][r] absoluteZLock

        private const string IoCollisionMeshKey = "CollisionMesh";
        private const string IoOffsetMeshKey = "OffsetMesh";
        private const string IoRefPlaneKey = "RefPlane";

        private const string IoHandlesCountKey = "HandlesCount";
        private const string IoHandleKey = "Handle_";
        private const string IoRrotKey = "_Rrotations";
        private const string IoRPlaneKey = "_RPlane_";
        private const string IoSPlaneKey = "_SPlane";
        private const string IoHWeightKey = "_Weight";
        private const string IoHIdleWeightKey = "_idleWeight";
        private const string IoHTypeKey = "_type";
        private const string IoHOccupancyKey = "_occupancy";
        private const string IoHNeighObjKey = "_neighbourObject";
        private const string IoHNeighHandKey = "_neighbourHandle";

        private const string IoDirectionKey = "Direction";
        private const string IoTypeKey = "Type";
        private const string IoNameKey = "Name";
        private const string IoaIndKey = "AInd";
        private const string IoOccludedNeighboursKey = "OccludedNeighbours";
        private const string IoWeightKey = "Weight";
        private const string IoIdleWeightKey = "idleWeight";
        private const string IoIWeightKey = "IWeight";

        private const string IoSupportsCountKey = "SupportsCount";
        private const string IoSupportKey = "Support_";
        private const string IoMinSupportsKey = "MinSupports";
        private const string IoSupportedKey = "Supported";
        private const string IoAbsZlockKey = "AbsZLock";
        private const string IoReceiverValueKey = "ReceiverValue";
        private const string IoSenderValueKey = "SenderValue";

        public override bool Write(GH_IWriter writer)
        {
            writer.SetGuid("RefID", ReferenceID);
            if (Value == null) return false;

            // serialize meshes
            byte[] collisionMeshByte = GH_Convert.CommonObjectToByteArray(Value.collisionMesh);
            byte[] offsetMeshByte = GH_Convert.CommonObjectToByteArray(Value.offsetMesh);
            writer.SetByteArray(IoCollisionMeshKey, collisionMeshByte);
            writer.SetByteArray(IoOffsetMeshKey, offsetMeshByte);

            // serialize reference Plane
            Plane p = Value.referencePlane;
            writer.SetPlane(IoRefPlaneKey, new GH_IO.Types.GH_Plane(
                    p.OriginX, p.OriginY, p.OriginZ, p.XAxis.X, p.XAxis.Y, p.XAxis.Z, p.YAxis.X, p.YAxis.Y, p.YAxis.Z));

            // serialize Handles
            // key struture: "Handle_<index>_<field>"
            // get number of handles
            writer.SetInt32(IoHandlesCountKey, Value.handles.Length);
            // serialize them
            for (int i = 0; i < Value.handles.Length; i++)
            {
                Handle h = Value.handles[i];

                // serialize number values
                writer.SetInt32(IoHandleKey + i.ToString() + IoHTypeKey, h.type);
                writer.SetDouble(IoHandleKey + i.ToString() + IoHWeightKey, h.weight);
                writer.SetDouble(IoHandleKey + i.ToString() + IoHIdleWeightKey, h.idleWeight);
                writer.SetInt32(IoHandleKey + i.ToString() + IoHOccupancyKey, h.occupancy);
                writer.SetInt32(IoHandleKey + i.ToString() + IoHNeighObjKey, h.neighbourObject);
                writer.SetInt32(IoHandleKey + i.ToString() + IoHNeighHandKey, h.neighbourHandle);

                // serialize sender plane
                p = h.sender;
                writer.SetPlane(IoHandleKey + i.ToString() + IoSPlaneKey, new GH_IO.Types.GH_Plane(
                    p.OriginX, p.OriginY, p.OriginZ, p.XAxis.X, p.XAxis.Y, p.XAxis.Z, p.YAxis.X, p.YAxis.Y, p.YAxis.Z));

                // serialize rotations (receiver planes will be reconstructed on deserialization)
                writer.SetDoubleArray(IoHandleKey + i.ToString() + IoRrotKey, h.rRotations);

            }

            // serialize direction
            Vector3d dir = Value.direction;
            writer.SetPoint3D(IoDirectionKey, new GH_IO.Types.GH_Point3D(dir.X, dir.Y, dir.Z));

            // serialize type
            writer.SetInt32(IoTypeKey, Value.type);

            // serialize name
            writer.SetString(IoNameKey, Value.name);

            // aInd
            writer.SetInt32(IoaIndKey, Value.AInd);

            // occludedNeighbours
            string occludedNeighbours = OccludeNeighboursToString(m_value.occludedNeighbours);
            writer.SetString(IoOccludedNeighboursKey,occludedNeighbours);

            // weight
            writer.SetDouble(IoWeightKey, Value.weight);
            
            // idleWeight
            writer.SetDouble(IoIdleWeightKey, Value.idleWeight);
            
            // iWeight
            writer.SetInt32(IoIWeightKey, Value.iWeight);

            // serialize Supports
            writer.SetInt32(IoSupportsCountKey, Value.supports.Count);
            // key structure: "Support_<index>_<field>"
            for (int i = 0; i < Value.supports.Count; i++)
            {
                Support s = Value.supports[i];
                writer.SetInt32(IoSupportKey + i.ToString() + "_neighbourObject", s.neighbourObject);
                writer.SetDouble(IoSupportKey + i.ToString() + "_initLength", s.initLength);

                Line l = s.line;
                writer.SetLine(IoSupportKey + i.ToString() + "_line", new GH_IO.Types.GH_Line(
                    l.FromX, l.FromY, l.FromZ, l.ToX, l.ToY, l.ToZ));
            }

            // minSupports
            writer.SetInt32(IoMinSupportsKey, Value.minSupports);

            // supported
            writer.SetBoolean(IoSupportedKey, Value.supported);

            // absoluteZLock
            writer.SetBoolean(IoAbsZlockKey, Value.worldZLock);

            // field values
            writer.SetDouble(IoReceiverValueKey, Value.receiverValue);
            writer.SetDouble(IoSenderValueKey, Value.senderValue);

            // children
            //if (m_value.children != null || m_value.children.Count > 0)
            //    for (int i = 0; i < m_value.children.Count; i++)
            //    {
            //        AssemblyObjectGoo child = new AssemblyObjectGoo(m_value.children[i]);
            //        child.Write(writer);
            //    }

            return true;
        }

        public override bool Read(GH_IReader reader)
        {

            //ReferenceGuid = Guid.Empty;
            //m_value = null;
            ReferenceID = reader.GetGuid("RefID");
            m_value = new AssemblyObject();

            // deserialize meshes
            if (reader.ItemExists(IoCollisionMeshKey))
            {
                m_value.collisionMesh = GH_Convert.ByteArrayToCommonObject<Mesh>(reader.GetByteArray(IoCollisionMeshKey));
            }
            if (reader.ItemExists(IoOffsetMeshKey))
            {
                m_value.offsetMesh = GH_Convert.ByteArrayToCommonObject<Mesh>(reader.GetByteArray(IoOffsetMeshKey));
            }

            // deserialize referencePlane
            if (reader.ItemExists(IoRefPlaneKey))
            {
                var pl = reader.GetPlane(IoRefPlaneKey);
                m_value.referencePlane = new Plane(new Point3d(pl.Origin.x, pl.Origin.y, pl.Origin.z),
                    new Vector3d(pl.XAxis.x, pl.XAxis.y, pl.XAxis.z), new Vector3d(pl.YAxis.x, pl.YAxis.y, pl.YAxis.z));
            }

            // deserialize handles
            if (reader.ItemExists(IoHandlesCountKey))
            {
                int nHandles = reader.GetInt32(IoHandlesCountKey);
                m_value.handles = new Handle[nHandles];

                for (int i = 0; i < nHandles; i++)
                {
                    // sender plane
                    var pl = reader.GetPlane(IoHandleKey + i.ToString() + IoSPlaneKey);
                    Plane sp = new Plane(new Point3d(pl.Origin.x, pl.Origin.y, pl.Origin.z),
                    new Vector3d(pl.XAxis.x, pl.XAxis.y, pl.XAxis.z), new Vector3d(pl.YAxis.x, pl.YAxis.y, pl.YAxis.z));

                    // type
                    int hType = reader.GetInt32(IoHandleKey + i.ToString() + IoHTypeKey);

                    // weight
                    double hWeight = reader.GetDouble(IoHandleKey + i.ToString() + IoHWeightKey);

                    // idleWeight
                    double hidleWeight = reader.GetDouble(IoHandleKey + i.ToString() + IoHIdleWeightKey);

                    // list of rotations
                    double[] rRot = reader.GetDoubleArray(IoHandleKey + i.ToString() + IoRrotKey);

                    // construct base handle
                    Handle h = new Handle(sp, hType, hWeight, rRot.ToList());
                    h.idleWeight = hidleWeight;

                    // add connectivity status
                    h.occupancy = reader.GetInt32(IoHandleKey + i.ToString() + IoHOccupancyKey);
                    h.neighbourObject = reader.GetInt32(IoHandleKey + i.ToString() + IoHNeighObjKey);
                    h.neighbourHandle = reader.GetInt32(IoHandleKey + i.ToString() + IoHNeighHandKey);

                    // add to array
                    m_value.handles[i] = h;
                }
            }

            // deserialize direction
            var p = reader.GetPoint3D(IoDirectionKey);
            m_value.direction = new Vector3d(p.x, p.y, p.z);

            // deserialize type
            m_value.type = reader.GetInt32(IoTypeKey);

            // deserialize name
            m_value.name = reader.GetString(IoNameKey);

            // deserialize aInd
            m_value.AInd = reader.GetInt32(IoaIndKey);

            // deserialize occludedNeighbours
            m_value.occludedNeighbours = OccludedNeighboursFromString(reader.GetString(IoOccludedNeighboursKey));

            // deserialize weight
            m_value.weight = reader.GetDouble(IoWeightKey);
            
            // deserialize idleWeight
            m_value.idleWeight = reader.GetDouble(IoIdleWeightKey);

            // deserialize iWeight
            m_value.iWeight = reader.GetInt32(IoIWeightKey);

            // deserialize supports
            int nSupports = reader.GetInt32(IoSupportsCountKey);
            m_value.supports = new List<Support>();
            for (int i = 0; i < nSupports; i++)
            {
                Support s;

                // line
                var l = reader.GetLine(IoSupportKey + i.ToString() + "_line");
                Line sLine = new Line(l.A.x, l.A.y, l.A.z, l.B.x, l.B.y, l.B.z);
                double currLen = sLine.Length;
                // initlength
                double initLen = reader.GetDouble(IoSupportKey + i.ToString() + "_initLength");
                // reset line length
                sLine.Length = initLen;
                // neighbourObject
                int neighObj = reader.GetInt32(IoSupportKey + i.ToString() + "_neighbourObject");

                // create support with reset line
                s = new Support(sLine);
                // rescale line and reassign
                sLine.Length = currLen;
                s.line = sLine;
                // assign remaining values
                //s.connected = connected;
                s.neighbourObject = neighObj;

                // add to object
                m_value.supports.Add(s);
            }


            // deserialize minSupports
            m_value.minSupports = reader.GetInt32(IoMinSupportsKey);

            // deserialize supported
            m_value.supported = reader.GetBoolean(IoSupportedKey);

            // deserialize absoluteZLock
            m_value.worldZLock = reader.GetBoolean(IoAbsZlockKey);

            // deserialize field values
            m_value.receiverValue = reader.GetDouble(IoReceiverValueKey);
            m_value.senderValue = reader.GetDouble(IoSenderValueKey);

            return true;
        }

        private string OccludeNeighboursToString(List<int[]> occludedNeighbours)
        {
            string neighbourString = "";

            for (int i = 0; i < occludedNeighbours.Count; i++)
            {
                neighbourString += occludedNeighbours[i][0].ToString() + "," + occludedNeighbours[i][1].ToString();
                if (i < occludedNeighbours.Count - 1) neighbourString += ";";
            }

            return neighbourString;
        }

        private List<int[]> OccludedNeighboursFromString(string neighbourString)
        {

            List<int[]> neighbours = new List<int[]>();

            if (!neighbourString.Equals(""))
            {
                string[] nSplit = neighbourString.Split(';');

                for (int i = 0; i < nSplit.Length; i++)
                {
                    string[] neigh = nSplit[i].Split(',');
                    neighbours.Add(new int[] { Convert.ToInt32(neigh[0]), Convert.ToInt32(neigh[1]) });
                }
            }
            return neighbours;
        }

        #endregion

        #region bake methods

        public bool IsBakeCapable
        {
            get { return m_value != null; }
        }

        public void BakeGeometry(RhinoDoc doc, List<Guid> obj_ids)
        {
            BakeGeometry(doc, new ObjectAttributes(), obj_ids);
        }

        public void BakeGeometry(RhinoDoc doc, ObjectAttributes att, List<Guid> obj_ids)
        {
            // bakes collisionMesh 
            obj_ids.Add(doc.Objects.AddMesh(m_value.collisionMesh));
            // bake reference plane as L polyline
            obj_ids.Add(doc.Objects.AddPolyline(PlaneToPoints(m_value.referencePlane)));
            // bake direction as line
            obj_ids.Add(doc.Objects.AddLine(m_value.referencePlane.Origin, m_value.referencePlane.Origin + m_value.direction));
            // bake  Handles as L polylines
            for (int i = 0; i < m_value.handles.Length; i++)
                obj_ids.Add(doc.Objects.AddPolyline(PlaneToPoints(m_value.handles[i].sender)));
        }

        Point3d[] PlaneToPoints(Plane p)
        {
            Point3d[] points = new Point3d[3];
            points[0] = p.Origin + p.XAxis;
            points[1] = p.Origin;
            points[2] = p.Origin + p.YAxis;

            return points;
        }

        #endregion

    }
}
