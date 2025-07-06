using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GrpcRemoteMvvmModelUtil
{
    public static class Helpers
    {
        public static IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol typeSymbol)
        {
            var currentType = typeSymbol;
            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in currentType.GetMembers())
                {
                    yield return member;
                }
                currentType = currentType.BaseType;
            }
        }

        public static bool AttributeMatches(AttributeData attributeData, string fullyQualifiedAttributeName)
        {
            var fqn = attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
            if (fqn == fullyQualifiedAttributeName)
                return true;

            var shortName = attributeData.AttributeClass?.Name;
            if (shortName == null)
                return false;

            var targetShort = fullyQualifiedAttributeName.Split('.').Last();
            if (targetShort.EndsWith("Attribute"))
                targetShort = targetShort.Substring(0, targetShort.Length - "Attribute".Length);

            var simpleName = shortName;
            if (simpleName.EndsWith("Attribute"))
                simpleName = simpleName.Substring(0, simpleName.Length - "Attribute".Length);

            return simpleName == targetShort || shortName == targetShort || shortName == fullyQualifiedAttributeName;
        }

        public static bool InheritsFrom(INamedTypeSymbol? typeSymbol, string baseTypeFullName)
        {
            while (typeSymbol != null && typeSymbol.SpecialType != SpecialType.System_Object)
            {
                var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                if (fqn == baseTypeFullName)
                    return true;
                typeSymbol = typeSymbol.BaseType;
            }
            return false;
        }
    }
}
