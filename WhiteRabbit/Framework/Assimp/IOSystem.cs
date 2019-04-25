using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhiteRabbit.Framework.Assimp
{
    public abstract class IOSystem
    {
        public IOSystem()
        {
        }

        public abstract bool Exists(string file);

        public abstract Stream Open(string file, FileMode mode = FileMode.Open);

        public abstract void Close(Stream file);

        public virtual bool ComparePaths(string one, string second)
        {
            return string.Compare(one, second) == 0;
        }
    }
}
