using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bombd.Types.ServerCommunication
{
    public class Message
    {
        public string Type { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
