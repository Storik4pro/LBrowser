using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinesBrowser
{
    public struct PointerPacket
    {
        public double px;
        public double py;
        public uint id;
    }

    public struct TextPacket
    {
        public TextPacketType PType;
        public string text;
    }

    public struct CommPacket
    {
        public PacketType PType;
        public string JSONData;
    }

    public enum TextPacketType
    {
        NavigatedUrl,
        TextInputContent,
        TextInputSend,
        TextInputCancel,
        LoadingStateChanged,
        OpenPages,
        EditOpenTabTitle,
        IsClientCanSendGoBackRequest,
        IsClientCanSendGoForwardRequest,
    }

    public enum PacketType
    {
        Navigation,
        SizeChange,
        TouchDown,
        TouchUp,
        TouchMoved,
        ACK,
        Frame,
        TextInputSend,
        NavigateForward,
        NavigateBack,
        SendKey,
        RequestFullPageScreenshot,
        ModeChange,
        SetActivePage,
        GetTabsOpen,
        CloseTab,
        RequestTabScreenshot,
        OpenUrlInNewTab,
        NewScreenShotRequest,
        IsCanGoBack,
        IsCanGoForward,
        SendKeyCommand,
        SendChar,
    }
    public struct DiscoveryPacket
    {
        public DiscoveryPacketType PType;
        public string ServerAddress;
    }
    public enum DiscoveryPacketType
    {
        AddressRequest,
        ACK
    }
    public class KeyCharPacket
    {
        public PacketType PType { get; set; } = PacketType.SendChar;
        public string JSONData { get; set; }
        public bool Shift { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public string Layout { get; set; }
    }
}
