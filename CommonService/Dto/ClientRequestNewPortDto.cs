using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Dto
{
    public class ClientRequestNewPortDto
    {
        public int LocalPort { get; set; }
        public int RequestPort { get; set; }
    }
}
