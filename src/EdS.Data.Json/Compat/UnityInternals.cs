// Lot 10 : dans Unity, les attributs InternalsVisibleTo des .csproj ne sont pas générés ; EdS.Net lit ses messages avec l'analyseur
// strict interne (JsonInput), comme dans la compilation .NET (EdS.Data.Json.csproj).
#if UNITY_5_3_OR_NEWER
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EdS.Net")]
#endif
