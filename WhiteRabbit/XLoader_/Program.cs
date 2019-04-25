using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhiteRabbit.XLoader_
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (var app = new XLoader_())
            {
                app.Initialize();
                app.Run();
            }
        }
    }
}
