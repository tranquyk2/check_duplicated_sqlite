using HidSharp;

namespace Scanner
{
    public static class PatliteGreen
    {
        private const int VID = 0x191A;
        private const int PID = 0x8003;

        private static void Send(byte[] cmd)
        {
            var device = DeviceList.Local.GetHidDeviceOrNull(VID, PID);
            if (device == null)
            {
                return;
            }

            using (var stream = device.Open())
            {
                byte[] data = new byte[cmd.Length + 1];
                data[0] = 0x00; // HID Report ID
                Array.Copy(cmd, 0, data, 1, cmd.Length);
                stream.Write(data);
            }
        }

        private static readonly byte[] CMD_GREEN_ON =
            { 0x00,0x00,0xF0,0x00,0x1F,0xFF,0xF0,0x00 };

        private static readonly byte[] CMD_RESET =
            { 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00 };

       
        public static void TurnOn()
        {
            Send(CMD_GREEN_ON);
        }

        public static void TurnOff()
        {
            Send(CMD_RESET);
        }
    }
}

