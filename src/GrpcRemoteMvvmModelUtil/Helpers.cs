using Microsoft.CodeAnalysis;
using System.Collections.Generic;

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
    }
}
