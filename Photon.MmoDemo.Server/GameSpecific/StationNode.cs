using System.Collections.Generic;
using Photon.MmoDemo.Common;

public enum Direction
{
    North = 1,
    East = 2,
    South = 3,
    West = 4
}

public struct PortBuilding
{
    public Direction dir;
    public HorizontalBuildingType btype;

}

public class StationBuilding
{
    public BuildingType type;
    public List<PortBuilding> portsUsed;   // ports that are already used and cannot be further connected
    public Vector pos;
    public StationNode parentNode;

    //public StationBuilding()
    //{
    //    type = BuildingType.MainConnector;
    //    portsUsed = new List<Tuple<Direction, StationBuilding>>();
    //}

    //public StationBuilding(BuildingType type, Vector3 pos)
    //{
    //    this.type = type;
    //    this.pos = pos;
    //    portsUsed = new List<Tuple<Direction, StationBuilding>>();
    //}

    public StationBuilding(BuildingType type, Vector pos, StationNode parent)
    {
        this.type = type;
        this.pos = pos;
        portsUsed = new List<PortBuilding>();
        parentNode = parent;
    }

    public bool IsPortUsed(Direction dir)
    {
        foreach (var port in portsUsed)
        {
            if (port.dir == dir)
                return true;
        }
        return false;
    }

    public HorizontalBuildingType GetPortConnectorType(Direction dir)
    {
        foreach (var port in portsUsed)
        {
            if (port.dir == dir)
                return port.btype;
        }
        return HorizontalBuildingType.None;
    }
}


// a station node contains a vertical tower of buildings
public class StationNode 
{



    LinkedList<StationBuilding> buildings;  // first is top, last is bottom
    LinkedList<StationBuilding> hubBuildings;
    //int topLevel;   // current height of top most building--- dont need anymore we now use SpaceStation.maxHeightTop to limit height
    private int maxTop = 2;
    public StationBuilding selectedHub;
    public bool IsBridgeCovered;    //  if this is true then this node isn't a building, its a special case
                                    // where a bridge from another node crosses over this node in the grid
                                    // needed to tell if a spot on the grid is occupied

    public StationNode()
    {
        buildings = new LinkedList<StationBuilding>();
        hubBuildings = new LinkedList<StationBuilding>();
        selectedHub = null;
        IsBridgeCovered = false;
    }

    // hubs used port is the occupied port for this newly initialized node's hub.
    public void Initialize(bool isBigHub, PortBuilding hubsUsedPort, Vector pos)
    {
        Initialize(isBigHub, pos);
        selectedHub.portsUsed.Add(hubsUsedPort);
    }

    // use this initialize for the first node of a space station which has no hubports used
    public void Initialize(bool isBigHub, Vector pos)
    {
        if (isBigHub)
        {
            selectedHub = new StationBuilding(BuildingType.MainConnector, pos, this);
        }
        else
        {
            selectedHub = new StationBuilding(BuildingType.SmallDisk, pos, this);

        }

        buildings.AddFirst(selectedHub);
        hubBuildings.AddFirst(selectedHub);
    }

    public bool IsEmpty() { return buildings.Count == 0; }

    public float? GetBottomHeight()
    {
        if (buildings.Last != null)
            return buildings.Last.Value.pos.y;
        return null;
    }

    public float? GetTopHeight()
    {
        if (buildings.First != null)
            return buildings.First.Value.pos.y;
        return null;
    }

    public bool CanAddBuilding(bool top, BuildingType btype, Vector buildingPos, float newBuildingHeight)
    {
        if (top)
        {
            if (buildingPos.y + newBuildingHeight > SpaceStation.maxHeightTop)
                return false;
        }
        else
        {
            if (buildingPos.y - newBuildingHeight < SpaceStation.maxHeightBtm)
                return false;
        }
        return true;
    }

    public bool AddBuilding(bool top, BuildingType btype)
    {
        float newBuildingHeight = SpaceStation.buildingHeightTable[btype];
        if (top)
        {
            Vector buildingPos = buildings.First.Value.pos;
            if (!CanAddBuilding(top, btype, buildingPos, newBuildingHeight))
                return false;
            buildingPos.y += newBuildingHeight + SpaceStation.buildingHeightTable[buildings.First.Value.type] / 2;
            StationBuilding newBuilding = new StationBuilding(btype, buildingPos, this);

            buildings.AddFirst(newBuilding);
            if (SpaceStation.IsHub(btype))
                hubBuildings.AddFirst(newBuilding);
        }
        else
        {
            Vector buildingPos = buildings.Last.Value.pos;
            if (!CanAddBuilding(top, btype, buildingPos, newBuildingHeight))
                return false;
            buildingPos.y -= newBuildingHeight + SpaceStation.buildingHeightTable[buildings.Last.Value.type] / 2;
            StationBuilding newBuilding = new StationBuilding(btype, buildingPos, this);
            buildings.AddLast(newBuilding);
            if (SpaceStation.IsHub(btype))
                hubBuildings.AddLast(newBuilding);
        }
        return true;

    }

    public StationBuilding GetBuildingTypeAtHeight(float height)
    {
        // remember positions of buildings are measured from their ceiling, so a building
        // is at a certain height if its less than pos.y but greater than pos.y-buildingheight
        foreach (var building in buildings)
        {
            if (height < building.pos.y && height > building.pos.y - SpaceStation.buildingHeightTable[building.type])
                return building;
        }
        return null;
    }

    public List<PortBuilding> GetPortsUsedOnSelectedHub()
    {
        return selectedHub.portsUsed;
    }

    public bool CanSelectUp()
    {
        if (hubBuildings.Find(selectedHub).Previous == null)
            return false;
        return true;
    }

    public bool CanSelectDown()
    {
        if (hubBuildings.Find(selectedHub).Next == null)
            return false;
        return true;
    }
    // returns false if no hub is available going up
    public bool SelectNextHubUp()
    {
        if (CanSelectUp())
            return false;
        else
            selectedHub = hubBuildings.Find(selectedHub).Previous.Value;
        return true;
    }


    // returns false if no hub is available going down
    public bool SelectNextHubDown()
    {
        if (CanSelectDown())
            return false;
        else
            selectedHub = hubBuildings.Find(selectedHub).Next.Value;
        return true;
    }

    // user of function ensures that correct bridge type is used for the building
    public bool BuildBridgeOnSelectedHub(Direction dir, HorizontalBuildingType connectedNodeType)
    {
        if (selectedHub.IsPortUsed(dir))
            return false;

        PortBuilding newBridge = new PortBuilding();
        newBridge.dir = dir;
        newBridge.btype = connectedNodeType;
        selectedHub.portsUsed.Add(newBridge);
        return true;
    }

    // if we build a hBuildingType building from the currently selected hub in the direction dir
    // return the position we end up at
    public Vector GetNextNodePosFromSelectedHub(Direction dir, HorizontalBuildingType hBuildingType)
    {
        float bridgeLen;
        if (hBuildingType == HorizontalBuildingType.LargeBridge || hBuildingType == HorizontalBuildingType.SmallBridge)
            bridgeLen = SpaceStation.bigBridgeLength;
        else if (hBuildingType == HorizontalBuildingType.SmallBridgeDouble || hBuildingType == HorizontalBuildingType.LargeBridgeDouble)
            bridgeLen = SpaceStation.bigBridgeLength * 2;
        else //if (hBuildingType == HorizontalBuildingType.Turret)
            return Vector.Zero;

        if (selectedHub.IsPortUsed(dir))
        {
            return selectedHub.pos + SpaceStation.GetDisplacementVector(dir, bridgeLen);
        }
        return Vector.Zero;
    }

    public Vector GetBridgePosFromPort(Direction dir, HorizontalBuildingType btype)
    {
        float bridgeLen;
        if (btype == HorizontalBuildingType.LargeBridge || btype == HorizontalBuildingType.SmallBridge)
            bridgeLen = SpaceStation.smBridgeLength;
        else if (btype == HorizontalBuildingType.SmallBridgeDouble || btype == HorizontalBuildingType.LargeBridgeDouble)
            bridgeLen = SpaceStation.bigBridgeLength;
        else //if (hBuildingType == HorizontalBuildingType.Turret)
            return Vector.Zero;

        Vector ans = Vector.Zero;
        ans = selectedHub.pos + SpaceStation.GetDisplacementVector(dir, bridgeLen / 2);
        ans.y -= SpaceStation.buildingHeightTable[BuildingType.MainConnector] / 2;
        return ans;
    }

    public PortBuilding? GetPortBuilding(Direction dir)
    {
        foreach (PortBuilding building in selectedHub.portsUsed)
        {
            if (building.dir == dir)
                return building;
        }
        return null;
    }


    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
