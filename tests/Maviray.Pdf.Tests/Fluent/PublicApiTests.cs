using System.Globalization;
using System.Reflection;
using Maviray.Pdf.Tests.Text;

namespace Maviray.Pdf.Tests.Fluent;

// The public API of each package's assembly (L-16), listed in src/<assembly>/PublicApi.txt, so a change to the API is a
// change to the listing that a review sees. Each exported type comes with its kind and base types, then each public or
// protected member as its signature. Setting MAVIRAY_PDF_WRITE_PUBLIC_API=true rewrites the listings.
public sealed class PublicApiTests
{
    private const string WriteVariable = "MAVIRAY_PDF_WRITE_PUBLIC_API";

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(void)] = "void",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(nint)] = "nint",
        [typeof(nuint)] = "nuint",
    };

    private readonly NullabilityInfoContext _nullability = new();

    [Theory]
    [InlineData("Maviray.Pdf")]
    [InlineData("Maviray.Pdf.Fonts.Default")]
    public void ThePublicApiMatchesItsListing(string assemblyName)
    {
        var actual = Listing(Assembly.Load(assemblyName));
        var listingPath = $"src/{assemblyName}/PublicApi.txt";
        var path = RepositoryFiles.PathOf(listingPath);
        if (Environment.GetEnvironmentVariable(WriteVariable) == "true")
        {
            File.WriteAllText(path, string.Join('\n', actual) + "\n");
        }

        Assert.True(File.Exists(path), $"{listingPath} is missing. Run the tests with {WriteVariable}=true to write it.");
        var listed = File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        for (var i = 0; i < Math.Max(listed.Length, actual.Count); i++)
        {
            var want = i < listed.Length ? listed[i] : "(the listing ends)";
            var got = i < actual.Count ? actual[i] : "(the API ends)";
            Assert.True(
                string.Equals(want, got, StringComparison.Ordinal),
                $"{listingPath}, line {i + 1}:\n  listed: {want}\n  actual: {got}\nIf the change is intended, run the tests with {WriteVariable}=true and review the listing's diff.");
        }
    }

    private List<string> Listing(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            lines.Add(TypeLine(type));
            lines.AddRange(Members(type).Order(StringComparer.Ordinal).Select(member => "  " + member));
        }

        return lines;
    }

    private static string TypeLine(Type type)
    {
        string kind;
        var bases = new List<string>();
        if (type.IsEnum)
        {
            kind = "enum";
            bases.Add(Name(Enum.GetUnderlyingType(type)));
        }
        else if (type.IsInterface)
        {
            kind = "interface";
        }
        else if (typeof(Delegate).IsAssignableFrom(type))
        {
            kind = "delegate";
        }
        else if (type.IsValueType)
        {
            kind = (HasAttribute(type.CustomAttributes, "IsReadOnlyAttribute") ? "readonly " : "") + "struct";
        }
        else
        {
            kind = (type.IsAbstract && type.IsSealed ? "static " : type.IsSealed ? "sealed " : type.IsAbstract ? "abstract " : "") + "class";
            if (type.BaseType is { } baseType && baseType != typeof(object))
            {
                bases.Add(Name(baseType));
            }
        }

        if (!type.IsEnum && kind != "delegate")
        {
            bases.AddRange(type.GetInterfaces().Select(face => Name(face)).Order(StringComparer.Ordinal));
        }

        return $"{kind} {Name(type)}" + (bases.Count > 0 ? " : " + string.Join(", ", bases) : "");
    }

    private IEnumerable<string> Members(Type type)
    {
        foreach (var member in type.GetMembers(Declared))
        {
            var line = member switch
            {
                ConstructorInfo constructor when Visible(constructor) => Access(constructor) + Modifiers(constructor) + $".ctor({Parameters(constructor)})",
                MethodInfo method when Visible(method) && (!method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal)) => MethodLine(method),
                PropertyInfo property => PropertyLine(property),
                FieldInfo field when (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) && !field.IsSpecialName => FieldLine(field),
                EventInfo e when e.AddMethod is { } add && Visible(add) => Access(add) + Modifiers(add) + $"event {Name(e.EventHandlerType!)} {e.Name}",
                _ => null,
            };
            if (line is not null)
            {
                yield return line;
            }
        }
    }

    private string MethodLine(MethodInfo method)
    {
        var arity = method.IsGenericMethodDefinition ? $"<{string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name))}>" : "";
        var returns = Name(method.ReturnType, _nullability.Create(method.ReturnParameter));
        return Access(method) + Modifiers(method) + $"{returns} {method.Name}{arity}({Parameters(method)})";
    }

    private string? PropertyLine(PropertyInfo property)
    {
        var accessors = new List<string>();
        if (property.GetMethod is { } get && Visible(get))
        {
            accessors.Add(Access(get) + "get;");
        }

        if (property.SetMethod is { } set && Visible(set))
        {
            var init = set.ReturnParameter.GetRequiredCustomModifiers().Any(modifier => modifier.Name == "IsExternalInit");
            accessors.Add(Access(set) + (init ? "init;" : "set;"));
        }

        if (accessors.Count == 0)
        {
            return null;
        }

        var accessor = property.GetMethod ?? property.SetMethod!;
        var indexes = property.GetIndexParameters();
        var name = indexes.Length > 0 ? $"this[{string.Join(", ", indexes.Select(Parameter))}]" : property.Name;
        return Modifiers(accessor) + $"{Name(property.PropertyType, _nullability.Create(property))} {name} {{ {string.Join(" ", accessors)} }}";
    }

    private string FieldLine(FieldInfo field)
    {
        if (field.DeclaringType!.IsEnum)
        {
            return $"{field.Name} = {Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)}";
        }

        var modifiers = field.IsLiteral ? "const " : (field.IsStatic ? "static " : "") + (field.IsInitOnly ? "readonly " : "");
        var value = field.IsLiteral ? " = " + Literal(field.FieldType, field.GetRawConstantValue()) : "";
        return (field.IsFamily || field.IsFamilyOrAssembly ? "protected " : "") + modifiers + $"{Name(field.FieldType, _nullability.Create(field))} {field.Name}{value}";
    }

    private string Parameters(MethodBase method)
    {
        var parameters = method.GetParameters();
        var extension = method.IsStatic && HasAttribute(method.CustomAttributes, "ExtensionAttribute");
        return string.Join(", ", parameters.Select((parameter, index) => (extension && index == 0 ? "this " : "") + Parameter(parameter)));
    }

    private string Parameter(ParameterInfo parameter)
    {
        var modifier = parameter.ParameterType.IsByRef
            ? parameter.IsOut ? "out " : HasAttribute(parameter.CustomAttributes, "IsReadOnlyAttribute") ? "in " : "ref "
            : HasAttribute(parameter.CustomAttributes, "ParamArrayAttribute") ? "params " : "";
        var type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
        var value = parameter.HasDefaultValue ? " = " + Literal(type, parameter.DefaultValue) : "";
        return $"{modifier}{Name(type, _nullability.Create(parameter))} {parameter.Name}{value}";
    }

    private static string Literal(Type type, object? value)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return value switch
        {
            null => type.IsValueType && Nullable.GetUnderlyingType(type) is null ? "default" : "null",
            string text => "\"" + text + "\"",
            bool flag => flag ? "true" : "false",
            _ when underlying.IsEnum => $"{Name(underlying)}.{Enum.ToObject(underlying, value)}",
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private static string Name(Type type, NullabilityInfo? nullability = null)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Name(underlying, nullability?.GenericTypeArguments.FirstOrDefault()) + "?";
        }

        var mark = !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? "?" : "";
        if (type.IsArray)
        {
            return Name(type.GetElementType()!, nullability?.ElementType) + $"[{new string(',', type.GetArrayRank() - 1)}]" + mark;
        }

        if (type.IsGenericParameter)
        {
            return type.Name + mark;
        }

        if (Keywords.TryGetValue(type, out var keyword))
        {
            return keyword + mark;
        }

        var name = type.IsNested ? Name(type.DeclaringType!) + "." + type.Name : type.Namespace + "." + type.Name;
        if (type.IsGenericType)
        {
            var arguments = type.GetGenericArguments();
            var names = arguments.Select((argument, index) => Name(argument, nullability?.GenericTypeArguments.ElementAtOrDefault(index)));
            name = name[..name.IndexOf('`', StringComparison.Ordinal)] + $"<{string.Join(", ", names)}>";
        }

        return name + mark;
    }

    private static string Access(MethodBase method) => method.IsFamily || method.IsFamilyOrAssembly ? "protected " : "";

    private static string Modifiers(MethodBase method)
    {
        var modifiers = method.IsStatic ? "static " : "";
        if (method is MethodInfo info && !method.DeclaringType!.IsInterface)
        {
            if (info.IsAbstract)
            {
                modifiers += "abstract ";
            }
            else if (info.GetBaseDefinition().DeclaringType != info.DeclaringType)
            {
                modifiers += "override ";
            }
            else if (info.IsVirtual && !info.IsFinal)
            {
                modifiers += "virtual ";
            }
        }

        return modifiers;
    }

    private static bool Visible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static bool HasAttribute(IEnumerable<CustomAttributeData> attributes, string name) =>
        attributes.Any(attribute => attribute.AttributeType.Name == name);
}
