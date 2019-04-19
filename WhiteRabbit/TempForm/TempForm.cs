using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using WhiteRabbit.Framework;


namespace WhiteRabbit.TempForm
{
    public partial class TempForm : Form
    {
        public class Form1 : TerrainForm.TerrainForm
        {
        }

        public class Form2 : TerrainForm.TerrainForm
        {
            
        }

        public TempForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Form1 form1 = new Form1();
            form1.Initialize();
            form1.Run();
        }
    }
}
