using System;
using System.Collections.Generic;

namespace Cordi.Configuration.External
{
    public class ExtraChatRoot
    {
        public Dictionary<ulong, ExtraChatConfigInfo>? Configs { get; set; }
    }

    public class ExtraChatConfigInfo
    {
        public string? Key { get; set; }
        public Dictionary<Guid, ExtraChatChannelInfo>? Channels { get; set; }

        public Dictionary<int, Guid>? ChannelOrder { get; set; }

        public Dictionary<string, Guid>? Aliases { get; set; }
        public Dictionary<Guid, string>? ChannelMarkers { get; set; }
    }

    public class ExtraChatChannelInfo
    {
        public string? Name { get; set; }
        // SharedSecret is byte[] in JSON but likely base64 string or object, 
        // we don't need it for mapping, so can ignore or treat as object.
    }
}
