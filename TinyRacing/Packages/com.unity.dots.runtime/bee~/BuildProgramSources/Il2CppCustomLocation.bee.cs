using System;

public static class Il2CppCustomLocation
{
    public static string CustomLocation
    {
        get
        {
            string path = null; // Set local il2cpp path here
            string envPath = Environment.GetEnvironmentVariable("IL2CPP_FROM_LOCAL"); // Set in CI for testing
            return path ?? envPath;
        }
    }
}