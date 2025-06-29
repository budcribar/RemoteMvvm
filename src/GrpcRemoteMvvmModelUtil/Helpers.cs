using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;

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
            var trimmed = Path.GetFileNameWithoutExtension(fullyQualifiedAttributeName);
            return shortName == trimmed || shortName == fullyQualifiedAttributeName;
        }
    }
}
