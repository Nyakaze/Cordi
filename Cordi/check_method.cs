using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(@"F:\Projects\Cordi\Cordi\bin\x64\Debug\net10.0-windows\Vortice.DXGI.dll");
        var type = asm.GetType("Vortice.DXGI.IDXGIKeyedMutex");
        foreach (var m in type.GetMethods())
        {
            if (m.Name == "AcquireSync")
            {
                Console.WriteLine($"Return Type: {m.ReturnType.Name}");
            }
        }
    }
}
