using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhiteRabbit.Framework.Assimp
{
    public class DeadlyImportError : Exception
    {
        public DeadlyImportError(string errorText)
            : base(errorText)
        {
        }
    }
}
