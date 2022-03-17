using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Klime.BuyASpawnGameLogic
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ButtonPanel), false, "BuyASpawnButton")]
    public class BuyASpawnGameLogic : MyGameLogicComponent
    {
        private IMyButtonPanel panel;
        MyIni ini = new MyIni();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                panel = Entity as IMyButtonPanel;
                NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (panel.CubeGrid.Physics != null)
            {
                if (string.IsNullOrWhiteSpace(panel.CustomData))
                {
                    ini.Set("Grid Info", "Name", "NAME_HERE");
                    ini.Set("Grid Info", "Cost", (long)0);
                    ini.Set("Grid Info", "Cooldown", 0);
                    ini.Set("Position", "GPSPaste", "GPS_PASTE_HERE");
                    ini.Set("Position", "Gravity", false);
                    panel.CustomData = ini.ToString();
                }
            }
        }

        public override void Close()
        {

        }
    }
}