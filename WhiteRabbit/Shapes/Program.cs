namespace WhiteRabbit.Shapes
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (var app = new Shapes())
            {
                app.Initialize();
                app.Run();
            }
        }
    }
}
