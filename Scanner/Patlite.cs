using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace Scanner
{
    // Helper to send simple commands to a PATLITE HID device.
    // This class is defensive: failures are swallowed so UI won't crash if device not present.
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
                    // Prepend report ID 0x00
                    byte[] data = new byte[cmd.Length + 1];
                    data[0] = 0x00;
                    Array.Copy(cmd, 0, data, 1, cmd.Length);
                    stream.Write(data);
                }
            }
            catch
            {
                // ignore errors (device may be unplugged)
            }
        }

        // Fire a short alert: red LED + buzzer then reset
        public static Task AlertDuplicateAsync(int durationMs = 800)
        {
            return Task.Run(() =>
            {
                // 8 byte commands (same as example)
                byte[] CMD_RED_ON  = {0x00,0x00,0x0F,0x00,0x1F,0xFF,0xF0,0x00};
                byte[] CMD_BUZ_ON  = {0x00,0x00,0x02,0xEF,0xFF,0xFF,0xF0,0x00};
                byte[] CMD_RESET   = {0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00};

                try
                {
                    // turn red LED on
                    Send(CMD_RED_ON);
                    // turn buzzer on
                    Send(CMD_BUZ_ON);

                    Thread.Sleep(durationMs);

                    // reset
                    Send(CMD_RESET);
                }
                catch
                {
                    // swallow any exception
                }
            });
        }
    }
}
