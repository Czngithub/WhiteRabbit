namespace WhiteRabbit.TerrainForm
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (var app = new TerrainForm())
            {
                app.Initialize();
                app.Run();
            }
        }
    }
}