// Sous-lot 9.3 : dans Unity, les attributs InternalsVisibleTo des .csproj ne sont pas générés ; EdS.Simulation lit des membres
// internes du domaine, comme dans la compilation .NET (EdS.Domain.csproj).
#if UNITY_5_3_OR_NEWER
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EdS.Simulation")]
#endif
