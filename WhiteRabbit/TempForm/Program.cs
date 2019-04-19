using System;
using System.Windows.Forms;

namespace WhiteRabbit.TempForm
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (var app = new TempForm())
            {
                app.Show();
                while (app.Created)
                {
                    Application.DoEvents();
                }
            }
        }
    }
}
