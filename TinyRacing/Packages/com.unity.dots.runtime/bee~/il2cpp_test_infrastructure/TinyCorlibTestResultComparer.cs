using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

static class TinyCorlibTestResultComparer
{
    static int Main(string[] args)
    {
        var dotNetResults = LoadResultsFrom(args[0]);
        var il2cppResults = LoadResultsFrom(args[1]);

        int failures = 0;
        foreach(var test in dotNetResults)
        {
            var testName = NameOfTest(test);
            var match = il2cppResults.Single(m => NameOfTest(m) == testName);

            var dotnetContent = ContentsOf(test);
            var il2cpPContent = ContentsOf(match);

            if (dotnetContent != il2cpPContent)
            {
                failures++;
                Console.WriteLine($"{testName} has different results");
                Console.WriteLine("DOTNET:");
                Console.WriteLine(dotnetContent);
                Console.WriteLine("IL2CPP:");
                Console.WriteLine(il2cpPContent);
            }
        }
        
        if (failures == 0)
            File.WriteAllText(args[2], "success");
        
        Console.WriteLine($"{dotNetResults.Length - failures} passed, {failures} failed.");
        
        return failures;
    }

    private static string ContentsOf(XElement test)
    {
        return test.DescendantNodes().OfType<XCData>().Single().Value;
    }

    private static string NameOfTest(XElement test)
    {
        return test.Attribute("Name").Value;
    }

    private static XElement[] LoadResultsFrom(string file)
    {
        return XElement.Load(file).DescendantNodes().ToArray().OfType<XElement>().Where(e => e.Name == "TestResult")
            .ToArray();
    }
    
    
}