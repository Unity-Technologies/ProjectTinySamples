using System;

namespace Unity.IL2CPP.IntegrationTests.Tiny
{
    public class TestAttribute : Attribute
    {
    }

    public class IgnoreAttribute : Attribute
    {
        public IgnoreAttribute(string msg)
        {
        }
    }
    
    public class ExcludeClrAttribute : Attribute
    {
        public ExcludeClrAttribute(Clr cl, string msg)
        {
            
        }
    }

    public enum Clr
    {
        Mono
    }
}
