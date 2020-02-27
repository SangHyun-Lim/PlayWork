using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsgFileMultSocketServer
{
    class FileData
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string Guid { get; set; }

        public FileData(string name, long size, string guid)
        {
            this.Name = name;
            this.Size = size;
            this.Guid = guid;
        }
    }
}
