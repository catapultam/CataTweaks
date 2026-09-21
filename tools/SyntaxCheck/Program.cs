// Parses the mod sources and reports syntax errors.
//
// The mod cannot be compiled without the game: it references Assembly-CSharp and eleven
// Unity assemblies from the Terra Invicta install, which are proprietary and are not in
// this repository. Roslyn can still parse the files. That catches unbalanced braces and
// broken string literals, which is what scripted edits to these files tend to produce.
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Paths resolve against the working directory. CI runs dotnet run from the repo root, and so
// does any local invocation of that same command.
int failures = 0;


foreach (string name in args)
{
    string path = Path.GetFullPath(name);
    if (!File.Exists(path))
    {
        Console.WriteLine($"FAIL {name} not found at {path}");
        failures++;
        continue;
    }

    SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path),
        new CSharpParseOptions(LanguageVersion.Latest), path);
    Diagnostic[] errors = tree.GetDiagnostics()
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .ToArray();

    foreach (Diagnostic error in errors)
    {
        FileLinePositionSpan span = error.Location.GetLineSpan();
        Console.WriteLine($"FAIL {name}({span.StartLinePosition.Line + 1}): {error.GetMessage()}");
    }
    failures += errors.Length;
    Console.WriteLine($"{name}: {errors.Length} errors");
}

return failures == 0 ? 0 : 1;
