using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using System.Numerics;
using System.Windows.Media;

namespace CsgoHackPlayground
{
    class Program
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        static int client;
        static int client_state;
        static int engine;

        public static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        public static int GetModule(string process, string moduleName)
        {
            if (Process.GetProcessesByName(process).Length > 0)
            {
                foreach (ProcessModule module in Process.GetProcessesByName(process)[0].Modules)
                {
                    if (module.ModuleName == (moduleName))
                    {
                        return (int)module.BaseAddress;
                    }

                }
            }
            return 0;
        }

        private static float IntegerToFloat(int Value)
        {
            return Value / 255f;
        }

        private static float HealthToPercent(int Value)
        {
            return Value / 100f;
        }

        private static Color HealthGradient(float Percent)
        {
            if (Percent < 0 || Percent > 1) { return new Color(); }

            int Red, Green;
            if (Percent < 0.5)
            {
                Red = 255;
                Green = (int)(255 * Percent);
            }
            else
            {
                Green = 255;
                Red = 255 - (int)(255 * (Percent - 0.5) / 0.5);
            }

            return Color.FromRgb((byte)Red, (byte)Green, 0);
        }

        public static float[] CalcLocalPos(int player)
        {
            float[] offset = Memory.ReadMatrix<float>(player + Offsets.netvars.m_vecViewOffset, 3);
            float[] origin = Memory.ReadMatrix<float>(player + Offsets.netvars.m_vecOrigin, 3);
            float[] local_pos = new float[3];
            local_pos[0] = offset[0] + origin[0];
            local_pos[1] = offset[1] + origin[1];
            local_pos[2] = offset[2] + origin[2];
            return local_pos;
        }

        public static float[] CalcAngle(float[] src, float[] dst)
        {
            float[] angles = new float[3];
            double[] delta = new double[3];
            delta[0] = src[0] - dst[0];
            delta[1] = src[1] - dst[1];
            delta[2] = src[2] - dst[2];
            double hyp = Math.Sqrt(delta[0] * delta[0] + delta[1] * delta[1]);
            angles[0] = (float)(Math.Asin(delta[2] / hyp) * 57.295779513082f);
            angles[1] = (float)(Math.Atan(delta[1] / delta[0]) * 57.295779513082f);
            angles[2] = 0.0f;
            if (delta[0] >= 0.0) { angles[1] += 180.0f; }
            return angles;
        }

        public static float deg2rad(float angle)
        {
            return (float)((Math.PI / 180) * angle);
        }

        public static float rad2deg(float radians)
        {
            return (float)(180 / Math.PI) * radians;
        }

        public static float[] Normalize(float[] angle)
        {
            if (angle[0] > 89) angle[0] = 89;
            if (angle[0] < -89) angle[0] = -89;
            while (angle[1] > 180) angle[1] -= 360;
            while (angle[1] < -180) angle[1] += 360;
            angle[2] = 0;
            return angle;
        }

        public static float CalcDistance(float lat1, float lon1, float lat2, float lon2)
        {
            if ((lat1 == lat2) && (lon1 == lon2))
            {
                return 0;
            }
            else
            {
                float theta = lon1 - lon2;
                float dist = (float)(Math.Sin(deg2rad(lat1)) * Math.Sin(deg2rad(lat2)) + Math.Cos(deg2rad(lat1)) * Math.Cos(deg2rad(lat2)) * Math.Cos(deg2rad(theta)));
                dist = (float)Math.Acos(dist);
                dist = rad2deg(dist);
                dist = (float)(dist * 60 * 1.1515);
                return (dist);
            }
        }

        public static bool isEntityInFov(float[] lViewAngles, float[] temp_angles_toAim)
        {
            float diffX = CalcDiffAngle(lViewAngles[0], temp_angles_toAim[0]);
            float diffY = CalcDiffAngle(lViewAngles[1], temp_angles_toAim[1]);

            return diffX < 2.5f && diffY < 2.5f;
        }

        public static float CalcDiffAngle(float alpha, float beta)
        {
            float phi = Math.Abs(beta - alpha) % 360;
            float distance = phi > 180 ? 360 - phi : phi;
            return distance;
        }

        public static float RandomFloat(Random random)
        {
            var buffer = new byte[4];
            random.NextBytes(buffer);
            return BitConverter.ToSingle(buffer, 0);
        }


        public static float[] Smooth(float[] lViewAngles, float[] dest, float amnt)
        {
            float[] delta = new float[] { dest[0] - lViewAngles[0], dest[1]- lViewAngles[1], 0f };
            delta = Normalize(delta);
            return new float[] {
                lViewAngles[0] + delta[0] / amnt,
                lViewAngles[1] + delta[1] / amnt,
                0f };
        }

        public static float[] CalcHeadPos(int entity)
        {
            int boneMatrix = Memory.ReadMemory<int>(entity + Offsets.netvars.m_dwBoneMatrix);
            float[] head_pos = new float[3];
            head_pos[0] = Memory.ReadMemory<float>(boneMatrix + 0x30 * 8 + 0x0C);
            head_pos[1] = Memory.ReadMemory<float>(boneMatrix + 0x30 * 8 + 0x1C);
            head_pos[2] = Memory.ReadMemory<float>(boneMatrix + 0x30 * 8 + 0x2C);
            return head_pos;
        }

        public static float[] GetClosestToCrosshair(int lplayer, List<int> ennemies_list)
        {
            float[] lViewAngles = Memory.ReadMatrix<float>(client_state + Offsets.signatures.dwClientState_ViewAngles, 3);
            float[] local_pos = CalcLocalPos(lplayer);
            float[] angles_toAim = new float[3];
            float closest_distance = float.MaxValue;

            foreach (int enn in ennemies_list)
            {
                float[] head_pos = CalcHeadPos(enn);

                float[] temp_angles_toAim = CalcAngle(local_pos, head_pos);


                float diffX = CalcDiffAngle(lViewAngles[0], temp_angles_toAim[0]);
                float diffY = CalcDiffAngle(lViewAngles[1], temp_angles_toAim[1]);
                float distance = diffX + diffY;

                if (distance < closest_distance)
                {
                    closest_distance = distance;
                    angles_toAim = temp_angles_toAim;
                }
            }

            return Smooth(lViewAngles, angles_toAim, 22f);
        }

        [STAThread]
        static void Main(string[] args)
        {
            Memory.Initialize("csgo");
            client = GetModule("csgo", "client.dll");
            engine = GetModule("csgo", "engine.dll");

            client_state = Memory.ReadMemory<int>(engine + Offsets.signatures.dwClientState);

            while (true) {
                int local_player = Memory.ReadMemory<int>(client + Offsets.signatures.dwLocalPlayer);

                //  AimBot
                if (local_player > 0 && GetActiveWindowTitle() == "Counter-Strike: Global Offensive" && ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left))
                {
                    float[] local_pos = CalcLocalPos(local_player);

                    int player_health = Memory.ReadMemory<int>(local_player + Offsets.netvars.m_iHealth);

                    if (player_health > 0)
                    {
                        List<int> ennemies_list = new List<int>();
                        int player_team = Memory.ReadMemory<int>(local_player + Offsets.netvars.m_iTeamNum);
                        float[] lViewAngles = Memory.ReadMatrix<float>(client_state + Offsets.signatures.dwClientState_ViewAngles, 3);

                        for (int i = 1; i < 64; i++)
                        {
                            int entity = Memory.ReadMemory<int>(client + Offsets.signatures.dwEntityList + i * 0x10);
                            if (entity > 0)
                            {
                                int ent_health = Memory.ReadMemory<int>(entity + Offsets.netvars.m_iHealth);
                                int entity_team_id = Memory.ReadMemory<int>(entity + Offsets.netvars.m_iTeamNum);
                                float[] ent_head_pos = CalcHeadPos(entity);
                                float[] temp_angles_toAim = CalcAngle(local_pos, ent_head_pos);
                                bool ent_dormant = Memory.ReadMemory<bool>(entity + Offsets.signatures.m_bDormant);
                                //bool isSpotted = Memory.ReadMemory<bool>(entity + Offsets.netvars.m_bSpotted);
                                if (ent_dormant) continue;
                                //if (!isSpotted) continue;
                                if (ent_health < 1) continue;
                                if (entity_team_id == player_team) continue;
                                if (!isEntityInFov(lViewAngles, temp_angles_toAim)) continue;
                                ennemies_list.Add(entity);
                            }
                        }

                        if (ennemies_list.Count > 0)
                        {
                            float[] angles_toAim = Normalize(GetClosestToCrosshair(local_player, ennemies_list));
                            Memory.WriteFloat(client_state + Offsets.signatures.dwClientState_ViewAngles, angles_toAim);
                        }
                    }
                }

                // AntiFlash
                if (local_player > 0)
                {
                    float flashDuration = Memory.ReadMemory<float>(local_player + Offsets.netvars.m_flFlashDuration);
                    float flashAlpha = Memory.ReadMemory<float>(local_player + Offsets.netvars.m_flFlashMaxAlpha);

                    if (flashDuration > 0f && flashAlpha == 255f)
                    {
                        Memory.WriteMemory<float>(local_player + Offsets.netvars.m_flFlashMaxAlpha, 0f);
                    }
                    else if (flashDuration == 0f && flashAlpha != 255f)
                    {
                        Memory.WriteMemory<float>(local_player + Offsets.netvars.m_flFlashMaxAlpha, 255f);
                    }
                }

                // Trigger Bot
                if (GetActiveWindowTitle() == "Counter-Strike: Global Offensive" && (Keyboard.IsKeyDown(Key.LeftAlt)))
                {
                    int entity_id = Memory.ReadMemory<int>(local_player + Offsets.netvars.m_iCrosshairId);
                    int entity = Memory.ReadMemory<int>(client + Offsets.signatures.dwEntityList + (entity_id - 1) * 0x10);

                    int entity_team = Memory.ReadMemory<int>(entity + Offsets.netvars.m_iTeamNum);
                    int player_team = Memory.ReadMemory<int>(local_player + Offsets.netvars.m_iTeamNum);

                    if (entity_id > 0 && entity_id <= 64 && player_team != entity_team && local_player > 0)
                    {
                        Thread.Sleep(60);
                        Memory.WriteMemory<int>(client + Offsets.signatures.dwForceAttack, 6);
                    }
                }

                // Bunny Hop
                if (GetActiveWindowTitle() == "Counter-Strike: Global Offensive" && (Keyboard.IsKeyDown(Key.Space)))
                {
                    int forceJump = client + Offsets.signatures.dwForceJump;

                    if (local_player > 0)
                    {
                        int on_ground = Memory.ReadMemory<int>(local_player + Offsets.netvars.m_fFlags);
                        if (on_ground == 256)
                        {
                            Memory.WriteMemory<int>(forceJump, 4);
                        } else
                        {
                            Memory.WriteMemory<int>(forceJump, 5);
                        }
                    }
                }

                // Wall Hack
                int glow_manager = Memory.ReadMemory<int>(client + Offsets.signatures.dwGlowObjectManager);
                Console.WriteLine("glow_manager: " + glow_manager);

                if (local_player != 0 && glow_manager != 0)
                {
                    int player_team_id = Memory.ReadMemory<int>(local_player + Offsets.netvars.m_iTeamNum);
                    int glow_count = Memory.ReadMemory<int>(client + Offsets.signatures.dwGlowObjectManager + 0x4);
                    for (int i = 1; i < glow_count; i++)
                    {
                        int entity = Memory.ReadMemory<int>(client + Offsets.signatures.dwEntityList + i * 0x10);
                        if (entity < 1) continue;
                        bool ent_dormant = Memory.ReadMemory<bool>(entity + Offsets.signatures.m_bDormant);
                        if (ent_dormant) continue;

                        bool isSpotted = Memory.ReadMemory<bool>(entity + Offsets.netvars.m_bSpotted);

                        int entity_team_id = Memory.ReadMemory<int>(entity + Offsets.netvars.m_iTeamNum);
                        int entity_glow_index = Memory.ReadMemory<int>(entity + Offsets.netvars.m_iGlowIndex);

                        if (entity_team_id != player_team_id && isSpotted)
                        {
                            int ent_health = Memory.ReadMemory<int>(entity + Offsets.netvars.m_iHealth);
                            var Gradient = HealthGradient(HealthToPercent(ent_health));
                            float Red = IntegerToFloat(Gradient.R);
                            float Green = IntegerToFloat(Gradient.G);
                            float Blue = IntegerToFloat(Gradient.B);
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0x4, Red);
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0x8, Green);
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0xC, Blue);
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0x10, 0.8f);
                            Memory.WriteMemory<int>(glow_manager + entity_glow_index * 0x38 + 0x24, 1);
                            Memory.WriteMemory<int>(glow_manager + entity_glow_index * 0x38 + 0x25, 0);
                        } else if (entity_team_id != player_team_id && !isSpotted)
                        {
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0x4, 255f);
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0x8, 255f);
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0xC, 255f);
                            Memory.WriteMemory<float>(glow_manager + entity_glow_index * 0x38 + 0x10, 0.8f);
                            Memory.WriteMemory<int>(glow_manager + entity_glow_index * 0x38 + 0x24, 1);
                            Memory.WriteMemory<int>(glow_manager + entity_glow_index * 0x38 + 0x25, 0);
                        }
                    }
                }

                Thread.Sleep(1);
            }
        } 
    }
}