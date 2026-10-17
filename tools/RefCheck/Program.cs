// Checks a built CataTweaks.dll against the game assemblies of one or more game versions.
//
// One DLL ships for every supported game version, but it compiles against one of them. A game
// method that another version renames or changes then fails only when the game runs it, with
// MissingMethodException. This tool reads the DLL without running it and resolves, against each
// version's TerraInvicta_Data/Managed folder:
// - every type, method and field the DLL references,
// - every member found by name, the way Harmony finds it: a [HarmonyPatch] target must be declared
//   on its type, an AccessTools lookup may be on a base type, each kind (method, field, property)
//   only as that kind, and a method named with no parameter types must have no overload. A name
//   inside TargetMethods is paired with that method's one AccessTools call.
// A member that is found at run time some other way (by shape, or in another mod) is not seen.
//
// Usage: RefCheck <CataTweaks.dll> <Managed folder> [<Managed folder> ...]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

if (args.Length < 2)
{
    Console.WriteLine("usage: RefCheck <CataTweaks.dll> <Managed folder> [<Managed folder> ...]");
    return 2;
}

int failures = 0;
foreach (string managed in args.Skip(1))
{
    if (!File.Exists(Path.Combine(managed, "Assembly-CSharp.dll")))
    {
        Console.WriteLine($"FAIL {managed}: no Assembly-CSharp.dll");
        failures++;
        continue;
    }
    var resolver = new AssemblyResolver { EnableTypeDefCache = true, FindExactMatch = false };
    resolver.PreSearchPaths.Add(managed);
    resolver.PreSearchPaths.Add(Path.Combine(managed, "UnityModManager"));
    var context = new ModuleContext(resolver);
    resolver.DefaultModuleContext = context;
    using ModuleDefMD module = ModuleDefMD.Load(args[0], context);

    var missing = new SortedSet<string>();
    foreach (TypeRef type in module.GetTypeRefs())
        if (type.Resolve() == null)
            missing.Add("type " + type.FullName);
    foreach (MemberRef member in module.GetMemberRefs())
    {
        // A member of a type that is missing is reported once, as the type.
        if (member.DeclaringType?.ScopeType?.ResolveTypeDef() == null) continue;
        if ((member.IsMethodRef ? member.ResolveMethod() : (IMemberDef)member.ResolveField()) == null)
            missing.Add((member.IsMethodRef ? "method " : "field ") + member.FullName);
    }
    foreach (Lookup lookup in NamedLookups(module))
    {
        TypeDef target = lookup.Type.ResolveTypeDef();
        string problem = target == null ? "type " + lookup.Type.FullName : Problem(target, lookup);
        if (problem != null) missing.Add(problem + " (looked up in " + lookup.User + ")");
    }

    string version = Version(managed);
    foreach (string line in missing)
        Console.WriteLine($"FAIL {version}: {line}");
    Console.WriteLine($"{version}: {missing.Count} missing");
    failures += missing.Count;
}
return failures == 0 ? 0 : 1;

// The game version a Managed folder belongs to: from globalgamemanagers beside it, else from the
// name of a folder that holds it, as in ~/ti-refs/1.0.62/TerraInvicta_Data/Managed.
static string Version(string managed)
{
    string full = Path.GetFullPath(managed).TrimEnd(Path.DirectorySeparatorChar);
    string parent = Path.GetDirectoryName(full);
    string file = Path.Combine(parent, "globalgamemanagers");
    var pattern = new System.Text.RegularExpressions.Regex(@"1\.0\.\d+[a-z]?");
    if (File.Exists(file))
    {
        var match = pattern.Match(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(file)));
        if (match.Success) return match.Value;
    }
    for (string folder = parent; !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
    {
        var match = pattern.Match(Path.GetFileName(folder));
        if (match.Success) return match.Value;
    }
    return managed;
}

// Why Harmony would not find the member, or null when it would.
static string Problem(TypeDef type, Lookup lookup)
{
    string what = lookup.Kind + " " + type.FullName + "." + lookup.Name;
    switch (lookup.Kind)
    {
        case "getter":
        case "setter":
            for (TypeDef t = type; t != null; t = lookup.Declared ? null : t.BaseType?.ResolveTypeDef())
            {
                PropertyDef property = t.Properties.FirstOrDefault(p => p.Name == lookup.Name);
                if (property != null)
                    return (lookup.Kind == "getter" ? property.GetMethod : property.SetMethod) != null ? null : what + " has no " + lookup.Kind;
            }
            return what;
        case "field":
            for (TypeDef t = type; t != null; t = lookup.Declared ? null : t.BaseType?.ResolveTypeDef())
                if (t.Fields.Any(f => f.Name == lookup.Name)) return null;
            return what;
        case "method":
            // Harmony takes the first type, walking up, that declares the name.
            for (TypeDef t = type; t != null; t = lookup.Declared ? null : t.BaseType?.ResolveTypeDef())
            {
                int count = t.Methods.Count(m => m.Name == lookup.Name);
                if (count > 1 && !lookup.HasParameterTypes) return what + " has " + count + " overloads and no parameter types are given";
                if (count > 0) return null;
            }
            return what;
        default:
            for (TypeDef t = type; t != null; t = t.BaseType?.ResolveTypeDef())
                if (t.Methods.Any(m => m.Name == lookup.Name) || t.Fields.Any(f => f.Name == lookup.Name)
                    || t.Properties.Any(p => p.Name == lookup.Name) || t.NestedTypes.Any(n => n.Name == lookup.Name))
                    return null;
            return what;
    }
}

// The kind of member an AccessTools method finds, and whether it looks on base types.
static (string kind, bool declared) KindOf(string accessTools) => accessTools switch
{
    "Method" or "FirstMethod" => ("method", false),
    "DeclaredMethod" => ("method", true),
    "Field" or "FieldRefAccess" or "StaticFieldRefAccess" => ("field", false),
    "DeclaredField" => ("field", true),
    "PropertyGetter" => ("getter", false),
    "DeclaredPropertyGetter" => ("getter", true),
    "PropertySetter" => ("setter", false),
    "DeclaredPropertySetter" => ("setter", true),
    _ => ("member", false),
};

// Each (type, name) pair the DLL hands to Harmony or AccessTools, with where it does it.
static IEnumerable<Lookup> NamedLookups(ModuleDefMD module)
{
    foreach (TypeDef type in module.GetTypes())
    {
        Lookup onType = Patch(type.CustomAttributes);
        foreach (MethodDef method in type.Methods)
        {
            Lookup onMethod = Patch(method.CustomAttributes);
            ITypeDefOrRef target = onMethod.Type ?? onType.Type;
            string name = onMethod.Name ?? onType.Name;
            if (target != null && name != null)
            {
                Lookup patch = onMethod.Name != null ? onMethod : onType;
                yield return patch with { Type = target, Name = name, User = type.FullName };
            }
            if (!method.HasBody) continue;
            IList<Instruction> code = method.Body.Instructions;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].OpCode != OpCodes.Ldstr) continue;
                string text = (string)code[i].Operand;
                // AccessTools.X(typeof(T), "name", ...): the type token sits just before the name.
                if (i >= 2 && code[i - 2].OpCode == OpCodes.Ldtoken && code[i - 2].Operand is ITypeDefOrRef token
                    && AccessToolsCall(code, i + 1) is Lookup call)
                    yield return call with { Type = token, Name = text, User = method.FullName };
                // AccessTools.FieldRefAccess<T, F>("name"): the type is the first generic argument.
                else if (i + 1 < code.Count && code[i + 1].Operand is MethodSpec spec
                    && spec.Method.DeclaringType.FullName == "HarmonyLib.AccessTools"
                    && spec.GenericInstMethodSig.GenericArguments.Count > 0)
                    yield return new Lookup(spec.GenericInstMethodSig.GenericArguments[0].ToTypeDefOrRef(), text,
                        method.FullName, KindOf(spec.Method.Name).kind, KindOf(spec.Method.Name).declared, false);
            }
        }
    }

    // TargetMethods that loop over names: every name in TargetMethods and in the iterator and
    // lambdas the compiler makes from it goes to the one AccessTools call they hold.
    foreach (TypeDef type in module.GetTypes().Where(t => t.Methods.Any(m => m.Name == "TargetMethods")))
    {
        List<MethodDef> group = type.Methods.Where(m => m.Name == "TargetMethods").ToList();
        foreach (TypeDef nested in type.NestedTypes)
            group.AddRange(nested.Methods.Where(m => nested.Name.String.Contains("<TargetMethods>") || m.Name.String.Contains("<TargetMethods>")));
        var calls = new List<Lookup>();
        var names = new List<string>();
        foreach (MethodDef method in group.Where(m => m.HasBody))
        {
            IList<Instruction> code = method.Body.Instructions;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].OpCode == OpCodes.Ldstr) names.Add((string)code[i].Operand);
                // typeof(T) is ldtoken then Type.GetTypeFromHandle; the name comes after.
                else if (code[i].OpCode == OpCodes.Ldtoken && code[i].Operand is ITypeDefOrRef token
                    && i + 2 < code.Count && code[i + 2].OpCode != OpCodes.Ldstr && AccessToolsCall(code, i + 2) is Lookup call)
                    calls.Add(call with { Type = token, User = type.FullName + "::TargetMethods" });
            }
        }
        if (calls.Count == 1)
            foreach (string name in names)
                yield return calls[0] with { Name = name };
    }
}

// The AccessTools call within the next few instructions, before another name, or null.
static Lookup AccessToolsCall(IList<Instruction> code, int from)
{
    for (int i = from; i < code.Count && i < from + 12; i++)
    {
        if (code[i].OpCode == OpCodes.Ldstr) return null;
        if ((code[i].OpCode == OpCodes.Call || code[i].OpCode == OpCodes.Callvirt) && code[i].Operand is IMethod called)
        {
            if (called.DeclaringType?.FullName != "HarmonyLib.AccessTools") return null;
            (string kind, bool declared) = KindOf(called.Name);
            // Method(type, name, parameters = null, generics = null): parameter types are given
            // unless the two arguments before the call are both null.
            bool typed = kind != "method" || !(i >= 2 && code[i - 1].OpCode == OpCodes.Ldnull && code[i - 2].OpCode == OpCodes.Ldnull);
            return new Lookup(null, null, null, kind, declared, typed);
        }
    }
    return null;
}

// The type and name of a [HarmonyPatch] attribute, either of which may be absent.
static Lookup Patch(CustomAttributeCollection attributes)
{
    ITypeDefOrRef type = null;
    string name = null, kind = "method";
    bool typed = false;
    foreach (CustomAttribute attribute in attributes.Where(a => a.TypeFullName == "HarmonyLib.HarmonyPatch"))
    {
        foreach (CAArgument argument in attribute.ConstructorArguments)
        {
            if (argument.Type.FullName == "HarmonyLib.MethodType")
                kind = Convert.ToInt32(argument.Value) switch { 1 => "getter", 2 => "setter", 0 => "method", _ => "other" };
            else if (argument.Value is TypeSig sig) type ??= sig.ToTypeDefOrRef();
            else if (argument.Value is UTF8String text) name ??= text.String;
            else if (argument.Value is IList<CAArgument>) typed = true;
        }
    }
    // A constructor or enumerator patch carries no name to check.
    return new Lookup(type, kind == "other" ? null : name, null, kind, true, typed);
}

record Lookup(ITypeDefOrRef Type, string Name, string User, string Kind, bool Declared, bool HasParameterTypes);
