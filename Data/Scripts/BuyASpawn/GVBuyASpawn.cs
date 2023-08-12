using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Components;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;


namespace Klime.BuyASpawn
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class BuyASpawn : MySessionComponentBase
    {
        string panel_subtype = "BuyASpawnButton";
        MyIni ini = new MyIni();
        Dictionary<int, string> codes = new Dictionary<int, string>
        {
            [0] = "Unspecified Error\nPlease try again",
            [1] = "Error:\nIncorrect Config",
            [2] = "Error:\nNot enough space",
            [3] = "Error:\nNot enough SC",
            [4] = "Success!\nGrid Spawned",
            [5] = "Error:\nPlease wait\nto spawn again."
        };

        Dictionary<long, DateTime> lastUsed = new Dictionary<long, DateTime>();

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.ButtonPressedTerminalName += Pressed;
            }
        }

        private void Pressed(string name, int button, long playerId, long blockId)
        {
            IMyButtonPanel panel = MyAPIGateway.Entities.GetEntityById(blockId) as IMyButtonPanel;
            IMyPlayer player = GetPlayer(playerId);

            if (panel != null && player != null && panel.CubeGrid.Physics != null)
            {
                if (panel.BlockDefinition.SubtypeName == panel_subtype)
                {
                    int result = TrySpawn(panel, player);

                    var ig_panel = panel as SpaceEngineers.Game.ModAPI.Ingame.IMyButtonPanel;
                    var provider = ig_panel as Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider;
                    if (provider != null)
                    {
                        var surface = provider.GetSurface(0);

                        if (surface != null)
                        {
                            surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                            surface.FontSize = 2.0f;
                            surface.TextPadding = 30f;
                            surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER;
                            surface.WriteText(codes[result], false);
                        }
                    }
                }
            }
        }
		Random rand = new Random();
        private int TrySpawn(IMyButtonPanel panel, IMyPlayer player)
        {
            int result = 0;

            try
            {
                if (ini.TryParse(panel.CustomData))
                {
                    string grid_name = ini.Get("Grid Info", "Name").ToString();
                    long cost = ini.Get("Grid Info", "Cost").ToInt64();
                    string gps_pos = ini.Get("Position", "GPSPaste").ToString();
                    bool gravity = ini.Get("Position", "Gravity").ToBoolean();
                    int cooldown = ini.Get("Grid Info", "Cooldown").ToInt32();

                    if (!string.IsNullOrWhiteSpace(grid_name) && !string.IsNullOrWhiteSpace(gps_pos) && cost >= 0)
                    {
                        Vector3D spawn_pos = Vector3D.Zero;
                        if (ParseVector3DFromGPS(gps_pos, out spawn_pos))
                        {
                            var dist = 70;
							List<MyEntity> ents = new List<MyEntity>();
							spawn_pos += Vector3D.Right * (rand.Next(2*dist)-dist);
							spawn_pos += Vector3D.Forward * (rand.Next(2*dist)-dist);
							spawn_pos += Vector3D.Up * (rand.Next(2*dist)-dist);
							BoundingSphereD sphere = new BoundingSphereD(spawn_pos, 10);
                            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, ents);

                            bool allowed = true;
                            foreach (var ent in ents)
                            {
                                if ((ent is MyCubeGrid || ent is IMyCharacter) && ent.Physics != null)
                                {
                                    allowed = false;
                                    break;
                                }
                            }
                            if (allowed)
                            {
                                long money = 0;
                                if (player.TryGetBalanceInfo(out money))
                                {
                                    if (money >= cost)
                                    {
                                        DateTime lastUse;
                                        if (!lastUsed.TryGetValue(player.IdentityId, out lastUse) || (DateTime.Now - lastUse).TotalMinutes >= cooldown)
                                        {
                                            player.RequestChangeBalance(-1 * cost);
                                            if (gravity)
                                            {
                                                MyVisualScriptLogicProvider.SpawnPrefabInGravity(grid_name, spawn_pos, panel.WorldMatrix.Backward, player.IdentityId);
                                            }
                                            else
                                            {
                                                MyVisualScriptLogicProvider.SpawnPrefab(grid_name, spawn_pos, panel.WorldMatrix.Backward, panel.WorldMatrix.Up, player.IdentityId);
                                            }
                                            lastUse = DateTime.Now;
                                            if (!lastUsed.ContainsKey(player.IdentityId)) lastUsed.Add(player.IdentityId, lastUse);
                                            else lastUsed[player.IdentityId] = lastUse;
                                            result = 4; //Spawned
                                        }
                                        else
                                        {
                                            codes[5] = $"Error:\nPlease wait {Math.Max(1, cooldown-(DateTime.Now - lastUse).TotalMinutes):N0} min\nto spawn again.";
                                            result = 5;
                                        }
                                    }
                                    else
                                    {
                                        result = 3;
                                    }
                                }
                                else
                                {
                                    result = 3;
                                }
                            }
                            else
                            {
                                result = 2;
                            }
                        }
                        else
                        {
                            result = 1;
                        }
                    }
                    else
                    {
                        result = 1; //Incorrect Config
                    }
                }
                else
                {
                    result = 1; //Incorrect Config
                }

            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("", e.StackTrace);
            }


            return result;
        }

        public T CastProhibit<T>(T ptr, object val) => (T)val;

        public bool ParseVector3DFromGPS(string gps, out Vector3D vec)
        {
            vec = Vector3D.Zero;

            if (!gps.StartsWith("GPS:"))
            {
                return false;
            }

            string[] segments = gps.Split(':');

            if (segments.Length < 6) // Because terminated with a colon
            {
                return false;
            }

            if (!double.TryParse(segments[2], out vec.X) || !double.TryParse(segments[3], out vec.Y) || !double.TryParse(segments[4], out vec.Z))
            {
                return false;
            }

            return true;
        }

        private IMyPlayer GetPlayer(long playerId)
        {
            IMyPlayer return_player = null;

            List<IMyPlayer> all_players = new List<IMyPlayer>();
            MyAPIGateway.Multiplayer.Players.GetPlayers(all_players);

            foreach (var p in all_players)
            {
                if (p.IdentityId == playerId)
                {
                    return_player = p;
                    break;
                }
            }

            return return_player;
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.ButtonPressedTerminalName -= Pressed;
            }
        }
    }
}