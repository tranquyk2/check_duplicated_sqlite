using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace Scanner
{
    
    public static class Patlite
    {
        const int VID = 0x191A;
        const int PID = 0x8003;

        private static void Send(byte[] cmd)
        {
            try
            {
                var deviceList = DeviceList.Local;
                var dev = deviceList.GetHidDeviceOrNull(VID, PID);
                if (dev == null)
                {
                    return;
                }
                using (var stream = dev.Open())
                {
                    
                    byte[] data = new byte[cmd.Length + 1];
                    data[0] = 0x00;
                    Array.Copy(cmd, 0, data, 1, cmd.Length);
                    stream.Write(data);
                }
            }
            catch
            {
                
            }
        }

        
        public static Task AlertDuplicateAsync(int durationMs = 800)
        {
            return Task.Run(() =>
            {
                
                byte[] CMD_RED_ON  = {0x00,0x00,0x0F,0x00,0x1F,0xFF,0xF0,0x00};
                byte[] CMD_BUZ_ON  = {0x00,0x00,0x02,0xEF,0xFF,0xFF,0xF0,0x00};
                byte[] CMD_RESET   = {0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00};

                try
                {
                    
                    Send(CMD_RED_ON);
                    
                    Send(CMD_BUZ_ON);

                    Thread.Sleep(durationMs);


                    Send(CMD_RESET);
                }
                catch
                {
                    
                }
            });
        }
    }
}
