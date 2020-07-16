using System;
using System.IO;
using Mono.Cecil;
using System.Text;
using System.Linq;

static class TestProgramCreator
{
        static void Main(string[] args)
        {
            var outputPath = args[0];

            
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(args[1]);
            
            var builder = new StringBuilder();

            builder.AppendLine("using System;");
            builder.AppendLine();

            builder.AppendLine("namespace Unity.IL2CPP.IntegrationTests.Tiny");
            builder.AppendLine("{");
            builder.AppendLine("    class Program");
            builder.AppendLine("    {");
            builder.AppendLine("        static int Main()");
            builder.AppendLine("        {");
            builder.AppendLine("            Console.WriteLine(\"<?xml version=\\\"1.0\\\" encoding=\\\"UTF-8\\\"?>\");");
            builder.AppendLine("            Console.WriteLine(\"<TestFixtureResult>\");");
            builder.AppendLine("            Console.WriteLine(\"<Tests>\");");

            var allTestMethods = assemblyDefinition.MainModule.Types.SelectMany(t => t.Methods.Where(IsTestMethod));
            
            foreach (var testMethod in allTestMethods)
            {
                var testName = testMethod.DeclaringType.FullName + "::" + testMethod.Name;
                builder.AppendLine($"            Console.WriteLine(\"<TestResult Name='{testName}'>\");");
                builder.AppendLine($"            Console.WriteLine(\"<![CDATA[\");");

                if (testMethod.IsStatic)
                {
                    builder.AppendLine($"            {testMethod.DeclaringType.FullName}.{testMethod.Name}();");
                }
                else
                {
                    builder.AppendLine($"            new {testMethod.DeclaringType.FullName}().{testMethod.Name}();");
                }

                builder.AppendLine($"            Console.WriteLine(\"]]>\");");
                builder.AppendLine($"            Console.WriteLine(\"</TestResult>\");");
            }

            builder.AppendLine("            Console.WriteLine(\"</Tests>\");");
            builder.AppendLine("            Console.WriteLine(\"</TestFixtureResult>\");");
            builder.AppendLine("            return 0;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");

            File.WriteAllText(outputPath, builder.ToString());
        }

        private static bool IsTestMethod(MethodDefinition method)
        {
            if (!method.CustomAttributes.Any(a => a.AttributeType.FullName == "Unity.IL2CPP.IntegrationTests.Tiny.TestAttribute"))
                return false;

            if (method.CustomAttributes.Any(a => a.AttributeType.FullName == "Unity.IL2CPP.IntegrationTests.Tiny.IgnoreAttribute"))
                return false;

            return true;
        }
    }