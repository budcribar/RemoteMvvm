using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtoGeneratorUtil
{
    public static class Helpers
    {
        // Place this helper method within your Program class or a utility class
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
