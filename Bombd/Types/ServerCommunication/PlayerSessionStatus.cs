using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bombd.Types.ServerCommunication
{
    public class PlayerSessionStatus
    {
        public Guid SessionID { get; set; }
        public bool IsAuthorized { get; set; }
    }
}
