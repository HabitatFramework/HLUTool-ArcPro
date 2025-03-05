using System;
using System.IO;
using System.Reflection;

namespace HLU;

public class AddInHelper
{
    public static string GetAddInLocation()
    {
        // Get the full path of the executing assembly (DLL inside the .esriAddinX package)
        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        // Get the directory where the add-in is loaded from
        string addInDirectory = Path.GetDirectoryName(assemblyPath);

        return addInDirectory;
    }
}
