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
                    if (member.IsStatic)
                        continue;
                    if (seen.Add(member))
                        yield return member;
                }
                currentType = currentType.BaseType;
            }

            foreach (var iface in typeSymbol.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (member.IsStatic)
                        continue;
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

            const string suffix = "Attribute";

            static string Normalize(string name)
            {
                name = name.Trim();
                if (name.StartsWith("global::", StringComparison.Ordinal))
                    name = name.Substring("global::".Length);

                return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? name.Substring(0, name.Length - suffix.Length)
                    : name;
            }

            var attrFull = Normalize(attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)));
            var targetFull = Normalize(fullyQualifiedAttributeName.Trim());

            // If the target does not specify a qualifier, compare using the simple name only.
            if (!targetFull.Contains('.'))
            {
                var attrSimple = attrFull.Contains('.') ? attrFull[(attrFull.LastIndexOf('.') + 1)..] : attrFull;
                return string.Equals(attrSimple, targetFull, StringComparison.OrdinalIgnoreCase);
            }

            var attrLastDot = attrFull.LastIndexOf('.');
            if (attrLastDot < 0)
                return false;

            var attrQualifier = attrFull[..attrLastDot];
            var attrName = attrFull[(attrLastDot + 1)..];

            var targetLastDot = targetFull.LastIndexOf('.');
            var targetQualifier = targetFull[..targetLastDot];
            var targetName = targetFull[(targetLastDot + 1)..];

            return string.Equals(attrName, targetName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(attrQualifier, targetQualifier, StringComparison.OrdinalIgnoreCase);
        }

        public static bool InheritsFrom(INamedTypeSymbol? typeSymbol, string baseTypeFullName)
        {
            static string Normalize(string name)
            {
                name = name.Trim();
                if (name.StartsWith("global::", StringComparison.Ordinal))
                    name = name.Substring("global::".Length);
                var genericPos = name.IndexOf('<');
                if (genericPos >= 0)
                    name = name.Substring(0, genericPos);
                return name;
            }

            static bool SymbolMatches(INamedTypeSymbol symbol, string fullName)
            {
                var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                var symbolNorm = Normalize(fqn);
                var targetNorm = Normalize(fullName);

                if (string.Equals(symbolNorm, targetNorm, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Allow matching open generic type definitions by comparing without generic arguments
                if (!targetNorm.Contains('<') && string.Equals(StripGenerics(symbolNorm), targetNorm, StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            }

            static bool InterfaceMatches(INamedTypeSymbol symbol, string fullName)
            {
                if (SymbolMatches(symbol, fullName))
                    return true;
                foreach (var iface in symbol.Interfaces)
                {
                    if (InterfaceMatches(iface, fullName))
                        return true;
                }
                return false;
            }

            while (typeSymbol != null && typeSymbol.SpecialType != SpecialType.System_Object)
            {
                if (SymbolMatches(typeSymbol, baseTypeFullName))
                    return true;

                foreach (var iface in typeSymbol.Interfaces)
                {
                    if (InterfaceMatches(iface, baseTypeFullName))
                        return true;
                }

                typeSymbol = typeSymbol.BaseType;
            }
            return false;
        }
    }
}
