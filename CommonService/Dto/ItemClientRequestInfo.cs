using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Dto
{
    public class ItemClientRequestInfo
    {
        public string RemoteEndpointName { get; set; } = string.Empty;
        public string LocalEndpointName { get; set; } = string.Empty;

        public int RemotePort { get; set; }
        public int LocalPort { get; set; }

        public List<string> ChildrenClient { get; set; } = new List<string>();
    }
}
