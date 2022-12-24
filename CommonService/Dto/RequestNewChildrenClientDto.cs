using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Dto
{
    public class RequestNewChildrenClientDto
    {
        public string ServerChildrentEnpoint { get; set; } = string.Empty;
        public int ServerChildrentPort { get; set; }

        public string ClientChildrentEnpoint { get; set; } = string.Empty;
        public int ClientChildrentPort { get; set; }

        public int ServerPort { get; set; }
    }
}
