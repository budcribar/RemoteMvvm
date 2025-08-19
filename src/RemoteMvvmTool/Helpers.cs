using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GrpcRemoteMvvmModelUtil
{
    public static class Helpers
    {
        public static IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol typeSymbol)
        {
            var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var currentType = typeSymbol;
            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in currentType.GetMembers())
                {
                    if (seen.Add(member))
                        yield return member;
                }
                currentType = currentType.BaseType;
            }

            foreach (var iface in typeSymbol.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (seen.Add(member))
                        yield return member;
                }
            }
        }

        public static bool AttributeMatches(AttributeData attributeData, string fullyQualifiedAttributeName)
        {
            var attrClass = attributeData.AttributeClass;
            if (attrClass == null)
                return false;

            var fqn = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
            if (string.Equals(fqn, fullyQualifiedAttributeName, StringComparison.Ordinal))
                return true;

            string? attrNamespace = attrClass.ContainingNamespace?.ToDisplayString();
            var attrName = attrClass.Name;
            if (attrName.EndsWith("Attribute", StringComparison.Ordinal))
                attrName = attrName.Substring(0, attrName.Length - "Attribute".Length);

            string? targetNamespace = null;
            var targetName = fullyQualifiedAttributeName;
            var lastDot = fullyQualifiedAttributeName.LastIndexOf('.');
            if (lastDot >= 0)
            {
                targetNamespace = fullyQualifiedAttributeName.Substring(0, lastDot);
                targetName = fullyQualifiedAttributeName.Substring(lastDot + 1);
            }
            if (targetName.EndsWith("Attribute", StringComparison.Ordinal))
                targetName = targetName.Substring(0, targetName.Length - "Attribute".Length);

            if (!string.Equals(attrName, targetName, StringComparison.Ordinal))
                return false;

            if (targetNamespace != null)
                return string.Equals(attrNamespace, targetNamespace, StringComparison.Ordinal);

            return true;
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
